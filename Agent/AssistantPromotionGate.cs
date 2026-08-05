using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BlueBrick.Agent
{
    public enum PromotionDecision
    {
        AllowPromotion,
        AdmitCandidateWithLimits,
        BlockPromotion
    }

    public sealed class AssistantPromotionGateResult
    {
        public PromotionDecision Decision { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string[] EvidenceIds { get; set; } = Array.Empty<string>();
        public bool ManifestIntegrityPassed { get; set; }
        public bool SecretScanPassed { get; set; }
        public bool ProviderFreshnessPassed { get; set; }
        public bool InjectionRegressionsPassed { get; set; }
        public bool SbomValidationPassed { get; set; }
        public bool ArtifactHashesPassed { get; set; }
        public bool ReceiptChainPassed { get; set; }

        public static AssistantPromotionGateResult Allow(string reason = "all controls passed")
        {
            return new AssistantPromotionGateResult
            {
                Decision = PromotionDecision.AllowPromotion,
                Reason = reason,
                ManifestIntegrityPassed = true,
                SecretScanPassed = true,
                ProviderFreshnessPassed = true,
                InjectionRegressionsPassed = true,
                SbomValidationPassed = true,
                ArtifactHashesPassed = true,
                ReceiptChainPassed = true
            };
        }

        public static AssistantPromotionGateResult AdmitWithLimits(string reason, params string[] evidenceIds)
        {
            return new AssistantPromotionGateResult
            {
                Decision = PromotionDecision.AdmitCandidateWithLimits,
                Reason = reason,
                EvidenceIds = evidenceIds ?? Array.Empty<string>()
            };
        }

        public static AssistantPromotionGateResult Block(string reason, params string[] evidenceIds)
        {
            return new AssistantPromotionGateResult
            {
                Decision = PromotionDecision.BlockPromotion,
                Reason = reason,
                EvidenceIds = evidenceIds ?? Array.Empty<string>()
            };
        }
    }

    public sealed class AssistantPromotionGate
    {
        private readonly Func<string, string> _manifestLoader;
        private readonly Func<string, AssistantIntegrityScanResult> _integrityChecker;
        private readonly Func<IReadOnlyList<AssistantSecretScanFinding>> _secretScanner;
        private readonly Func<bool> _providerFreshnessChecker;
        private readonly Func<bool> _injectionRegressionsChecker;
        private readonly Func<bool> _sbomValidator;
        private readonly Func<bool> _receiptChainValidator;

        public AssistantPromotionGate(
            Func<string, string> manifestLoader = null,
            Func<string, AssistantIntegrityScanResult> integrityChecker = null,
            Func<IReadOnlyList<AssistantSecretScanFinding>> secretScanner = null,
            Func<bool> providerFreshnessChecker = null,
            Func<bool> injectionRegressionsChecker = null,
            Func<bool> sbomValidator = null,
            Func<bool> receiptChainValidator = null)
        {
            _manifestLoader = manifestLoader;
            _integrityChecker = integrityChecker;
            _secretScanner = secretScanner;
            _providerFreshnessChecker = providerFreshnessChecker;
            _injectionRegressionsChecker = injectionRegressionsChecker;
            _sbomValidator = sbomValidator;
            _receiptChainValidator = receiptChainValidator;
        }

        public AssistantPromotionGateResult Evaluate(string manifestPath = null)
        {
            var evidenceIds = new List<string>();

            var manifestOk = true;
            if (_manifestLoader != null && !string.IsNullOrEmpty(manifestPath))
            {
                try
                {
                    var json = _manifestLoader(manifestPath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var manifest = JsonConvert.DeserializeObject<AssistantManifest>(json);
                        var (valid, reason, state) = AssistantManifestVerifier.Verify(manifest);
                        if (!valid)
                        {
                            manifestOk = false;
                            evidenceIds.Add("MANIFEST-" + state.ToString().ToUpper());
                        }
                    }
                }
                catch
                {
                    manifestOk = false;
                    evidenceIds.Add("MANIFEST-PARSE-ERROR");
                }
            }

            var integrityOk = _integrityChecker == null;
            if (!integrityOk)
            {
                try
                {
                    var result = _integrityChecker(manifestPath ?? "");
                    integrityOk = !result.Tampered && result.Findings.Count == 0;
                    if (!integrityOk) evidenceIds.Add("INTEGRITY-FAILURE");
                }
                catch
                {
                    integrityOk = false;
                    evidenceIds.Add("INTEGRITY-ERROR");
                }
            }

            var secretOk = _secretScanner == null;
            if (!secretOk)
            {
                try
                {
                    var findings = _secretScanner();
                    secretOk = findings == null || findings.Count == 0;
                    if (!secretOk) evidenceIds.Add("SECRET-SCAN-FINDINGS");
                }
                catch
                {
                    secretOk = false;
                    evidenceIds.Add("SECRET-SCAN-ERROR");
                }
            }

            var providerOk = _providerFreshnessChecker == null || _providerFreshnessChecker();
            if (!providerOk) evidenceIds.Add("PROVIDER-FRESHNESS");

            var injectionOk = _injectionRegressionsChecker == null || _injectionRegressionsChecker();
            if (!injectionOk) evidenceIds.Add("INJECTION-REGRESSION");

            var sbomOk = _sbomValidator == null || _sbomValidator();
            if (!sbomOk) evidenceIds.Add("SBOM-VALIDATION");

            var receiptOk = _receiptChainValidator == null || _receiptChainValidator();
            if (!receiptOk) evidenceIds.Add("RECEIPT-CHAIN");

            if (!manifestOk || !integrityOk || !secretOk || !providerOk || !injectionOk || !sbomOk || !receiptOk)
            {
                return AssistantPromotionGateResult.Block(
                    "Promotion blocked: one or more mandatory controls failed.",
                    evidenceIds.ToArray());
            }

            return AssistantPromotionGateResult.Allow("All mandatory controls passed.");
        }
    }
}
