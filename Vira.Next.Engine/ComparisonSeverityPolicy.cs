using Vira.Next.Contracts;

namespace Vira.Next.Engine;

public sealed class ComparisonSeverityPolicy
{
    public PacketCadSeverity Classify(string canonicalName, PacketCadComparisonStatus status, bool authoritative)
    {
        if (!authoritative)
        {
            return PacketCadSeverity.Info;
        }

        if (status == PacketCadComparisonStatus.Conflict &&
            (canonicalName == "Revision" || canonicalName == "PartNumber"))
        {
            return PacketCadSeverity.High;
        }

        return status is PacketCadComparisonStatus.Conflict or
            PacketCadComparisonStatus.MissingInCad or
            PacketCadComparisonStatus.MissingInPdf or
            PacketCadComparisonStatus.Duplicate or
            PacketCadComparisonStatus.QuantityConflict or
            PacketCadComparisonStatus.HierarchyConflict or
            PacketCadComparisonStatus.ConfigurationGap
            ? PacketCadSeverity.Warning
            : PacketCadSeverity.Info;
    }
}
