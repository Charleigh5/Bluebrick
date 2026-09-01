using System.Collections.Generic;

namespace BlueBrick.SolidWorks.Adapters.Internal
{
    internal interface ISwFeatureNode
    {
        string GetId();
        string GetName();
        string GetTypeName();
        string GetSuppressionState();
        string GetState();
        ISwFeatureNode GetNext();
        ISwFeatureNode GetFirstSubFeature();
    }

    internal interface ISwFeatureSource
    {
        string GetDocumentIdentityHash();
        ISwFeatureNode GetFirstFeature();
    }
}
