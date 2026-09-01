using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    internal interface IFeatureTreeReadAdapter
    {
        string AdapterName { get; }
        FeatureTreeSnapshot ReadFeatureTree(AuditRunRequest request, out List<AuditError> errors);
    }
}
