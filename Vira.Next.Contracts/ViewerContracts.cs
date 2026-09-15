namespace Vira.Next.Contracts;

public interface ICadViewerConverter
{
    string ConverterId { get; }
    string ConverterVersion { get; }
    Task<ViewerConversionResult> ConvertAsync(string sourceStepPath, string outputGlbPath, CancellationToken cancellationToken = default);
}

public sealed record ViewerConversionResult
{
    public bool Success { get; init; }
    public string ConverterId { get; init; } = string.Empty;
    public string ConverterVersion { get; init; } = string.Empty;
    public string SourceSha256 { get; init; } = string.Empty;
    public string GlbSha256 { get; init; } = string.Empty;
    public long GlbByteLength { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public interface IVendorCadProvider
{
    Task<McMasterProductSnapshot> GetProductAsync(string partNumber, CancellationToken cancellationToken = default);
    Task<byte[]> GetCadBytesAsync(string normalizedCadUrl, CancellationToken cancellationToken = default);
    Task EnsureSubscribedAsync(string partNumber, CancellationToken cancellationToken = default);
}

public interface ICadAssetRepository
{
    Task<VendorCadAsset?> FindByShaAsync(string sha256, CancellationToken cancellationToken = default);
    Task<VendorCadAsset> StoreAsync(string partNumber, string sourceCadLink, string normalizedCadUrl, byte[] bytes, McMasterProductSnapshot? product, CancellationToken cancellationToken = default);
    Task<ViewerDerivative?> FindDerivativeAsync(string sourceSha256, CancellationToken cancellationToken = default);
    Task<ViewerDerivative> StoreDerivativeAsync(ViewerDerivative derivative, byte[] glbBytes, CancellationToken cancellationToken = default);
}
