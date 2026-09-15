using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class ChainedCadViewerConverter : ICadViewerConverter
{
    private readonly IReadOnlyList<ICadViewerConverter> _chain;

    public ChainedCadViewerConverter(IEnumerable<ICadViewerConverter>? chain = null)
    {
        _chain = chain?.ToArray() ?? new ICadViewerConverter[] { new OcctCadViewerConverter(), new StubCadViewerConverter() };
    }

    public string ConverterId => "vira.chained-step-to-glb.v1";
    public string ConverterVersion => string.Join("+", _chain.Select(c => $"{c.ConverterId}@{c.ConverterVersion}"));

    public async Task<ViewerConversionResult> ConvertAsync(string sourceStepPath, string outputGlbPath, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        foreach (var converter in _chain)
        {
            var result = await converter.ConvertAsync(sourceStepPath, outputGlbPath, cancellationToken);
            if (result.Success) return result with { Warnings = warnings.Concat(result.Warnings).ToArray() };
            warnings.Add($"{converter.ConverterId}@{converter.ConverterVersion}: {result.ErrorCode} {result.ErrorMessage}");
            try { if (File.Exists(outputGlbPath)) File.Delete(outputGlbPath); } catch { }
        }
        return new ViewerConversionResult { Success = false, ConverterId = ConverterId, ConverterVersion = ConverterVersion, ErrorCode = "ALL_CONVERTERS_FAILED", ErrorMessage = string.Join(" | ", warnings), Warnings = warnings };
    }
}
