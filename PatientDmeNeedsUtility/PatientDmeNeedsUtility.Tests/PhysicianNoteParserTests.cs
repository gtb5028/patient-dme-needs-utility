using Newtonsoft.Json.Linq;

namespace Synapse.PatientDmeNeedsUtility.Tests
{
    /// <summary>
    /// Contains unit tests for <see cref="PhysicianNoteParser"/>, validating its ability to
    /// convert physician notes into structured JSON data.
    /// </summary>
    public class PhysicianNoteParserTests
    {
        private const string PhysicianNotePath = "physician_note1.txt";
        private const string ExpectedJsonPath = "expected_output1.json";

        /// <summary>
        /// Verifies that <see cref="PhysicianNoteParser.Parse"/> correctly converts a well-formatted
        /// physician note into the expected JSON structure, with all fields properly mapped.
        /// </summary>
        [Fact]
        public void Parse_WithTypicalNote_ReturnsExpectedJson()
        {
            string physicianNote = ResourceFileHelper.ReadResourceFile(PhysicianNotePath);
            string expectedJson = ResourceFileHelper.ReadResourceFile(ExpectedJsonPath);
            JObject parsedResult = PhysicianNoteParser.Parse(physicianNote);
            JObject expectedResult = JObject.Parse(expectedJson);

            Assert.True(
                JToken.DeepEquals(expectedResult, parsedResult),
                "Parsed physician note JSON did not match the expected output."
            );
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Parse_RejectsInvalidInputs(string input)
        {
            try
            {
                PhysicianNoteParser.Parse(input);
                Assert.Fail($"Expected ArgumentException for input: '{input}'");
            }
            catch (ArgumentException)
            {
                // Test passes as exception was expected no need to assert
                // just return
                return;
            }
            catch (Exception ex)
            {
                // Fail with details if wrong exception type
                Assert.Fail($"Expected ArgumentException but got {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
