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
            PatientDmeNeeds parsedNeeds = PhysicianNoteParser.Parse(physicianNote);

            string expectedJson = ResourceFileHelper.ReadResourceFile(ExpectedJsonPath);
            JObject expectedResult = JObject.Parse(expectedJson);
            var expectedNeeds = expectedResult.ToObject<PatientDmeNeeds>();
            
            var comparer = new PatientDmeNeedsComparer();
            Assert.True(
                comparer.Equals(expectedNeeds, parsedNeeds),
                "Parsed physician note did not match the expected output."
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

        [Theory]
        // Standard cases
        [InlineData("Use oxygen during sleep", new[] { "sleep" })]
        [InlineData("Oxygen required for exertion", new[] { "exertion" })]
        [InlineData("Needs oxygen for sleep and exertion", new[] { "sleep", "exertion" })]
        // Edge cases
        [InlineData("", new string[0])]
        [InlineData(null, new string[0])]
        [InlineData("No usage mentioned", new string[0])]
        [InlineData("sleeping and exercising", new[] { "sleep" })]
        // Case sensitivity
        [InlineData("SLEEP and EXERTION", new[] { "sleep", "exertion" })]
        public void ParseUsage_DetectsUsageScenarios(string input, string[] expected)
        {
            var result = PhysicianNoteParser.ParseUsage(input);
            var expectedSet = new HashSet<string>(expected, StringComparer.OrdinalIgnoreCase);

            Assert.True(result.SetEquals(expectedSet), $"Expected: {string.Join(",", expected)} | Actual: {string.Join(",", result)}");
        }

        private class PatientDmeNeedsComparer : IEqualityComparer<PatientDmeNeeds>
        {
            public bool Equals(PatientDmeNeeds x, PatientDmeNeeds y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.Device == y.Device
                    && x.Liters == y.Liters
                    && x.Usage.SetEquals(y.Usage)
                    && x.Diagnosis == y.Diagnosis
                    && x.OrderingProvider == y.OrderingProvider
                    && x.PatientName == y.PatientName
                    && x.DOB == y.DOB
                    && x.MaskType == y.MaskType
                    && x.AddOns == y.AddOns
                    && x.Qualifier == y.Qualifier;
            }

            public int GetHashCode(PatientDmeNeeds obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
