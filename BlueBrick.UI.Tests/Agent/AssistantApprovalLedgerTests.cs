using System;
using System.IO;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantApprovalLedgerTests
    {
        [TestMethod]
        public void AssistantApprovalLedger_Append_Tail_RoundTrips_Entries()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "bb-approval-" + Guid.NewGuid().ToString("N"));
            try
            {
                var ledger = new AssistantApprovalLedger(Path.Combine(tempRoot, "approvals", "approval-ledger.jsonl"));
                ledger.Append(new AssistantApprovalLedgerEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    TraceId = "trace-1",
                    SessionId = "session-1",
                    ApprovalId = "approval-1",
                    CapabilityId = "file.save_as",
                    ArgumentDigest = "digest-1",
                    Environment = "lab",
                    Event = AssistantApprovalLedgerEntry.Requested,
                    Outcome = "pending"
                });
                ledger.Append(new AssistantApprovalLedgerEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    TraceId = "trace-2",
                    SessionId = "session-1",
                    CapabilityId = "file.save_as",
                    Environment = "lab",
                    Event = AssistantApprovalLedgerEntry.Issued,
                    Outcome = "granted"
                });
                ledger.Append(new AssistantApprovalLedgerEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    TraceId = "trace-3",
                    SessionId = "session-2",
                    CapabilityId = "assembly.insert_component",
                    Environment = "production",
                    Event = AssistantApprovalLedgerEntry.Denied,
                    Outcome = "denied"
                });

                var tail = ledger.Tail(3);
                Assert.AreEqual(3, tail.Count);
                Assert.AreEqual("trace-1", tail[0].TraceId);
                Assert.AreEqual("trace-2", tail[1].TraceId);
                Assert.AreEqual("trace-3", tail[2].TraceId);
                Assert.AreEqual("approval-1", tail[0].ApprovalId);
                Assert.AreEqual("digest-1", tail[0].ArgumentDigest);
                Assert.IsNull(tail[1].ApprovalId);
                Assert.IsNull(tail[1].ArgumentDigest);
                Assert.AreEqual("file.save_as", tail[1].CapabilityId);
                Assert.AreEqual("session-1", tail[1].SessionId);
                Assert.AreEqual("lab", tail[1].Environment);
                Assert.AreEqual("issued", tail[1].Event);
                Assert.AreEqual("granted", tail[1].Outcome);
                Assert.AreEqual(AssistantApprovalLedgerEntry.Denied, tail[2].Event);
                Assert.AreEqual("session-2", tail[2].SessionId);
                Assert.AreEqual("production", tail[2].Environment);
            }
            finally
            {
                TryDelete(tempRoot);
            }
        }

        [TestMethod]
        public void AssistantApprovalLedger_Persists_Jsonl_To_Explicit_Path()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "bb-approval-" + Guid.NewGuid().ToString("N"));
            var ledgerPath = Path.Combine(tempRoot, "approvals", "approval-ledger.jsonl");
            try
            {
                var ledger = new AssistantApprovalLedger(ledgerPath);
                ledger.Append(new AssistantApprovalLedgerEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    TraceId = "trace-1",
                    SessionId = "session-1",
                    ApprovalId = "approval-1",
                    CapabilityId = "file.save_as",
                    ArgumentDigest = "digest-1",
                    Environment = "lab",
                    Event = AssistantApprovalLedgerEntry.Requested,
                    Outcome = "pending"
                });
                ledger.Append(new AssistantApprovalLedgerEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    TraceId = "trace-2",
                    SessionId = "session-1",
                    CapabilityId = "file.save_as",
                    Environment = "lab",
                    Event = AssistantApprovalLedgerEntry.Expired,
                    Outcome = "expired"
                });

                Assert.IsTrue(File.Exists(ledgerPath));
                var lines = File.ReadAllLines(ledgerPath);
                Assert.AreEqual(2, lines.Length);
                Assert.IsTrue(lines[0].StartsWith("{"));
                Assert.IsTrue(lines[0].EndsWith("}"));
                Assert.IsTrue(lines[1].StartsWith("{"));
                Assert.IsTrue(lines[1].EndsWith("}"));
                StringAssert.Contains(lines[0], "approvalId");
                StringAssert.Contains(lines[0], "argumentDigest");
                StringAssert.Contains(lines[1], "\"timestampUtc\"");
                StringAssert.Contains(lines[1], "\"traceId\":\"trace-2\"");
                Assert.IsFalse(lines[1].Contains("approvalId"));
                Assert.IsFalse(lines[1].Contains("argumentDigest"));

                var secondLedger = new AssistantApprovalLedger(ledgerPath);
                secondLedger.Append(new AssistantApprovalLedgerEntry
                {
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    TraceId = "trace-3",
                    SessionId = "session-2",
                    CapabilityId = "assembly.insert_component",
                    Environment = "lab",
                    Event = AssistantApprovalLedgerEntry.Consumed,
                    Outcome = "consumed"
                });

                var afterAppend = File.ReadAllLines(ledgerPath);
                Assert.AreEqual(3, afterAppend.Length);
                StringAssert.Contains(afterAppend[0], "\"traceId\":\"trace-1\"");
                StringAssert.Contains(afterAppend[2], "\"traceId\":\"trace-3\"");
                StringAssert.Contains(afterAppend[2], "\"event\":\"consumed\"");
            }
            finally
            {
                TryDelete(tempRoot);
            }
        }

        [TestMethod]
        public void AssistantMutationSettings_Defaults_Resolve()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "bb-defaults-" + Guid.NewGuid().ToString("N"));
            var config = AgentConfig.LoadFrom(tempRoot);

            Assert.IsNotNull(config.Assistant);
            Assert.IsNotNull(config.Assistant.Mutations);
            Assert.IsFalse(config.Assistant.Mutations.Enabled);
            Assert.AreEqual(60, config.Assistant.Mutations.ApprovalTimeoutSeconds);
            StringAssert.StartsWith(config.Assistant.Mutations.TestFileRoot, AppIdentity.DefaultWorkingFolder);
            StringAssert.EndsWith(config.Assistant.Mutations.TestFileRoot, "AssistantTestFiles");
        }

        [TestMethod]
        public void AgentConfig_InvalidFallback_Loads_With_Mutation_Defaults()
        {
            var configPath = Path.Combine(Path.GetTempPath(), "bb-fallback-" + Guid.NewGuid().ToString("N"), "config", Path.GetFileName(AppIdentity.ConfigPath("root")));
            var failure = new AgentConfigurationException("CONFIG_PRESENT_INVALID", configPath, new InvalidDataException("bad config"));

            var config = AgentConfig.CreateInvalidFallback(configPath, failure);

            Assert.IsNotNull(config.Assistant);
            Assert.IsNotNull(config.Assistant.Mutations);
            Assert.IsFalse(config.Assistant.Mutations.Enabled);
            Assert.AreEqual(60, config.Assistant.Mutations.ApprovalTimeoutSeconds);
            StringAssert.StartsWith(config.Assistant.Mutations.TestFileRoot, AppIdentity.DefaultWorkingFolder);
            StringAssert.EndsWith(config.Assistant.Mutations.TestFileRoot, "AssistantTestFiles");
            Assert.AreEqual("CONFIG_PRESENT_INVALID", config.ConfigurationDiagnostics.ConfigurationLoadStatus);
            Assert.AreEqual("CONFIG_VALUE_DEFAULTED", config.ConfigurationDiagnostics.AssistantValueSource);
            Assert.AreEqual(configPath, config.ConfigurationDiagnostics.ConfigPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }
    }
}
