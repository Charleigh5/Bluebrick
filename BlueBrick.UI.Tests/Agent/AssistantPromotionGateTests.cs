using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantPromotionGateTests
    {
        [TestMethod]
        public void PromotionGate_AllControlsPass_AllowsPromotion()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "{ \"pluginId\": \"test\", \"pluginVersion\": \"1.0.0\", \"signature\": \"sig\", \"signerKeyId\": \"key\", \"trustState\": 1, \"checkedAtUtc\": \"2026-07-30T00:00:00Z\", \"artifactHashes\": { \"canonicalPayload\": \"abc\" } }",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = false, Findings = new List<string>() },
                secretScanner: () => Array.Empty<AssistantSecretScanFinding>(),
                providerFreshnessChecker: () => true,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => true
            );

            var result = gate.Evaluate();
            Assert.AreEqual(PromotionDecision.AllowPromotion, result.Decision, "All controls passing should allow promotion.");
            Assert.IsTrue(result.ManifestIntegrityPassed);
            Assert.IsTrue(result.SecretScanPassed);
            Assert.IsTrue(result.ProviderFreshnessPassed);
            Assert.IsTrue(result.InjectionRegressionsPassed);
            Assert.IsTrue(result.SbomValidationPassed);
            Assert.IsTrue(result.ArtifactHashesPassed);
            Assert.IsTrue(result.ReceiptChainPassed);
        }

        [TestMethod]
        public void PromotionGate_ManifestFails_BlocksPromotion()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "{ \"pluginId\": \"\", \"pluginVersion\": \"\", \"signature\": \"\", \"trustState\": 0 }",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = false, Findings = new List<string>() },
                secretScanner: () => Array.Empty<AssistantSecretScanFinding>(),
                providerFreshnessChecker: () => true,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => true
            );

            // Manifest verification runs when a manifest path is supplied to Evaluate.
            var result = gate.Evaluate("manifest.json");
            Assert.AreEqual(PromotionDecision.BlockPromotion, result.Decision, "Manifest failure should block promotion.");
            Assert.IsTrue(result.EvidenceIds.Contains("MANIFEST-MISSING") || result.EvidenceIds.Contains("MANIFEST-UNTRUSTED") || result.EvidenceIds.Contains("MANIFEST-PARSE-ERROR"));
        }

        [TestMethod]
        public void PromotionGate_IntegrityFailure_BlocksPromotion()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "{ \"pluginId\": \"test\", \"pluginVersion\": \"1.0.0\", \"signature\": \"sig\", \"signerKeyId\": \"key\", \"trustState\": 1, \"checkedAtUtc\": \"2026-07-30T00:00:00Z\", \"artifactHashes\": { \"canonicalPayload\": \"abc\" } }",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = true, Findings = new List<string> { "hash mismatch" } },
                secretScanner: () => Array.Empty<AssistantSecretScanFinding>(),
                providerFreshnessChecker: () => true,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => true
            );

            var result = gate.Evaluate();
            Assert.AreEqual(PromotionDecision.BlockPromotion, result.Decision, "Integrity failure should block promotion.");
            Assert.IsTrue(result.EvidenceIds.Contains("INTEGRITY-FAILURE"));
        }

        [TestMethod]
        public void PromotionGate_SecretScanFindings_BlocksPromotion()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "{ \"pluginId\": \"test\", \"pluginVersion\": \"1.0.0\", \"signature\": \"sig\", \"signerKeyId\": \"key\", \"trustState\": 1, \"checkedAtUtc\": \"2026-07-30T00:00:00Z\", \"artifactHashes\": { \"canonicalPayload\": \"abc\" } }",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = false, Findings = new List<string>() },
                secretScanner: () => new[] { new AssistantSecretScanFinding { FilePath = "config.json", PatternName = "api_key", Severity = "high" } },
                providerFreshnessChecker: () => true,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => true
            );

            var result = gate.Evaluate();
            Assert.AreEqual(PromotionDecision.BlockPromotion, result.Decision, "Secret scan findings should block promotion.");
            Assert.IsTrue(result.EvidenceIds.Contains("SECRET-SCAN-FINDINGS"));
        }

        [TestMethod]
        public void PromotionGate_ProviderExpired_BlocksPromotion()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "{ \"pluginId\": \"test\", \"pluginVersion\": \"1.0.0\", \"signature\": \"sig\", \"signerKeyId\": \"key\", \"trustState\": 1, \"checkedAtUtc\": \"2026-07-30T00:00:00Z\", \"artifactHashes\": { \"canonicalPayload\": \"abc\" } }",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = false, Findings = new List<string>() },
                secretScanner: () => Array.Empty<AssistantSecretScanFinding>(),
                providerFreshnessChecker: () => false,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => true
            );

            var result = gate.Evaluate();
            Assert.AreEqual(PromotionDecision.BlockPromotion, result.Decision, "Expired provider should block promotion.");
            Assert.IsTrue(result.EvidenceIds.Contains("PROVIDER-FRESHNESS"));
        }

        [TestMethod]
        public void PromotionGate_ReceiptChainBroken_BlocksPromotion()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "{ \"pluginId\": \"test\", \"pluginVersion\": \"1.0.0\", \"signature\": \"sig\", \"signerKeyId\": \"key\", \"trustState\": 1, \"checkedAtUtc\": \"2026-07-30T00:00:00Z\", \"artifactHashes\": { \"canonicalPayload\": \"abc\" } }",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = false, Findings = new List<string>() },
                secretScanner: () => Array.Empty<AssistantSecretScanFinding>(),
                providerFreshnessChecker: () => true,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => false
            );

            var result = gate.Evaluate();
            Assert.AreEqual(PromotionDecision.BlockPromotion, result.Decision, "Broken receipt chain should block promotion.");
            Assert.IsTrue(result.EvidenceIds.Contains("RECEIPT-CHAIN"));
        }

        [TestMethod]
        public void PromotionGate_NoCustomCheckers_AllowsPromotion()
        {
            var gate = new AssistantPromotionGate();
            var result = gate.Evaluate();
            Assert.AreEqual(PromotionDecision.AllowPromotion, result.Decision, "No custom checkers means all controls pass by default.");
        }

        [TestMethod]
        public void PromotionGate_ResultHasReasonsAndEvidenceIds()
        {
            var gate = new AssistantPromotionGate(
                manifestLoader: _ => "invalid-json{{{",
                integrityChecker: _ => new AssistantIntegrityScanResult { Tampered = false, Findings = new List<string>() },
                secretScanner: () => Array.Empty<AssistantSecretScanFinding>(),
                providerFreshnessChecker: () => true,
                injectionRegressionsChecker: () => true,
                sbomValidator: () => true,
                receiptChainValidator: () => true
            );

            // Manifest verification runs when a manifest path is supplied to Evaluate.
            var result = gate.Evaluate("manifest.json");
            Assert.AreEqual(PromotionDecision.BlockPromotion, result.Decision);
            Assert.IsFalse(string.IsNullOrEmpty(result.Reason), "Blocked result must have a reason.");
            Assert.IsTrue(result.EvidenceIds.Length > 0, "Blocked result must have evidence IDs.");
        }

        [TestMethod]
        public void PromotionGate_AllDecisionValuesExist()
        {
            Assert.AreEqual(PromotionDecision.AllowPromotion, PromotionDecision.AllowPromotion);
            Assert.AreEqual(PromotionDecision.AdmitCandidateWithLimits, PromotionDecision.AdmitCandidateWithLimits);
            Assert.AreEqual(PromotionDecision.BlockPromotion, PromotionDecision.BlockPromotion);
        }
    }
}