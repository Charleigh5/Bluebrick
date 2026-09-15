using Vira.Next.Contracts;

namespace Vira.Next.Engine.Hardware;

public sealed class StubCadViewerConverter : ICadViewerConverter
{
    public string ConverterId => "vira.stub-step-to-glb.v1";
    public string ConverterVersion => "1.0.0";

    public Task<ViewerConversionResult> ConvertAsync(string sourceStepPath, string outputGlbPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ViewerConversionResult { Success = false, ConverterId = ConverterId, ConverterVersion = ConverterVersion,
            ErrorCode = "NON_PRODUCTION_STUB", ErrorMessage = "Stub conversion is diagnostic only and cannot produce viewer geometry." });
    }
}

public sealed class GlbValidator
{
    public static (bool valid, string error) Validate(byte[] glbBytes)
    {
        if (glbBytes == null || glbBytes.Length < 20) return (false, "GLB too short");
        if (BitConverter.ToUInt32(glbBytes, 0) != 0x46546C67) return (false, "GLB magic mismatch");
        if (BitConverter.ToUInt32(glbBytes, 4) != 2) return (false, "GLB version must be 2");
        if (BitConverter.ToUInt32(glbBytes, 8) != glbBytes.Length) return (false, "GLB length mismatch");
        var offset = 12;
        var first = true;
        while (offset < glbBytes.Length)
        {
            if (glbBytes.Length - offset < 8) return (false, "GLB chunk header truncated");
            var length = BitConverter.ToUInt32(glbBytes, offset);
            var type = BitConverter.ToUInt32(glbBytes, offset + 4);
            if (length % 4 != 0 || length > glbBytes.Length - offset - 8) return (false, "GLB chunk length invalid");
            if (first)
            {
                if (type != 0x4E4F534A) return (false, "GLB first chunk must be JSON");
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(glbBytes.AsMemory(offset + 8, (int)length));
                    if (!doc.RootElement.TryGetProperty("asset", out var asset) || !asset.TryGetProperty("version", out var version) || version.GetString() != "2.0") return (false, "GLB asset version invalid");
                }
                catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException) { return (false, "GLB JSON invalid"); }
            }
            first = false;
            offset += 8 + (int)length;
        }
        return (true, string.Empty);
    }
}
