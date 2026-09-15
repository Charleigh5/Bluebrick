using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class CapabilityRouter
{
    private readonly CapabilityManifestService _manifest;
    private readonly CapabilityPolicyEvaluator _policy;

    public CapabilityRouter()
        : this(new CapabilityManifestService(), new CapabilityPolicyEvaluator())
    {
    }

    public CapabilityRouter(CapabilityManifestService manifest, CapabilityPolicyEvaluator policy)
    {
        _manifest = manifest;
        _policy = policy;
    }

    public CapabilityDecision Route(CapabilityRequest request)
    {
        var capability = _manifest.Find(request.CapabilityId);
        return _policy.Evaluate(request, capability);
    }
}
