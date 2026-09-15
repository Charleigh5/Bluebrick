namespace Vira.Next.Contracts;

public interface IPdmDrawingCatalog
{
    Task<IReadOnlyList<ControlledDrawingRecord>> FindCandidatesAsync(
        DrawingResolutionRequest request,
        CancellationToken cancellationToken = default);
}
