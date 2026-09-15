using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Vira.Next.Contracts;
using Vira.Next.Engine;

namespace Vira.Next.Engine.Tests;

[TestClass]
public sealed class PacketDrawingEvidenceMapperTests
{
    [TestMethod]
    public void M01_SalesforceExactStarterPair_MapsRequestAndPreservesProvenance()
    {
        var result = Map(Salesforce(
            "sf-part", "SALESFORCE.PART_NUMBER", " ASY511002 ", "row-42576", role: "STARTER"),
            Salesforce("sf-doc", "SALESFORCE.DOCUMENT_NUMBER", "80233885", "row-42576"));

        Assert.AreEqual("42576", result.Request.OpportunityId);
        Assert.AreEqual("TE-3057", result.Request.TaskId);
        Assert.AreEqual("Walmart", result.Request.Customer);
        Assert.AreEqual("STARTER", result.Request.SourceRole);
        Assert.AreEqual(DrawingRoleAuthority.SourceDerived, result.Request.RoleAuthority);
        Assert.AreEqual(" ASY511002 ", result.Request.PartNumber);
        Assert.AreEqual("80233885", result.Request.DocumentNumber);
        Assert.AreEqual(2, result.Request.Identifiers.Count);
        Assert.AreEqual("sf-shot-1", result.Request.Identifiers[0].SourceArtifactId);
        Assert.AreEqual("sha-salesforce", result.Request.Identifiers[0].SourceArtifactSha256);
        Assert.AreEqual("row-42576", result.Request.Identifiers[0].SourceRecordId);
        Assert.AreEqual("SALESFORCE.PART_NUMBER", result.Request.Identifiers[0].SourceField);
        Assert.AreEqual("STARTER", result.Request.Identifiers[0].SourceLabel);
        Assert.AreEqual(PacketDrawingSourceType.SalesforceScreenshot, result.Request.Identifiers[0].SourceType);
        Assert.AreEqual("SCREENSHOT_OCR", result.Request.Identifiers[0].ExtractionMethod);
        Assert.IsFalse(result.ExternalSystemsAccessed);
        Assert.AreEqual(0, result.MutationActions);
    }

    [TestMethod]
    public void M02_SalesforceAdderPair_MapsExactFields()
    {
        var result = Map(Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511003", "row-adder", role: "ADDER"),
            Salesforce("sf-doc", "SALESFORCE.DOCUMENT_NUMBER", "80233886", "row-adder"));

        Assert.AreEqual("ASY511003", result.Request.PartNumber);
        Assert.AreEqual("80233886", result.Request.DocumentNumber);
        Assert.AreEqual("ADDER", result.Request.SourceRole);
    }

