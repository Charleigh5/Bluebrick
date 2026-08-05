using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlueBrick.Agent;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class AssistantChainedReceiptTests
    {
        [TestMethod]
        public void ChainedReceipt_Append_SetsPreviousReceiptHash()
        {
            var chain = new AssistantChainedReceiptChain();
            var receipt1 = new AssistantChainedReceipt { ActorId = "actor-1", ActorName = "Test Actor" };
            receipt1.Seal();
            chain.Append(receipt1);

            var receipt2 = new AssistantChainedReceipt { ActorId = "actor-2", ActorName = "Test Actor 2" };
            chain.Append(receipt2);

            Assert.AreEqual(receipt1.CanonicalPayloadHash, receipt2.PreviousReceiptHash, "Second receipt must link to first receipt hash.");
        }

        [TestMethod]
        public void ChainedReceipt_ChainIsValid_WhenUntampered()
        {
            var chain = new AssistantChainedReceiptChain();
            chain.Append(new AssistantChainedReceipt { ActorId = "a1", ActorName = "A" });
            chain.Append(new AssistantChainedReceipt { ActorId = "a2", ActorName = "B" });
            chain.Append(new AssistantChainedReceipt { ActorId = "a3", ActorName = "C" });

            Assert.IsTrue(chain.IsChainValid(), "Untampered chain should be valid.");
        }

        [TestMethod]
        public void ChainedReceipt_ChainIsInvalid_WhenTampered()
        {
            var chain = new AssistantChainedReceiptChain();
            var receipt1 = new AssistantChainedReceipt { ActorId = "a1", ActorName = "A" };
            chain.Append(receipt1);

            var receipt2 = new AssistantChainedReceipt { ActorId = "a2", ActorName = "B" };
            chain.Append(receipt2);

            receipt2.Message = "tampered message";

            Assert.IsFalse(chain.IsChainValid(), "Tampered chain should be invalid.");
        }

        [TestMethod]
        public void ChainedReceipt_CreateCorrection_LinksToOriginal()
        {
            var chain = new AssistantChainedReceiptChain();
            var original = new AssistantChainedReceipt { ActorId = "a1", ActorName = "Original", ToolIdentity = "search_local_vault" };
            chain.Append(original);

            var correction = chain.CreateCorrection(original.ReceiptId, "a2", "Corrector", "Fixed incorrect result");

            Assert.AreEqual("corrected", correction.Status);
            Assert.AreEqual(original.ReceiptId, correction.Metadata["corrects_receipt_id"]);
            Assert.IsTrue(correction.Message.Contains(original.ReceiptId));
        }

        [TestMethod]
        public void ChainedReceipt_CreateCorrection_ThrowsForMissingOriginal()
        {
            var chain = new AssistantChainedReceiptChain();
            Assert.ThrowsException<ArgumentException>(() =>
                chain.CreateCorrection("nonexistent-receipt", "a1", "Corrector", "reason"));
        }

        [TestMethod]
        public void ChainedReceipt_SealedHasCanonicalPayloadHash()
        {
            var receipt = new AssistantChainedReceipt { ActorId = "a1", ActorName = "A" };
            receipt.Seal();
            Assert.IsFalse(string.IsNullOrEmpty(receipt.CanonicalPayloadHash), "Sealed receipt must have canonical payload hash.");
        }

        [TestMethod]
        public void ChainedReceipt_FirstReceipt_HasEmptyPreviousReceiptHash()
        {
            var chain = new AssistantChainedReceiptChain();
            var receipt = new AssistantChainedReceipt { ActorId = "a1", ActorName = "A" };
            chain.Append(receipt);

            Assert.AreEqual(string.Empty, receipt.PreviousReceiptHash, "First receipt in chain must have empty previous hash.");
        }

        [TestMethod]
        public void ChainedReceipt_HeadReturnsLastReceipt()
        {
            var chain = new AssistantChainedReceiptChain();
            var receipt1 = new AssistantChainedReceipt { ActorId = "a1" };
            chain.Append(receipt1);
            var receipt2 = new AssistantChainedReceipt { ActorId = "a2" };
            chain.Append(receipt2);

            Assert.AreSame(receipt2, chain.Head, "Head must return the last appended receipt.");
        }

        [TestMethod]
        public void ChainedReceipt_CorrectionCreatesLinkedSupersedingReceipt()
        {
            var chain = new AssistantChainedReceiptChain();
            var original = new AssistantChainedReceipt { ActorId = "a1", ToolIdentity = "search_local_vault", Status = "ok" };
            chain.Append(original);

            var correction = chain.CreateCorrection(original.ReceiptId, "a2", "Corrector", "supersedes original");

            Assert.IsTrue(correction.CanonicalPayloadHash.Length > 0, "Correction receipt must be sealed.");
            Assert.AreEqual(original.CanonicalPayloadHash, correction.PreviousReceiptHash, "Correction must link to original hash.");
        }
    }
}