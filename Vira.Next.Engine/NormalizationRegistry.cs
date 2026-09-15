using System.Globalization;
using System.Text.RegularExpressions;
using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class NormalizationRegistry
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Gauge = new(@"^\s*(?<gauge>\d+(?:\.\d+)?)\s*(?:GA|GAUGE)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Thickness = new(@"^\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>IN|INCH|INCHES|""|MM)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string CanonicalPropertyName(string name)
    {
        var key = new string((name ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return key switch
        {
            "PARTNUMBER" or "DOCUMENTNUMBER" or "DRAWINGNUMBER" or "DOCNUMBER" => "PartNumber",
            "REV" or "REVISION" => "Revision",
            "DESC" or "DESCRIPTION" or "DESCCONFIG" => "Description",
            "MATERIAL" or "MATERIALNUMBER" => "Material",
            "THICKNESS" or "GAUGE" => "Thickness",
            "CUSTOMER" or "CUSTOMERABBREVIATION" or "CUSTOMERABBR" => "CustomerAbbreviation",
            "DRAWNBY" or "DRAWN" => "DrawnBy",
            "CONFIG" or "CONFIGURATION" => "Configuration",
            _ => string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim()
        };
    }

    public string NormalizeText(string value) => Whitespace.Replace(value?.Trim() ?? string.Empty, " ").ToUpperInvariant();

    public string NormalizeIdentifier(string value)
    {
        var normalized = NormalizeText(value).Replace('_', '-');
        foreach (var extension in new[] { ".SLDASM", ".SLDPRT", ".SLDDRW", ".PDF" })
        {
            if (normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^extension.Length];
                break;
            }
        }

        return normalized.Replace(" ", string.Empty);
    }

    public bool IsIdentityProperty(string name) => CanonicalPropertyName(name) == "PartNumber";

    public string NormalizeHash(string value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    public string NormalizeValue(string canonicalName, string value, PropertyComparisonOptions options)
    {
        var normalized = NormalizeText(value);
        if (options.ApprovedValueAliases.TryGetValue(canonicalName, out var aliases) &&
            aliases.TryGetValue(normalized, out var approved))
        {
            return NormalizeText(approved);
        }

        return normalized;
    }

    public ThicknessNormalization NormalizeThickness(string value, PropertyComparisonOptions options)
    {
        var gauge = Gauge.Match(value ?? string.Empty);
        if (gauge.Success)
        {
            var gaugeKey = $"{gauge.Groups["gauge"].Value} GA";
            return options.ApprovedGaugeThicknessInches.TryGetValue(gaugeKey, out var inches)
                ? ThicknessNormalization.Resolved(inches, "VIRA-THICKNESS-APPROVED-GAUGE-001")
                : ThicknessNormalization.Unsupported("Gauge-to-thickness conversion requires an approved mapping.");
        }

        var numeric = Thickness.Match(value ?? string.Empty);
        if (!numeric.Success || !decimal.TryParse(numeric.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return ThicknessNormalization.Unsupported("Thickness requires an explicit numeric unit or approved gauge mapping.");
        }

        var unit = numeric.Groups["unit"].Value.ToUpperInvariant();
        var inchesValue = unit == "MM" ? parsed / 25.4m : parsed;
        return ThicknessNormalization.Resolved(inchesValue, "VIRA-THICKNESS-INCH-TOLERANCE-001");
    }
}

public sealed record ThicknessNormalization
{
    public bool IsSupported { get; init; }
    public decimal Inches { get; init; }
    public string RuleId { get; init; } = string.Empty;
    public string Limitation { get; init; } = string.Empty;

    public static ThicknessNormalization Resolved(decimal inches, string ruleId) => new()
    {
        IsSupported = true,
        Inches = inches,
        RuleId = ruleId
    };

    public static ThicknessNormalization Unsupported(string limitation) => new()
    {
        IsSupported = false,
        Limitation = limitation
    };
}
