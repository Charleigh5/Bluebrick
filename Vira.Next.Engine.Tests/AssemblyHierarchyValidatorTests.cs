using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class AssemblyHierarchyValidatorTests
{
    [TestMethod]
    public void Validate_NestedFixture_PreservesPrimitiveSerializableHierarchyAndLimits()
    {
        var parent = PacketCadAssemblyFixtures.Component("sub-1", "SUB511200-80240000", kind: CadComponentKind.Assembly);
        var child = PacketCadAssemblyFixtures.Component(
            "part-1", "MPM511284-80241102", "sub-1", properties: new[]
            {
                PacketCadAssemblyFixtures.Property("Revision", "$PRP:\"Revision\"") with
                {
                    EvaluatedValue = "B",
                    WasResolved = true
                }
            });
        var snapshot = PacketCadAssemblyFixtures.Cad(parent, child);

        var validation = new AssemblyHierarchyValidator().Validate(snapshot);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.IsTrue(validation.IsValid);
        Assert.AreEqual(2, validation.RecordedCount);
        Assert.AreEqual(0, validation.CycleCount);
        Assert.AreEqual("$PRP:\"Revision\"", child.Properties[0].RawValue);
        Assert.AreEqual("B", child.Properties[0].EvaluatedValue);
        Assert.IsTrue(json.Contains("MPM511284-80241102", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("System.__ComObject", StringComparison.Ordinal));
        Assert.AreEqual(0, snapshot.AssemblyTraversal.MutationActions);
        Assert.IsFalse(snapshot.AssemblyTraversal.ExternalSystemsAccessed);
    }

    [TestMethod]
    public void Validate_ParentCycleDepthAndRecordLimits_ReturnExplicitFailures()
    {
        var first = PacketCadAssemblyFixtures.Component("a", "SUB-A", "b", kind: CadComponentKind.Assembly) with { Depth = 33 };
        var second = PacketCadAssemblyFixtures.Component("b", "SUB-B", "a", kind: CadComponentKind.Assembly);
        var snapshot = PacketCadAssemblyFixtures.Cad(first, second) with
        {
            AssemblyTraversal = new CadAssemblyTraversalSummary
            {
                MaxDepth = 32,
                RecordLimit = 1,
                RecordedCount = 2,
                Truncated = true,
                MutationActions = 0,
                ExternalSystemsAccessed = false
            }
        };

        var validation = new AssemblyHierarchyValidator().Validate(snapshot);

        Assert.IsFalse(validation.IsValid);
        Assert.AreEqual(1, validation.CycleCount);
        Assert.IsTrue(validation.Limitations.Any(item => item.Contains("depth", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(validation.Limitations.Any(item => item.Contains("record", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(validation.Limitations.Any(item => item.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Validate_DuplicateSnapshotIds_AreRejected()
    {
        var duplicate = PacketCadAssemblyFixtures.Component("same", "MPM511284-80241102");
        var snapshot = PacketCadAssemblyFixtures.Cad(duplicate, duplicate with { IdentifierCandidate = "MPM511285-80241103" });

        var validation = new AssemblyHierarchyValidator().Validate(snapshot);

        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.Limitations.Any(item => item.Contains("duplicate", StringComparison.OrdinalIgnoreCase)));
    }
}
