using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public interface IReadOnlyPdmSearchGateway
{
    Task<PdmSearchResponse> SearchAsync(
        PdmSearchQuery query,
        CancellationToken cancellationToken = default);
}
