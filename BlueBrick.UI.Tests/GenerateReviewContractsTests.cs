using System;
using BlueBrick;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BlueBrick.UI.Tests
{
    [TestClass]
    public class GenerateReviewContractsTests
    {
        [TestMethod]
        public void GenerateReviewRequest_UsesPhaseTwoDefaults()
        {
            var requestType = typeof(SwAddin).Assembly.GetType("BlueBrick.Agent.GenerateReviewRequest", true);
            var request = Activator.CreateInstance(requestType);

            Assert.AreEqual("ASME", requestType.GetProperty("StandardsBaseline").GetValue(request));
            Assert.AreEqual("Y14.100", requestType.GetProperty("StandardsVersion").GetValue(request));
            Assert.AreEqual("default", requestType.GetProperty("CustomerRulesetVersion").GetValue(request));
            Assert.AreEqual("incremental_checkpoint", requestType.GetProperty("AnalysisMode").GetValue(request));
            Assert.AreEqual("supervised_auto_fix", requestType.GetProperty("AutoActionPolicy").GetValue(request));
            Assert.AreEqual(true, requestType.GetProperty("AutoApplyLowRiskActions").GetValue(request));
        }

        [TestMethod]
        public void SuggestedAction_RoundTripsPhaseTwoFields()
        {
            var actionType = typeof(SwAddin).Assembly.GetType("BlueBrick.Agent.SuggestedAction", true);
            var action = Activator.CreateInstance(actionType);
            actionType.GetProperty("Id").SetValue(action, "action_1");
            actionType.GetProperty("ActionType").SetValue(action, "refresh_metadata");
            actionType.GetProperty("Label").SetValue(action, "Refresh artifact metadata");
            actionType.GetProperty("Description").SetValue(action, "Refresh low-risk metadata only.");
            actionType.GetProperty("RequiresConfirmation").SetValue(action, false);
            actionType.GetProperty("RiskLevel").SetValue(action, "LOW");
            actionType.GetProperty("ExecutionScope").SetValue(action, "artifact");
            actionType.GetProperty("IdempotencyKey").SetValue(action, "idem_action_1");
            actionType.GetProperty("Deterministic").SetValue(action, true);

            var json = JsonConvert.SerializeObject(action);
            var roundTrip = JsonConvert.DeserializeObject(json, actionType);

            Assert.IsNotNull(roundTrip);
            Assert.AreEqual("LOW", actionType.GetProperty("RiskLevel").GetValue(roundTrip));
            Assert.AreEqual("artifact", actionType.GetProperty("ExecutionScope").GetValue(roundTrip));
            Assert.AreEqual("idem_action_1", actionType.GetProperty("IdempotencyKey").GetValue(roundTrip));
            Assert.AreEqual(true, actionType.GetProperty("Deterministic").GetValue(roundTrip));
            Assert.AreEqual(false, actionType.GetProperty("RequiresConfirmation").GetValue(roundTrip));
        }
    }
}
