using System.Text.Json.Serialization;

namespace Vira.Next.Contracts;

public sealed record HardwareRecord
{
    public string HardwareRecordId { get; init; } = string.Empty;
    public string HwdNumber { get; init; } = string.Empty;
    public string McMasterPartNumber { get; init; } = string.Empty;
    public string Vendor { get; init; } = "MCM";
    public string Description { get; init; } = string.Empty;
}

public sealed record McMasterSpecification
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed record McMasterLink
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed record McMasterProductSnapshot
{
    public string PartNumber { get; init; } = string.Empty;
    public string ProductStatus { get; init; } = string.Empty;
    public string FamilyDescription { get; init; } = string.Empty;
    public string DetailDescription { get; init; } = string.Empty;
    public IReadOnlyList<McMasterSpecification> Specifications { get; init; } = Array.Empty<McMasterSpecification>();
    public IReadOnlyList<McMasterLink> Links { get; init; } = Array.Empty<McMasterLink>();
    public DateTime FetchedAtUtc { get; init; }
    public string SourceBaseUrl { get; init; } = string.Empty;
}

public sealed record VendorCadBinding
{
    public string HardwareRecordId { get; init; } = string.Empty;
    public string McMasterPartNumber { get; init; } = string.Empty;
    public string Vendor { get; init; } = "MCM";
    public string PreferredVariantKey { get; init; } = "3-D STEP";
    public string AuthoritativeCadLink { get; init; } = string.Empty;
    public string NormalizedCadUrl { get; init; } = string.Empty;
    public DateTime BoundAtUtc { get; init; }
    public string BindingReceiptId { get; init; } = string.Empty;
}

public sealed record VendorCadAsset
{
    public string AssetId { get; init; } = string.Empty;
    public string PartNumber { get; init; } = string.Empty;
    public string SourceCadLink { get; init; } = string.Empty;
    public string NormalizedCadUrl { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public long ByteLength { get; init; }
    public DateTime AcquiredAtUtc { get; init; }
    public McMasterProductSnapshot? ProductSnapshot { get; init; }
    public string ContentAddressedPath { get; init; } = string.Empty;
}

public sealed record ViewerDerivative
{
    public string SchemaVersion { get; init; } = "vira.viewer-derivative.v1";
    public string SourceSha256 { get; init; } = string.Empty;
    public string SourceAssetId { get; init; } = string.Empty;
    public string GlbSha256 { get; init; } = string.Empty;
    public long GlbByteLength { get; init; }
    public string GlbContentAddressedPath { get; init; } = string.Empty;
    public string ConverterId { get; init; } = string.Empty;
    public string ConverterVersion { get; init; } = string.Empty;
    public DateTime ConvertedAtUtc { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool IsValid { get; init; }
}

public enum VendorCadAcquisitionStatus
{
    Acquired,
    CacheHit,
    NoCadAvailable,
    ProductNotFound,
    SubscriptionRequired,
    AuthFailed,
    RateLimited,
    Failed
}

public sealed record VendorCadAcquisitionReceipt
{
    public string ReceiptId { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public string HardwareRecordId { get; init; } = string.Empty;
    public string PartNumber { get; init; } = string.Empty;
    public VendorCadAcquisitionStatus Status { get; init; }
    public VendorCadBinding? Binding { get; init; }
    public VendorCadAsset? Asset { get; init; }
    public ViewerDerivative? ViewerDerivative { get; init; }
    public bool CacheHit { get; init; }
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    public ReceiptError[] Errors { get; init; } = Array.Empty<ReceiptError>();
    public bool SafeToRetry { get; init; }
    public EngineeringMutationBoundary MutationBoundary { get; init; } = EngineeringMutationBoundary.ReadOnly;
}

public sealed record HardwareCadBindingReceipt
{
    public string ReceiptId { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; }
    public HardwareRecord HardwareRecord { get; init; } = new();
    public VendorCadAcquisitionReceipt Acquisition { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
