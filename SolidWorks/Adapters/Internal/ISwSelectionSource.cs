using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Adapters.Internal
{
    internal interface ISwSelectionSource
    {
        string GetDocumentIdentityHash();
        int GetSelectedObjectCount2(int mark);
        int GetSelectedObjectType3(int index, int mark);
        string GetSafeNameForIndex(int index, int mark);
        int GetSelectionMark(int index, int mark);
    }
}
