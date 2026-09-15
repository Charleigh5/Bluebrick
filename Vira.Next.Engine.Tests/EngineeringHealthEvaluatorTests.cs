using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class EngineeringHealthEvaluatorTests
{
    [TestMethod]
    public void Evaluate_MissingRequiredProperty_ProducesPreviewOnlyFinding()
    {
        var report = new EngineeringHealthEvaluator().Evaluate(
            new FakeHostDocument
            {
                Title = "fixture",
                DocumentType = "part",
                IsReadOnly = true
            },
            new[] { new RequiredPropertyRule("Material", "Approved material classification is required.") });

        Assert.AreEqual(1, report.WarningCount);
        Assert.AreEqual(1, report.UnknownCount);
        Assert.AreEqual("Material", report.Findings.Single().PropertyName);
        Assert.AreEqual(EngineeringActionBoundary.PreviewOnly, report.Findings.Single().ActionBoundary);
    }
}
