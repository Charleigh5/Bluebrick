using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class FakeSolidWorksHost
{
    public FakeHostSnapshot CaptureSnapshot()
    {
        return new FakeHostSnapshot
        {
            ActiveDocument = new FakeHostDocument
            {
                Title = "FAKE-BLUEBRICK-PART-1001",
                DocumentType = "part",
                PathHash = "sha256:local-fixture-0001",
                IsDirty = false,
                IsReadOnly = true,
                CustomProperties = new Dictionary<string, string>
                {
                    ["Material"] = "fixture-steel",
                    ["Finish"] = "fixture-powder-coat",
                    ["Source"] = "Vira.Next.Engine fake host"
                }
            },
            ExternalSystemsAccessed = false
        };
    }
}
