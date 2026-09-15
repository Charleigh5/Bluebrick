using System.Security.Cryptography;

namespace Vira.Next.Engine.Hardware;

public static class ContentAddressedCache
{
    public static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeSha256(string filePath)
    {
        new CachePathGuard().RequireRegularAncestry(filePath);
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GetShardPath(string root, string sha256, string extension)
    {
        if (!IsSha256(sha256)) throw new ArgumentException("SHA-256 must contain exactly 64 hexadecimal characters.", nameof(sha256));
        if (extension is not (".step" or ".glb")) throw new ArgumentException("Unsupported cache extension.", nameof(extension));
        sha256 = sha256.ToLowerInvariant();
        return Path.GetFullPath(Path.Combine(root, sha256[..2], $"{sha256}{extension}"));
    }

    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public static bool SameSha(string? a, string? b) => IsSha256(a) && IsSha256(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static void EnsureDirectory(string filePath, CachePathGuard? pathGuard = null)
    {
        (pathGuard ?? new CachePathGuard()).RequireRegularAncestry(filePath);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
    }

    public static async Task<string> WriteIfNotExistsAsync(string path, byte[] bytes, CancellationToken ct = default, CachePathGuard? pathGuard = null)
    {
        path = Path.GetFullPath(path);
        pathGuard ??= new CachePathGuard();
        pathGuard.RequireRegularAncestry(path);
        if (File.Exists(path))
        {
            if (!(await File.ReadAllBytesAsync(path, ct)).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("Cache object collision.");
            return path;
        }
        EnsureDirectory(path, pathGuard);
        var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            pathGuard.RequireRegularAncestry(tmp);
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            pathGuard.RequireRegularAncestry(path);
            try { File.Move(tmp, path); }
            catch (IOException) when (File.Exists(path))
            {
                if (!(await File.ReadAllBytesAsync(path, ct)).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException("Cache object collision.");
            }
            return path;
        }
        finally { pathGuard.RequireRegularAncestry(tmp); if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
