namespace Vira.Next.Contracts;

public sealed class FakeHostDocument
{
    public string Title { get; init; } = string.Empty;
    public string DocumentType { get; init; } = "part";
    public string PathHash { get; init; } = string.Empty;
    public bool IsDirty { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public IReadOnlyDictionary<string, string> CustomProperties { get; init; } = new Dictionary<string, string>();
}

public sealed class FakeHostSnapshot
{
    public string HostName { get; init; } = "VIRA Next fake SOLIDWORKS host";
    public string RuntimeVersion { get; init; } = "FAKE-2024-SP5";
    public FakeHostDocument ActiveDocument { get; init; } = new();
    public bool ExternalSystemsAccessed { get; init; }
}
