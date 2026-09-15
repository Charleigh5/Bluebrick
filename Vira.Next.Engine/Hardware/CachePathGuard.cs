namespace Vira.Next.Engine.Hardware;

// Attribute-only seam permits ancestry tests without creating OS links or processes.
public sealed class CachePathGuard
{
    private readonly Func<string, FileAttributes?> _readAttributes;

    public CachePathGuard(Func<string, FileAttributes?>? readAttributes = null) =>
        _readAttributes = readAttributes ?? ReadAttributes;

    public void RequireRegularAncestry(string path)
    {
        for (string? current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); current != null; current = Path.GetDirectoryName(current))
        {
            if (_readAttributes(current) is { } attributes && (attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Cache path is not a contained regular path.");
        }
    }

    private static FileAttributes? ReadAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}