    [TestMethod]
    public void M03_SalesforcePartOnly_MapsPartAndLeavesDocumentBlank()
    {
        var result = Map(Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511001", "row-single"));

        Assert.AreEqual("ASY511001", result.Request.PartNumber);
        Assert.AreEqual(string.Empty, result.Request.DocumentNumber);
        Assert.AreEqual(1, result.Request.Identifiers.Count);
    }

    [TestMethod]
    public void M04_BareUnlabeledNumeric_RemainsUnclassified()
    {
        var result = Map(Salesforce("sf-bare", "", "80233885", "row-bare"));

        Assert.AreEqual(string.Empty, result.Request.DocumentNumber);
        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.AreEqual(DrawingIdentifierKind.Unclassified, result.Evidence[0].ClassifiedKind);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("unclassified", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M05_DistinctTypedPartCandidates_AreForwardedForFailClosedResolver()
    {
        var result = Map(Salesforce("sf-part-1", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1"),
            Salesforce("typed-part", "TYPED.PART_NUMBER", "ASY511003", "row-1"));

        Assert.AreEqual(string.Empty, result.Request.PartNumber);
        Assert.AreEqual(2, result.Request.Identifiers.Count(item => item.Kind == DrawingIdentifierKind.PartNumber));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Multiple distinct PartNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M06_EquivalentDuplicates_DoNotRepeatRequestCandidatesButPreserveEvidence()
    {
        var result = Map(Salesforce("sf-part-1", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1"),
            Salesforce("sf-part-2", "SALESFORCE.PART_NUMBER", " asy511002.sldasm ", "row-1"));

        Assert.AreEqual(1, result.Request.Identifiers.Count);
        Assert.AreEqual(2, result.Evidence.Count);
        Assert.IsTrue(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsFalse(result.Evidence[1].IncludedInResolutionRequest);
        Assert.IsTrue(result.Warnings.Any(item => item.Contains("Equivalent duplicate", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M07_PdfTitleBlockPair_PreservesPageRegionAndNativeTextMethod()
    {
        var result = Map(Pdf("pdf-part", "TITLE_BLOCK.PART_NO", "ASY511003", 3, "x=10,y=20,w=30,h=40", "pdf-row-3"),
            Pdf("pdf-doc", "TITLE_BLOCK.DOC_NO", "80233886", 3, "x=50,y=20,w=30,h=40", "pdf-row-3"));

        Assert.AreEqual("ASY511003", result.Request.PartNumber);
        Assert.AreEqual("80233886", result.Request.DocumentNumber);
        Assert.AreEqual(3, result.Request.Identifiers[0].SourcePageNumber);
        Assert.AreEqual("x=10,y=20,w=30,h=40", result.Request.Identifiers[0].SourceRegion);
        Assert.AreEqual("NATIVE_TEXT", result.Request.Identifiers[0].ExtractionMethod);
    }

    [TestMethod]
    public void M08_PdfPartOnly_MapsPartCandidate()
    {
        var result = Map(Pdf("pdf-part", "TITLE_BLOCK.PART_NO", "ASY511001", 3, "part-region", "pdf-row-3"));

        Assert.AreEqual("ASY511001", result.Request.PartNumber);
        Assert.AreEqual(string.Empty, result.Request.DocumentNumber);
    }

    [TestMethod]
    public void M09_PdfDocumentOnly_MapsDocumentCandidate()
    {
        var result = Map(Pdf("pdf-doc", "TITLE_BLOCK.DOC_NO", "80233881", 3, "doc-region", "pdf-row-3"));

        Assert.AreEqual(string.Empty, result.Request.PartNumber);
        Assert.AreEqual("80233881", result.Request.DocumentNumber);
    }

    [TestMethod]
    public void M10_ConflictingDocumentValues_AreNotPromotedToExplicitDocument()
    {
        var result = Map(Pdf("pdf-doc-1", "TITLE_BLOCK.DOC_NO", "80233885", 3, "doc-region-a", "pdf-row-3"),
            Pdf("pdf-doc-2", "TITLE_BLOCK.DOC_NO", "80233886", 4, "doc-region-b", "pdf-row-4"));

        Assert.AreEqual(string.Empty, result.Request.DocumentNumber);
        Assert.AreEqual(2, result.Request.Identifiers.Count(item => item.Kind == DrawingIdentifierKind.DocumentNumber));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("Multiple distinct DocumentNumber", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M11_MultipleSameKindDistinctValues_RemainVisibleForResolver()
    {
        var result = Map(Pdf("pdf-part-1", "TITLE_BLOCK.PART_NO", "ASY511002", 3, "a", "pdf-row-3"),
            Pdf("pdf-part-2", "TITLE_BLOCK.PART_NO", "ASY511003", 4, "b", "pdf-row-4"));

        Assert.AreEqual(string.Empty, result.Request.PartNumber);
        CollectionAssert.AreEquivalent(
            new[] { "ASY511002", "ASY511003" },
            result.Request.Identifiers.Select(item => item.RawValue).ToArray());
    }

    [TestMethod]
    public void M12_SourceRoleAuthority_RemainsSourceDerived()
    {
        var result = Map(Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1", role: "STARTER"));

        Assert.AreEqual("STARTER", result.Request.SourceRole);
        Assert.AreEqual(DrawingRoleAuthority.SourceDerived, result.Request.RoleAuthority);
    }

    [TestMethod]
    public void M15_ControlledInputRoleAuthority_IsForcedToSourceDerivedWithDiagnostic()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            SourceRole = "STARTER",
            RoleAuthority = DrawingRoleAuthority.ControlledEvidence,
            Identifiers = new[] { Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1") }
        });

        Assert.AreEqual(DrawingRoleAuthority.SourceDerived, result.Request.RoleAuthority);
        Assert.IsTrue(result.Warnings.Any(item => item.Contains("ControlledEvidence", StringComparison.Ordinal)));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("controlled authority", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M16_HintedBareNumbers_RemainUnclassifiedAndExcluded()
    {
        var result = Map(
            SalesforceWithHint("hinted-document", "80233885", DrawingIdentifierKind.DocumentNumber),
            SalesforceWithHint("hinted-part", "ASY511002", DrawingIdentifierKind.PartNumber));

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsTrue(result.Evidence.All(item => item.ClassifiedKind == DrawingIdentifierKind.Unclassified));
        Assert.IsTrue(result.Evidence.All(item => !item.IncludedInResolutionRequest));
    }

    [TestMethod]
    public void M17_NullIdentifierCollection_IsHandledWithLimitation()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            Identifiers = null!
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("null", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M18_BlankEvidenceId_IsExcludedFromResolutionRequest()
    {
        var result = Map(Salesforce("", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1"));

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("blank EvidenceId", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M19_DuplicateEvidenceIds_AreExcludedFromResolutionRequest()
    {
        var result = Map(
            Salesforce("duplicate", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1"),
            Salesforce("duplicate", "SALESFORCE.PART_NUMBER", "ASY511003", "row-2"));

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsTrue(result.Evidence.All(item => !item.IncludedInResolutionRequest));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("EvidenceId 'duplicate' is duplicated", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M20_ArtifactManifestFillsMissingPerEvidenceHash()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            Artifacts = new[]
            {
                new PacketDrawingArtifact
                {
                    ArtifactId = "sf-shot-1",
                    SourceType = PacketDrawingSourceType.SalesforceScreenshot,
                    Sha256 = "sha-from-manifest"
                }
            },
            Identifiers = new[]
            {
                Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1") with
                {
                    SourceArtifactSha256 = string.Empty
                }
            }
        });

        Assert.AreEqual("sha-from-manifest", result.Evidence[0].Evidence.SourceArtifactSha256);
        Assert.AreEqual("sha-from-manifest", result.Request.Identifiers[0].SourceArtifactSha256);
        Assert.IsTrue(result.Evidence[0].IncludedInResolutionRequest);
    }

    [TestMethod]
    public void M21_ArtifactManifestHashMismatch_ExcludesEvidence()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            Artifacts = new[]
            {
                new PacketDrawingArtifact
                {
                    ArtifactId = "sf-shot-1",
                    SourceType = PacketDrawingSourceType.SalesforceScreenshot,
                    Sha256 = "sha-from-manifest"
                }
            },
            Identifiers = new[]
            {
                Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1") with
                {
                    SourceArtifactSha256 = "sha-from-evidence"
                }
            }
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("does not match", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M22_MissingArtifactManifestReference_ExcludesEvidenceWhenManifestIsSupplied()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            Artifacts = new[]
            {
                new PacketDrawingArtifact
                {
                    ArtifactId = "other-artifact",
                    SourceType = PacketDrawingSourceType.SalesforceScreenshot,
                    Sha256 = "sha-other"
                }
            },
            Identifiers = new[]
            {
                Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1")
            }
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("was not found in the supplied artifact manifest", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M23_UnrelatedSourceField_DoesNotUseDocumentRoleLabel()
    {
        var result = Map(new PacketDrawingIdentifierEvidence
        {
            EvidenceId = "description-document-label",
            SourceType = PacketDrawingSourceType.SalesforceScreenshot,
            SourceField = "DESCRIPTION",
            SourceLabel = "DOCUMENT NUMBER",
            KindHint = DrawingIdentifierKind.DocumentNumber,
            RawValue = "80233885"
        });

        Assert.AreEqual(DrawingIdentifierKind.Unclassified, result.Evidence[0].ClassifiedKind);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.AreEqual(0, result.Request.Identifiers.Count);
    }

    [TestMethod]
    public void M24_UnrelatedSourceField_DoesNotUsePartRoleLabel()
    {
        var result = Map(new PacketDrawingIdentifierEvidence
        {
            EvidenceId = "description-part-label",
            SourceType = PacketDrawingSourceType.SalesforceScreenshot,
            SourceField = "DESCRIPTION",
            SourceLabel = "PART NO",
            KindHint = DrawingIdentifierKind.PartNumber,
            RawValue = "ASY511002"
        });

        Assert.AreEqual(DrawingIdentifierKind.Unclassified, result.Evidence[0].ClassifiedKind);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.AreEqual(0, result.Request.Identifiers.Count);
    }

    [TestMethod]
    public void M25_BareGenericLabelsAndHints_RemainUnclassified()
    {
        var result = Map(
            new PacketDrawingIdentifierEvidence
            {
                EvidenceId = "bare-part",
                SourceType = PacketDrawingSourceType.PdfPacket,
                SourceLabel = "PART",
                KindHint = DrawingIdentifierKind.PartNumber,
                RawValue = "ASY511002"
            },
            new PacketDrawingIdentifierEvidence
            {
                EvidenceId = "bare-doc",
                SourceType = PacketDrawingSourceType.PdfPacket,
                SourceLabel = "DOC",
                KindHint = DrawingIdentifierKind.DocumentNumber,
                RawValue = "80233885"
            },
            new PacketDrawingIdentifierEvidence
            {
                EvidenceId = "bare-document",
                SourceType = PacketDrawingSourceType.PdfPacket,
                SourceLabel = "DOCUMENT",
                KindHint = DrawingIdentifierKind.DocumentNumber,
                RawValue = "80233886"
            },
            new PacketDrawingIdentifierEvidence
            {
                EvidenceId = "bare-drawing",
                SourceType = PacketDrawingSourceType.PdfPacket,
                SourceLabel = "DRAWING",
                KindHint = DrawingIdentifierKind.DocumentNumber,
                RawValue = "80233887"
            });

        Assert.IsTrue(result.Evidence.All(item => item.ClassifiedKind == DrawingIdentifierKind.Unclassified));
        Assert.IsTrue(result.Evidence.All(item => !item.IncludedInResolutionRequest));
        Assert.AreEqual(0, result.Request.Identifiers.Count);
    }

    [TestMethod]
    public void M26_SupportedSourceField_WinsAgainstConflictingLabelAndHint()
    {
        var result = Map(new PacketDrawingIdentifierEvidence
        {
            EvidenceId = "field-part-label-document",
            SourceType = PacketDrawingSourceType.SalesforceScreenshot,
            SourceField = "SALESFORCE.PART_NUMBER",
            SourceLabel = "DOCUMENT NUMBER",
            KindHint = DrawingIdentifierKind.DocumentNumber,
            RawValue = "ASY511002"
        });

        Assert.AreEqual(DrawingIdentifierKind.PartNumber, result.Evidence[0].ClassifiedKind);
        Assert.AreEqual(DrawingIdentifierKind.PartNumber, result.Request.Identifiers[0].Kind);
        Assert.AreEqual("ASY511002", result.Request.PartNumber);
        Assert.AreEqual(string.Empty, result.Request.DocumentNumber);
    }

    [TestMethod]
    public void M27_InvalidSourceType_IsExcludedWithDeterministicLimitation()
    {
        var result = Map(new PacketDrawingIdentifierEvidence
        {
            EvidenceId = "invalid-source-type",
            SourceType = (PacketDrawingSourceType)99,
            SourceField = "SALESFORCE.PART_NUMBER",
            RawValue = "ASY511002"
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("unsupported source type", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M28_BlankSourceField_UsesSupportedLabelBeforeConflictingHint()
    {
        var result = Map(new PacketDrawingIdentifierEvidence
        {
            EvidenceId = "label-document-hint-part",
            SourceType = PacketDrawingSourceType.SalesforceScreenshot,
            SourceLabel = "DOCUMENT_NUMBER",
            KindHint = DrawingIdentifierKind.PartNumber,
            RawValue = "80233885"
        });

        Assert.AreEqual(DrawingIdentifierKind.DocumentNumber, result.Evidence[0].ClassifiedKind);
        Assert.AreEqual(DrawingIdentifierKind.DocumentNumber, result.Request.Identifiers[0].Kind);
        Assert.AreEqual("80233885", result.Request.DocumentNumber);
        Assert.AreEqual(string.Empty, result.Request.PartNumber);
    }

    [TestMethod]
    public void M29_NullEvidenceItem_IsExcludedWithLimitation()
    {
        var result = Map(new PacketDrawingIdentifierEvidence[] { null! });

        Assert.AreEqual(1, result.Evidence.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("evidence at index 0 was null", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M30_NullSourceFieldAndLabel_AreHandledAsEmpty()
    {
        var result = Map(new PacketDrawingIdentifierEvidence
        {
            EvidenceId = "null-field-label",
            SourceType = PacketDrawingSourceType.SalesforceScreenshot,
            SourceField = null!,
            SourceLabel = null!,
            RawValue = "ASY511002"
        });

        Assert.AreEqual(DrawingIdentifierKind.Unclassified, result.Evidence[0].ClassifiedKind);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.AreEqual(0, result.Request.Identifiers.Count);
    }

    [TestMethod]
    public void M31_MixedValidUnsupportedRejectedWhitespaceAndUnknownEvidence_FailsClosedPerItem()
    {
        var result = Map(
            Salesforce("valid", "SALESFORCE.PART_NUMBER", "ASY511002", "row-valid"),
            Salesforce("unsupported", "SALESFORCE.DOCUMENT_NUMBER", "80233885", "row-unsupported") with
            {
                Status = PacketDrawingEvidenceStatus.Unsupported
            },
            Salesforce("rejected", "SALESFORCE.PART_NUMBER", "ASY511003", "row-rejected") with
            {
                Status = PacketDrawingEvidenceStatus.Rejected
            },
            Salesforce("whitespace", "SALESFORCE.DOCUMENT_NUMBER", "   ", "row-whitespace"),
            Salesforce("unknown-field", "UNRELATED_FIELD", "80233886", "row-unknown"));

        Assert.AreEqual(1, result.Request.Identifiers.Count);
        Assert.AreEqual("valid", result.Request.Identifiers[0].EvidenceId);
        Assert.AreEqual("ASY511002", result.Request.PartNumber);
        Assert.AreEqual(string.Empty, result.Request.DocumentNumber);
        Assert.IsTrue(result.Warnings.Any(item => item.Contains("empty raw value", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("status Unsupported", StringComparison.Ordinal)));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("status Rejected", StringComparison.Ordinal)));
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("remains unclassified", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void M32_ArtifactSourceTypeMismatch_SalesforceArtifactPdfEvidence_IsExcluded()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            Artifacts = new[]
            {
                new PacketDrawingArtifact
                {
                    ArtifactId = "shared-artifact",
                    SourceType = PacketDrawingSourceType.SalesforceScreenshot,
                    Sha256 = "shared-hash"
                }
            },
            Identifiers = new[]
            {
                Pdf("pdf-part", "TITLE_BLOCK.PART_NO", "ASY511002", 3, "part-region", "pdf-row") with
                {
                    SourceArtifactId = "shared-artifact",
                    SourceArtifactSha256 = "shared-hash"
                }
            }
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("source type does not match", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M33_ArtifactSourceTypeMismatch_PdfArtifactSalesforceEvidence_IsExcluded()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            Artifacts = new[]
            {
                new PacketDrawingArtifact
                {
                    ArtifactId = "shared-artifact",
                    SourceType = PacketDrawingSourceType.PdfPacket,
                    Sha256 = "shared-hash"
                }
            },
            Identifiers = new[]
            {
                Salesforce("sf-part", "SALESFORCE.PART_NUMBER", "ASY511002", "sf-row") with
                {
                    SourceArtifactId = "shared-artifact",
                    SourceArtifactSha256 = "shared-hash"
                }
            }
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("source type does not match", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M34_InvalidEvidenceStatus_IsExcludedWithDeterministicLimitation()
    {
        var result = Map(Salesforce("invalid-status", "SALESFORCE.PART_NUMBER", "ASY511002", "row-status") with
        {
            Status = (PacketDrawingEvidenceStatus)99
        });

        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsFalse(result.Evidence[0].IncludedInResolutionRequest);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("unsupported evidence status value", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void M35_EquivalentDuplicatePermutation_ProducesIdenticalCanonicalOutput()
    {
        var salesforce = Salesforce("sf-duplicate", "SALESFORCE.PART_NUMBER", "ASY511002", "row-1") with
        {
            SourceArtifactId = "sf-artifact",
            SourceArtifactSha256 = "sf-hash",
            Region = "sf-region",
            SourceLabel = "ROLE:STARTER",
            Confidence = 0.91
        };
        var pdf = Pdf("pdf-duplicate", "TITLE_BLOCK.PART_NO", " ASY511002.SLDPRT ", 3, "pdf-region", "row-1") with
        {
            SourceArtifactId = "pdf-artifact",
            SourceArtifactSha256 = "pdf-hash",
            SourceLabel = "TITLE BLOCK",
            Confidence = 0.98
        };

        var firstOrder = MapWithRole("STARTER", salesforce, pdf);
        var secondOrder = MapWithRole("STARTER", pdf, salesforce);

        Assert.AreEqual(JsonSerializer.Serialize(firstOrder), JsonSerializer.Serialize(secondOrder));
        Assert.AreEqual(1, firstOrder.Request.Identifiers.Count);
        Assert.AreEqual("pdf-duplicate", firstOrder.Request.Identifiers[0].EvidenceId);
        Assert.AreEqual(1, firstOrder.Evidence.Count(item => item.IncludedInResolutionRequest));
        Assert.AreEqual(2, firstOrder.Evidence.Count);
    }

    [TestMethod]
    public void M36_ContextualLabelOnlyFormsRemainUnclassifiedAndHintsCannotOverride()
    {
        var result = MapWithRole("STARTER",
            new PacketDrawingIdentifierEvidence
            {
                EvidenceId = "contextual-document-label",
                SourceType = PacketDrawingSourceType.SalesforceScreenshot,
                SourceLabel = "ROLE: DOCUMENT NUMBER",
                KindHint = DrawingIdentifierKind.DocumentNumber,
                RawValue = "80233885"
            },
            new PacketDrawingIdentifierEvidence
            {
                EvidenceId = "contextual-part-label",
                SourceType = PacketDrawingSourceType.PdfPacket,
                SourceLabel = "DESCRIPTION: PART NO",
                KindHint = DrawingIdentifierKind.PartNumber,
                RawValue = "ASY511002"
            });

        Assert.IsTrue(result.Evidence.All(item => item.ClassifiedKind == DrawingIdentifierKind.Unclassified));
        Assert.IsTrue(result.Evidence.All(item => !item.IncludedInResolutionRequest));
        Assert.AreEqual(0, result.Request.Identifiers.Count);
    }

    [TestMethod]
    public void M13_NullEvidenceSet_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PacketDrawingEvidenceMapper().Map(null!));
    }

    [TestMethod]
    public void M14_EmptyEvidenceSet_ReturnsValidRequestWithLimitation()
    {
        var result = new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            OpportunityId = "42576",
            TaskId = "TE-3057"
        });

        Assert.AreEqual("42576", result.Request.OpportunityId);
        Assert.AreEqual(0, result.Request.Identifiers.Count);
        Assert.IsTrue(result.Limitations.Any(item => item.Contains("No packet drawing identifier evidence", StringComparison.Ordinal)));
    }

    private static DrawingResolutionMappingResult Map(params PacketDrawingIdentifierEvidence[] evidence) =>
        MapWithRole(
            evidence.FirstOrDefault(item => item is not null && !string.IsNullOrWhiteSpace(item.SourceLabel))?.SourceLabel ?? "STARTER",
            evidence);

    private static DrawingResolutionMappingResult MapWithRole(
        string role,
        params PacketDrawingIdentifierEvidence[] evidence) =>
        new PacketDrawingEvidenceMapper().Map(new PacketDrawingEvidenceSet
        {
            OpportunityId = "42576",
            TaskId = "TE-3057",
            Customer = "Walmart",
            Project = "DELI BAKERY RACK",
            SourceRole = role,
            RoleAuthority = DrawingRoleAuthority.SourceDerived,
            Identifiers = evidence
        });

    private static PacketDrawingIdentifierEvidence Salesforce(
        string id,
        string field,
        string value,
        string recordId,
        string role = "") => new()
    {
        EvidenceId = id,
        SourceType = PacketDrawingSourceType.SalesforceScreenshot,
        SourceArtifactId = "sf-shot-1",
        SourceArtifactSha256 = "sha-salesforce",
        SourceRecordId = recordId,
        ExtractionMethod = "SCREENSHOT_OCR",
        SourceField = field,
        SourceLabel = role,
        RawValue = value,
        Status = PacketDrawingEvidenceStatus.Candidate,
        Confidence = 0.91
    };

    private static PacketDrawingIdentifierEvidence SalesforceWithHint(
        string id,
        string value,
        DrawingIdentifierKind hint) => Salesforce(id, string.Empty, value, "row-hinted") with
        {
            KindHint = hint
        };

    private static PacketDrawingIdentifierEvidence Pdf(
        string id,
        string field,
        string value,
        int page,
        string region,
        string recordId) => new()
    {
        EvidenceId = id,
        SourceType = PacketDrawingSourceType.PdfPacket,
        SourceArtifactId = "pdf-packet-1",
        SourceArtifactSha256 = "sha-pdf",
        SourceRecordId = recordId,
        PageNumber = page,
        Region = region,
        ExtractionMethod = "NATIVE_TEXT",
        SourceField = field,
        RawValue = value,
        Status = PacketDrawingEvidenceStatus.Observed,
        Confidence = 0.98
    };
}
