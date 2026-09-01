using System.Collections.Generic;
using BlueBrick.Audit.Contracts;
using BlueBrick.SolidWorks.Snapshots;

namespace BlueBrick.SolidWorks.Adapters
{
    public interface ISelectionReadAdapter
    {
        string AdapterName { get; }
        SelectionSnapshot ReadSelection(AuditRunRequest request, out List<AuditError> errors);
    }
}
