using System.Linq;
using System.Threading.Tasks;
using BlueBrick.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace BlueBrick.UI.Tests.Agent
{
    [TestClass]
    public class SetCustomPropertyDescriptorTests
    {
        private const string ToolName = "solidworks.set_custom_property";

        [TestMethod]
        public void SetCustomProperty_DescriptorAndSchema_AreExposedOnlyWhenLabMutationFlagIsEnabled()
        {
            var isLabBuild = IsLabBuild();
            var config = CreateConfig(true);
            var toolService = new AssistantToolService(config);
            var descriptor = toolService.GetCatalog().Single(tool => tool.Name == ToolName);

            Assert.AreEqual(isLabBuild, descriptor.Enabled,
                "The mutation descriptor must require both a Lab build and the mutation flag.");
            Assert.IsTrue(descriptor.MutatesCad);
            Assert.IsFalse(descriptor.ReadOnly);
            Assert.IsTrue(descriptor.AllowedInChat);
            CollectionAssert.AreEqual(new[] { "HUMAN_APPROVED_MUTATION" }, descriptor.AllowedModes);
            Assert.IsFalse(descriptor.RequiresCredential);
            Assert.AreEqual("medium", descriptor.RiskLevel);
            Assert.IsTrue(descriptor.AuditRequired);

            var schemas = new OpenAiAssistantService(config, toolService).GetToolSchemasForTest();
            var schema = schemas
                .OfType<JObject>()
                .SingleOrDefault(item => (string)item["function"]?["name"] == ToolName);

            if (!isLabBuild)
            {
                Assert.IsNull(schema, "Production builds must not expose the mutation tool to the model.");
                return;
            }

            Assert.IsNotNull(schema, "A Lab build with mutations enabled must expose the tool to the model.");
            var parameters = (JObject)schema["function"]["parameters"];
            var properties = (JObject)parameters["properties"];
            CollectionAssert.AreEquivalent(
                new[] { "file_path", "property", "value" },
                properties.Properties().Select(property => property.Name).ToArray());
            CollectionAssert.AreEqual(
                new[] { "file_path", "property", "value" },
                ((JArray)parameters["required"]).Values<string>().ToArray());
            Assert.IsTrue(properties.Properties().All(property => (string)property.Value["type"] == "string"));
        }

        [TestMethod]
        public void SetCustomProperty_IsAbsentFromModelSchemasWhenMutationFlagIsDisabled()
        {
            var config = CreateConfig(false);
            var toolService = new AssistantToolService(config);
            var descriptor = toolService.GetCatalog().Single(tool => tool.Name == ToolName);
            var schemas = new OpenAiAssistantService(config, toolService).GetToolSchemasForTest();

            Assert.IsFalse(descriptor.Enabled);
            Assert.IsFalse(schemas
                .OfType<JObject>()
                .Any(item => (string)item["function"]?["name"] == ToolName));
        }

        [TestMethod]
        public async Task SetCustomProperty_DirectInvocation_FailsClosedBeforeAnyExecutor()
        {
            var config = CreateConfig(true);
            var result = await new AssistantToolService(config).ExecuteAsync(
                new AssistantToolRequest
                {
                    ToolName = ToolName,
                    Parameters =
                    {
                        ["file_path"] = @"C:\approved\part.SLDPRT",
                        ["property"] = "Description",
                        ["value"] = "Fixture bracket"
                    }
                },
                "set-property-descriptor-test");

            Assert.AreEqual(AppIdentity.IsLabBuild ? "approval_required" : "disabled", result.Status);
            Assert.IsNotNull(result.Receipt);
            Assert.IsFalse(result.Receipt.Allowed);
        }

        [TestMethod]
        public void SetCustomProperty_NameAvoidsPermanentMutationSubstringDenyList()
        {
            var policy = new AssistantToolPolicy();

            var allowedName = policy.EvaluateToolName(ToolName);
            var deniedControl = policy.EvaluateToolName("solidworks.write_custom_property");

            Assert.IsTrue(allowedName.Allowed, "The load-bearing set_custom_property name must pass the name policy.");
            Assert.IsFalse(deniedControl.Allowed, "The write substring control must remain denied.");
            Assert.AreEqual("DENY", deniedControl.Code);
        }

        private static AgentConfig CreateConfig(bool mutationsEnabled)
        {
            return new AgentConfig
            {
                Assistant = new AssistantSettings
                {
                    Mutations = new AssistantMutationSettings { Enabled = mutationsEnabled }
                }
            };
        }

        private static bool IsLabBuild()
        {
            return AppIdentity.IsLabBuild;
        }
    }
}
