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
        [InlineData("Patient needs CPAP", MedicalDeviceType.CPAP)]
        [InlineData("Requires oxygen tank", MedicalDeviceType.OxygenTank)]
        [InlineData("Prescribe wheelchair", MedicalDeviceType.Wheelchair)]
        [InlineData("Needs CpAp machine", MedicalDeviceType.CPAP)] // Case insensitivity
        [InlineData("CPAP and oxygen", MedicalDeviceType.CPAP)] // First match
        [InlineData("No device needed", MedicalDeviceType.Unknown)] // No match
        public void ParseDeviceType_Returns_Correct_Device(string note, MedicalDeviceType expected)
        {
            var result = PhysicianNoteParser.ParseDeviceType(note);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("CPAP with full face mask", MedicalDeviceType.CPAP, MaskType.FullFace)]
        [InlineData("CPAP but no mask specified", MedicalDeviceType.CPAP, MaskType.None)]
        public void ParseMaskType_Returns_Correct_Mask(string note, MedicalDeviceType device, MaskType expected)
        {
            var result = PhysicianNoteParser.ParseMaskType(note, device);
            Assert.Equal(expected, result);
        }

        [Theory]
        // Standard cases
        [InlineData("Use oxygen during sleep", new[] { Usage.Sleep })]
        [InlineData("Oxygen required for exertion", new[] { Usage.Exertion })]
        [InlineData("Needs oxygen for sleep and exertion", new[] { Usage.Sleep, Usage.Exertion })]
        // Edge cases
        [InlineData("", new Usage[0])]
        [InlineData(null, new Usage[0])]
        [InlineData("No usage mentioned", new Usage[0])]
        [InlineData("sleeping and exercising", new[] { Usage.Sleep })]
        // Case sensitivity
        [InlineData("SLEEP and EXERTION", new[] { Usage.Sleep, Usage.Exertion })]
        public void ParseUsage_DetectsUsageScenarios(string input, Usage[] expected)
        {
            var result = PhysicianNoteParser.ParseUsage(input);
            var expectedSet = new HashSet<Usage>(expected);

            Assert.True(result.SetEquals(expectedSet), $"Expected: {string.Join(",", expected)} | Actual: {string.Join(",", result)}");
        }

        [Theory]
        [InlineData("Patient needs humidifier", PhysicianNoteParser.HumidifierKeyword)]
        [InlineData("No add-ons needed", null)]
        public void ParseAddOns_Detects_Humidifier(string note, string expected)
        {
            var result = PhysicianNoteParser.ParseAddOns(note);
            Assert.Equal(expected, result);
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
