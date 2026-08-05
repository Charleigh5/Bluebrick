using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantIntegrityScannerTests
    {
        private string _tempDir;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { }
            }
        }

        [TestMethod]
        public void IntegrityScanner_ComputeSha256String_Deterministic()
        {
            var input = "Hello, World!";
            var hash1 = AssistantIntegrityScanner.ComputeSha256String(input);
            var hash2 = AssistantIntegrityScanner.ComputeSha256String(input);
            Assert.AreEqual(hash1, hash2, "Same input must produce same SHA-256 hash.");
            Assert.AreEqual(64, hash1.Length, "SHA-256 hex digest must be 64 chars.");
        }

        [TestMethod]
        public void IntegrityScanner_ComputeSha256String_DifferentInputs_DifferentHashes()
        {
            var hash1 = AssistantIntegrityScanner.ComputeSha256String("input-A");
            var hash2 = AssistantIntegrityScanner.ComputeSha256String("input-B");
            Assert.AreNotEqual(hash1, hash2, "Different inputs must produce different hashes.");
        }

        [TestMethod]
        public void IntegrityScanner_ScanFile_ReturnsHashAndSize()
        {
            var filePath = Path.Combine(_tempDir, "test.txt");
            // Write without a UTF-8 BOM so the on-disk size equals the character count.
            File.WriteAllText(filePath, "Hello, World!", new UTF8Encoding(false));

            var result = AssistantIntegrityScanner.ScanFile(filePath);
            Assert.IsFalse(result.Tampered, "Existing file should not be tampered.");
            Assert.AreEqual(13, result.SizeBytes, "File size should match.");
            Assert.IsFalse(string.IsNullOrEmpty(result.Sha256Hash), "Hash should be populated.");
        }

        [TestMethod]
        public void IntegrityScanner_ScanFile_MissingFile_ReturnsTampered()
        {
            var filePath = Path.Combine(_tempDir, "missing.txt");
            var result = AssistantIntegrityScanner.ScanFile(filePath);
            Assert.IsTrue(result.Tampered, "Missing file should be flagged as tampered.");
            Assert.IsTrue(result.Findings.Contains("file does not exist"), "Finding should mention missing file.");
        }

        [TestMethod]
        public void IntegrityScanner_ScanForSecrets_DetectsApiKey()
        {
            var filePath = Path.Combine(_tempDir, "config.json");
            File.WriteAllText(filePath, "{\"api_key\": \"sk-secret-key-12345\"}", Encoding.UTF8);

            var findings = AssistantIntegrityScanner.ScanForSecrets(filePath);
            Assert.IsTrue(findings.Count > 0, "Should detect secret in config.json.");
            Assert.IsTrue(findings[0].RedactedLine.Contains("REDACTED"), "Secret value should be redacted.");
        }

        [TestMethod]
        public void IntegrityScanner_ScanForSecrets_DetectsBearerToken()
        {
            var filePath = Path.Combine(_tempDir, "auth.txt");
            File.WriteAllText(filePath, "bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0", Encoding.UTF8);

            var findings = AssistantIntegrityScanner.ScanForSecrets(filePath);
            Assert.IsTrue(findings.Count > 0, "Should detect bearer token.");
        }

        [TestMethod]
        public void IntegrityScanner_ScanForSecrets_NoSecrets_ReturnsEmpty()
        {
            var filePath = Path.Combine(_tempDir, "clean.txt");
            File.WriteAllText(filePath, "This is a clean file with no secrets.", Encoding.UTF8);

            var findings = AssistantIntegrityScanner.ScanForSecrets(filePath);
            Assert.AreEqual(0, findings.Count, "Clean file should have no secret findings.");
        }

        [TestMethod]
        public void IntegrityScanner_ScanDirectory_ScansEligibleFiles()
        {
            var filePath = Path.Combine(_tempDir, "settings.json");
            File.WriteAllText(filePath, "{\"password\": \"hunter2\"}", Encoding.UTF8);

            var findings = AssistantIntegrityScanner.ScanDirectory(_tempDir);
            Assert.IsTrue(findings.Count > 0, "Should detect secret in scanned directory.");
        }

        [TestMethod]
        public void IntegrityScanner_RedactFindings_RedactsSecretValues()
        {
            var input = "api_key=sk-secret-key-12345";
            var redacted = AssistantIntegrityScanner.RedactFindings(input);
            Assert.IsTrue(redacted.Contains("REDACTED"), "Secret value should be redacted.");
            Assert.IsFalse(redacted.Contains("sk-secret-key-12345"), "Raw secret should not appear in redacted output.");
        }

        [TestMethod]
        public void IntegrityScanner_HasSecrets_ReturnsTrueForFindings()
        {
            var findings = new[] { new AssistantSecretScanFinding { FilePath = "test", PatternName = "api_key" } };
            Assert.IsTrue(AssistantIntegrityScanner.HasSecrets(findings), "Should report secrets present.");
        }

        [TestMethod]
        public void IntegrityScanner_HasSecrets_ReturnsFalseForEmpty()
        {
            Assert.IsFalse(AssistantIntegrityScanner.HasSecrets(Array.Empty<AssistantSecretScanFinding>()), "Should report no secrets.");
        }
    }
}