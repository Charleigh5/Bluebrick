using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class HardwareCadBindingService
{
    private readonly IVendorCadProvider _provider;
    private readonly ICadAssetRepository _repository;
    private readonly ICadViewerConverter _converter;
    private readonly McMasterOptions _options;

    public HardwareCadBindingService(IVendorCadProvider provider, ICadAssetRepository repository, ICadViewerConverter converter, McMasterOptions? options = null)
    {
        _provider = provider;
        _repository = repository;
        _converter = converter;
        _options = options ?? new McMasterOptions();
    }

    public async Task<VendorCadAcquisitionReceipt> AcquireAsync(HardwareRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.McMasterPartNumber))
            return Receipt(record, VendorCadAcquisitionStatus.Failed, null, null, null, false, new[] { "HardwareRecord.McMasterPartNumber is required." }, new[] { new ReceiptError { Code = "MISSING_PART_NUMBER", Message = "McMaster part number is required." } }, false);
        var partNumber = record.McMasterPartNumber.Trim();
        var receiptId = $"mcm-{partNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36];

        McMasterProductSnapshot? product = null;
        try
        {
            await _provider.EnsureSubscribedAsync(partNumber, cancellationToken);
        }
        catch (McMasterRateLimitedException ex)
        {
            return Receipt(record, VendorCadAcquisitionStatus.RateLimited, null, null, null, false, new[] { ex.Message }, new[] { new ReceiptError { Code = "RATE_LIMITED", Message = ex.Message } }, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Receipt(record, VendorCadAcquisitionStatus.Failed, null, null, null, false, new[] { ex.Message }, new[] { new ReceiptError { Code = "SUBSCRIBE_FAILED", Message = Truncate(ex.Message, 400) } }, true);
        }

        try
        {
            product = await _provider.GetProductAsync(partNumber, cancellationToken);
            if (product == null || string.IsNullOrWhiteSpace(product.PartNumber) || !string.Equals(product.PartNumber, partNumber, StringComparison.Ordinal))
                throw new InvalidOperationException("Product identity is missing or mismatched.");
        }
        catch (McMasterProductNotFoundException)
        {
            return Receipt(record, VendorCadAcquisitionStatus.ProductNotFound, null, null, null, false, new[] { $"Product {partNumber} not found or not subscribed." }, new[] { new ReceiptError { Code = "PRODUCT_NOT_FOUND", Message = $"Product {partNumber} not found." } }, false);
        }
        catch (McMasterRateLimitedException ex)
        {
            return Receipt(record, VendorCadAcquisitionStatus.RateLimited, null, null, null, false, new[] { ex.Message }, new[] { new ReceiptError { Code = "RATE_LIMITED", Message = ex.Message } }, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Receipt(record, VendorCadAcquisitionStatus.Failed, null, null, null, false, new[] { ex.Message }, new[] { new ReceiptError { Code = "PRODUCT_FETCH_FAILED", Message = Truncate(ex.Message, 400) } }, true);
        }

        var normalizer = new CadLinkNormalizer();
        var linkResult = normalizer.Normalize(product.Links, _options.BaseUrl, partNumber);
        if (!linkResult.IsSuccess)
        {
            return Receipt(record, VendorCadAcquisitionStatus.NoCadAvailable, null, null, null, false, new[] { linkResult.ErrorMessage }, new[] { new ReceiptError { Code = linkResult.ErrorCode, Message = linkResult.ErrorMessage } }, false);
        }

        var binding = new VendorCadBinding
        {
            HardwareRecordId = record.HardwareRecordId,
            McMasterPartNumber = partNumber,
            Vendor = record.Vendor,
            PreferredVariantKey = CadLinkNormalizer.PreferredKey,
            AuthoritativeCadLink = linkResult.RawValue,
            NormalizedCadUrl = linkResult.NormalizedUrl,
            BoundAtUtc = DateTime.UtcNow,
            BindingReceiptId = receiptId
        };

        byte[] cadBytes;
        try
        {
            cadBytes = await _provider.GetCadBytesAsync(linkResult.NormalizedUrl, cancellationToken);
        }
        catch (McMasterRateLimitedException ex)
        {
            return Receipt(record, VendorCadAcquisitionStatus.RateLimited, binding, null, null, false, new[] { ex.Message }, new[] { new ReceiptError { Code = "RATE_LIMITED", Message = ex.Message } }, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Receipt(record, VendorCadAcquisitionStatus.Failed, binding, null, null, false, new[] { ex.Message }, new[] { new ReceiptError { Code = "CAD_RETRIEVAL_FAILED", Message = Truncate(ex.Message, 400) } }, true);
        }

        if (cadBytes == null || cadBytes.Length == 0)
            return Receipt(record, VendorCadAcquisitionStatus.Failed, binding, null, null, false, new[] { "CAD bytes empty." }, new[] { new ReceiptError { Code = "EMPTY_CAD", Message = "CAD retrieval returned empty bytes." } }, true);

        var sha = ContentAddressedCache.ComputeSha256(cadBytes);
        VendorCadAsset? asset = null;
        var cacheHit = false;
        VendorCadAcquisitionReceipt Failure(string code, string message) => Receipt(record,
            VendorCadAcquisitionStatus.Failed,
            binding, asset, null, cacheHit, new[] { message }, new[] { new ReceiptError { Code = code, Message = message } }, false);
        try
        {
            var existing = await _repository.FindByShaAsync(sha, cancellationToken);
            var candidate = existing ?? await _repository.StoreAsync(partNumber, linkResult.RawValue, linkResult.NormalizedUrl, cadBytes, product, cancellationToken);
            if (candidate == null || !ContentAddressedCache.SameSha(candidate.Sha256, sha) || !ContentAddressedCache.SameSha(candidate.AssetId, sha) ||
                candidate.ByteLength != cadBytes.Length || candidate.PartNumber != partNumber ||
                (candidate.ProductSnapshot != null && candidate.ProductSnapshot.PartNumber != partNumber))
                return Failure("SOURCE_PROVENANCE_INVALID", "Cached source identity does not match the requested product and bytes.");
            asset = candidate;
            cacheHit = existing != null;

            var derivative = await _repository.FindDerivativeAsync(sha, cancellationToken);
            if (derivative != null && !DerivativeEvidence.MatchesSource(derivative, asset))
                return Failure("DERIVATIVE_PROVENANCE_INVALID", "Cached derivative does not match the requested source asset.");
            if (derivative == null)
            {
                var tempRoot = Directory.CreateTempSubdirectory("vira-mcm-conversion-").FullName;
                var tmpStep = Path.Combine(tempRoot, "source.step");
                var tmpGlb = Path.Combine(tempRoot, "output.glb");
                var retainTemp = false;
                OperationCanceledException? activeCancellation = null;
                try
                {
                    await File.WriteAllBytesAsync(tmpStep, cadBytes, cancellationToken);
                    var conversion = await _converter.ConvertAsync(tmpStep, tmpGlb, cancellationToken);
                    if (!conversion.Success) return Failure(conversion.ErrorCode, conversion.ErrorMessage);
                    if (!File.Exists(tmpGlb)) return Failure("GLB_MISSING", "Successful conversion did not produce GLB bytes.");
                    var glbBytes = await File.ReadAllBytesAsync(tmpGlb, cancellationToken);
                    var toStore = new ViewerDerivative
                    {
                        SourceSha256 = conversion.SourceSha256,
                        SourceAssetId = asset.AssetId,
                        GlbSha256 = conversion.GlbSha256,
                        GlbByteLength = conversion.GlbByteLength,
                        ConverterId = conversion.ConverterId,
                        ConverterVersion = conversion.ConverterVersion,
                        ConvertedAtUtc = DateTime.UtcNow,
                        Warnings = conversion.Warnings,
                        IsValid = true
                    };
                    if (!DerivativeEvidence.Matches(toStore, asset, glbBytes))
                        return Failure("DERIVATIVE_PROVENANCE_INVALID", "Conversion source, GLB bytes, or converter provenance are invalid.");
                    derivative = await _repository.StoreDerivativeAsync(toStore, glbBytes, cancellationToken);
                    if (!DerivativeEvidence.Matches(derivative, asset, glbBytes) || derivative.ConverterId != conversion.ConverterId || derivative.ConverterVersion != conversion.ConverterVersion)
                        return Failure("DERIVATIVE_PROVENANCE_INVALID", "Stored derivative does not preserve conversion evidence.");
                }
                catch (OperationCanceledException ex)
                {
                    activeCancellation = ex;
                    retainTemp = ex.InnerException is CadChildExitUnconfirmedException;
                    if (retainTemp) ex.Data["RetainedCadTempDirectory"] = tempRoot;
                    throw;
                }
                catch (CadChildExitUnconfirmedException ex)
                {
                    retainTemp = true;
                    return Receipt(record, VendorCadAcquisitionStatus.Failed, binding, asset, null, cacheHit,
                        new[] { $"Invocation temp directory retained because child exit/output release was not confirmed: {tempRoot}" },
                        new[] { new ReceiptError { Code = CadChildExitUnconfirmedException.ErrorCode, Message = ex.Message } }, false);
                }
                finally
                {
                    // A stop request or disposed wrapper does not release a child's paths.
                    if (!retainTemp)
                    {
                        try
                        {
                            try { File.Delete(tmpStep); }
                            finally
                            {
                                try { File.Delete(tmpGlb); }
                                finally { Directory.Delete(tempRoot); }
                            }
                        }
                        catch (Exception ex) when (activeCancellation != null && ex is IOException or UnauthorizedAccessException)
                        {
                            // Preserve the active caller cancellation, with sanitized local recovery detail.
                            activeCancellation.Data["CadTempCleanupFailure"] = "Temporary file cleanup failed; files may remain.";
                            activeCancellation.Data["RetainedCadTempDirectory"] = tempRoot;
                        }
                    }
                }
            }
            return Receipt(record, cacheHit ? VendorCadAcquisitionStatus.CacheHit : VendorCadAcquisitionStatus.Acquired,
                binding, asset, derivative, cacheHit, Array.Empty<string>(), Array.Empty<ReceiptError>(), false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Failure("CAD_CACHE_OR_CONVERSION_FAILED", "Source caching or derivative conversion failed; no viewer derivative is available.");
        }
    }
    private static VendorCadAcquisitionReceipt Receipt(HardwareRecord record, VendorCadAcquisitionStatus status, VendorCadBinding? binding, VendorCadAsset? asset, ViewerDerivative? derivative, bool cacheHit, IReadOnlyList<string> limitations, ReceiptError[] errors, bool safeToRetry)
    {
        return new VendorCadAcquisitionReceipt
        {
            ReceiptId = binding?.BindingReceiptId ?? $"mcm-{record.McMasterPartNumber}-{Guid.NewGuid():N}"[..24],
            TimestampUtc = DateTime.UtcNow,
            HardwareRecordId = record.HardwareRecordId,
            PartNumber = record.McMasterPartNumber,
            Status = status,
            Binding = binding,
            Asset = asset,
            ViewerDerivative = derivative,
            CacheHit = cacheHit,
            Limitations = limitations,
            Errors = errors,
            SafeToRetry = safeToRetry,
            MutationBoundary = EngineeringMutationBoundary.ReadOnly
        };
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...";
}
