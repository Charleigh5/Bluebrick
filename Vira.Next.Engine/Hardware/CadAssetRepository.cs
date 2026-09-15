using System.Text.Json;
using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class CadAssetRepository : ICadAssetRepository
{
    private readonly string _cacheRoot;
    private readonly string _manifestPath;
    private readonly CachePathGuard _pathGuard;

    public CadAssetRepository(string? cacheRoot = null, CachePathGuard? pathGuard = null)
    {
        _cacheRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cacheRoot ?? Path.Combine(AppContext.BaseDirectory, "vira-cad-cache")));
        _pathGuard = pathGuard ?? new CachePathGuard();
        _pathGuard.RequireRegularAncestry(_cacheRoot);
        Directory.CreateDirectory(_cacheRoot);
        _manifestPath = Path.Combine(_cacheRoot, "manifest.jsonl");
    }

    public async Task<VendorCadAsset?> FindByShaAsync(string sha256, CancellationToken cancellationToken = default)
    {
        var path = ContentAddressedCache.GetShardPath(_cacheRoot, sha256, ".step");
        cancellationToken.ThrowIfCancellationRequested();
        if (!SafePath(path) || !SafePath(path + ".meta.json") || !File.Exists(path) || !File.Exists(path + ".meta.json")) return null;
        try
        {
            var asset = JsonSerializer.Deserialize<VendorCadAsset>(await File.ReadAllTextAsync(path + ".meta.json", cancellationToken));
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return asset != null && SourceMatches(asset, sha256, bytes) && SamePath(asset.ContentAddressedPath, path) ? asset : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    public async Task<VendorCadAsset> StoreAsync(string partNumber, string sourceCadLink, string normalizedCadUrl, byte[] bytes, McMasterProductSnapshot? product, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || (product != null && !string.Equals(product.PartNumber, partNumber, StringComparison.Ordinal)))
            throw new InvalidDataException("Source product identity or bytes are invalid.");
        var sha = ContentAddressedCache.ComputeSha256(bytes);
        var path = ContentAddressedCache.GetShardPath(_cacheRoot, sha, ".step");
        RequireSafe(path);
        RequireSafe(path + ".meta.json");
        if (File.Exists(path) || File.Exists(path + ".meta.json"))
        {
            var existing = await FindByShaAsync(sha, cancellationToken);
            if (existing == null || existing.PartNumber != partNumber || existing.SourceCadLink != sourceCadLink ||
                existing.NormalizedCadUrl != normalizedCadUrl || JsonSerializer.Serialize(existing.ProductSnapshot) != JsonSerializer.Serialize(product))
                throw new InvalidDataException("Source cache bytes or metadata collision.");
            return existing;
        }
        var asset = new VendorCadAsset { AssetId = sha, PartNumber = partNumber, SourceCadLink = sourceCadLink,
            NormalizedCadUrl = normalizedCadUrl, Sha256 = sha, ByteLength = bytes.Length, AcquiredAtUtc = DateTime.UtcNow,
            ProductSnapshot = product, ContentAddressedPath = path };
        await ContentAddressedCache.WriteIfNotExistsAsync(path, bytes, cancellationToken, _pathGuard);
        await WriteMetadataExclusiveAsync(path + ".meta.json", asset, cancellationToken);
        RequireSafe(_manifestPath);
        await File.AppendAllTextAsync(_manifestPath, JsonSerializer.Serialize(asset) + Environment.NewLine, cancellationToken);
        return asset;
    }

    public async Task<ViewerDerivative?> FindDerivativeAsync(string sourceSha256, CancellationToken cancellationToken = default)
    {
        var path = ContentAddressedCache.GetShardPath(_cacheRoot, sourceSha256, ".glb");
        var asset = await FindByShaAsync(sourceSha256, cancellationToken);
        if (asset == null || !SafePath(path) || !SafePath(path + ".meta.json") || !File.Exists(path) || !File.Exists(path + ".meta.json")) return null;
        try
        {
            var derivative = JsonSerializer.Deserialize<ViewerDerivative>(await File.ReadAllTextAsync(path + ".meta.json", cancellationToken));
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return derivative != null && DerivativeEvidence.Matches(derivative, asset, bytes) && SamePath(derivative.GlbContentAddressedPath, path) ? derivative : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    public async Task<ViewerDerivative> StoreDerivativeAsync(ViewerDerivative derivative, byte[] glbBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(derivative);
        ArgumentNullException.ThrowIfNull(glbBytes);
        if (!ContentAddressedCache.IsSha256(derivative.SourceSha256)) throw new InvalidDataException("Derivative source identity is invalid.");
        var asset = await FindByShaAsync(derivative.SourceSha256, cancellationToken);
        if (asset == null || !DerivativeEvidence.Matches(derivative, asset, glbBytes)) throw new InvalidDataException("Derivative evidence does not match source or GLB bytes.");
        var path = ContentAddressedCache.GetShardPath(_cacheRoot, derivative.SourceSha256, ".glb");
        RequireSafe(path);
        RequireSafe(path + ".meta.json");
        if (!string.IsNullOrEmpty(derivative.GlbContentAddressedPath) && !SamePath(derivative.GlbContentAddressedPath, path)) throw new InvalidDataException("Derivative path mismatch.");
        var stored = derivative with { GlbContentAddressedPath = path };
        if (File.Exists(path) || File.Exists(path + ".meta.json"))
        {
            var existing = await FindDerivativeAsync(derivative.SourceSha256, cancellationToken);
            if (existing == null || JsonSerializer.Serialize(existing) != JsonSerializer.Serialize(stored)) throw new InvalidDataException("Derivative cache collision.");
            return existing;
        }
        await ContentAddressedCache.WriteIfNotExistsAsync(path, glbBytes, cancellationToken, _pathGuard);
        await WriteMetadataExclusiveAsync(path + ".meta.json", stored, cancellationToken);
        return stored;
    }

    private static bool SourceMatches(VendorCadAsset asset, string sha, byte[] bytes) =>
        ContentAddressedCache.SameSha(asset.AssetId, sha) && ContentAddressedCache.SameSha(asset.Sha256, sha) &&
        ContentAddressedCache.SameSha(ContentAddressedCache.ComputeSha256(bytes), sha) && bytes.Length > 0 && asset.ByteLength == bytes.Length &&
        !string.IsNullOrWhiteSpace(asset.PartNumber) && (asset.ProductSnapshot == null || asset.ProductSnapshot.PartNumber == asset.PartNumber);

    private static bool SamePath(string actual, string expected) => !string.IsNullOrWhiteSpace(actual) && Path.IsPathFullyQualified(actual) &&
        string.Equals(Path.GetFullPath(actual), expected, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private bool SafePath(string path)
    {
        var relative = Path.GetRelativePath(_cacheRoot, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;
        try { _pathGuard.RequireRegularAncestry(path); return true; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return false; }
    }

    private void RequireSafe(string path)
    {
        if (!SafePath(path)) throw new InvalidDataException("Cache path is not a contained regular path.");
    }

    private async Task WriteMetadataExclusiveAsync<T>(string path, T value, CancellationToken ct)
    {
        RequireSafe(path);
        // CreateNew never overwrites another writer's metadata. Interrupted pairs remain untrusted.
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, value, cancellationToken: ct);
    }
}

internal static class DerivativeEvidence
{
    public static bool HasConverter(string? id, string? version) => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version) &&
        !id.Contains("stub", StringComparison.OrdinalIgnoreCase) && !id.Equals("vira.chained-step-to-glb.v1", StringComparison.Ordinal);

    public static bool MatchesSource(ViewerDerivative derivative, VendorCadAsset asset) => derivative.IsValid &&
        ContentAddressedCache.SameSha(derivative.SourceSha256, asset.Sha256) && string.Equals(derivative.SourceAssetId, asset.AssetId, StringComparison.Ordinal) &&
        ContentAddressedCache.IsSha256(derivative.GlbSha256) && derivative.GlbByteLength > 0 && HasConverter(derivative.ConverterId, derivative.ConverterVersion);

    public static bool Matches(ViewerDerivative derivative, VendorCadAsset asset, byte[] bytes) => MatchesSource(derivative, asset) &&
        derivative.GlbByteLength == bytes.Length && ContentAddressedCache.SameSha(derivative.GlbSha256, ContentAddressedCache.ComputeSha256(bytes)) && GlbValidator.Validate(bytes).valid;
}
