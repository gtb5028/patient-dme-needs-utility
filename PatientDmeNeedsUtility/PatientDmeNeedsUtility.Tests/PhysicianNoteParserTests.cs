using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Synapse.PatientDmeNeedsUtility.Tests
{
    public class PhysicianNoteParserTests
    {
        public static readonly ILogger<ResourceFileHelper> ResourceFileHelperLogger =
            NullLogger<ResourceFileHelper>.Instance;

        public static readonly ILogger<PhysicianNoteParser> PhysicianNoteParserLogger =
            NullLogger<PhysicianNoteParser>.Instance;

        [Theory]
        [InlineData("physician_note1.txt", "expected_output1.json")]
        [InlineData("physician_note2.txt", "expected_output2.json")]
        public void Parse_WithTypicalNote_ReturnsExpectedJson(string notePath, string expectedJsonPath)
        {
            var fileHelper = new ResourceFileHelper(ResourceFileHelperLogger);
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);

            string physicianNote = fileHelper.ReadResourceFile(notePath);
            PatientDmeNeeds parsedNeeds = parser.Parse(physicianNote);

            string expectedJson = fileHelper.ReadResourceFile(expectedJsonPath);
            PatientDmeNeeds expectedNeeds = parser.ParseJson(expectedJson);
            var comparer = new PatientDmeNeedsComparer();
            Assert.True(
                expectedNeeds != null &&
                parsedNeeds != null &&
                comparer.Equals(expectedNeeds, parsedNeeds),
                "Parsed physician note did not match the expected output."
            );
        }

        #nullable disable
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Parse_RejectsInvalidInputs(string input)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            Assert.Throws<ArgumentException>(() => parser.Parse(input));
        }

        [Theory]
        [InlineData("Patient needs CPAP", MedicalDeviceType.CPAP)]
        [InlineData("Requires oxygen tank", MedicalDeviceType.OxygenTank)]
        [InlineData("Prescribe wheelchair", MedicalDeviceType.Wheelchair)]
        [InlineData("Needs CpAp machine", MedicalDeviceType.CPAP)]
        [InlineData("CPAP and oxygen", MedicalDeviceType.CPAP)]
        [InlineData("Patient uses a walker for mobility", MedicalDeviceType.Walker)]
        [InlineData("Requires crutches for leg injury", MedicalDeviceType.Crutches)]
        [InlineData("Patient uses a cane daily", MedicalDeviceType.Cane)]
        [InlineData("Needs a nebulizer for asthma", MedicalDeviceType.Nebulizer)]
        [InlineData("Patient requires a prosthesis", MedicalDeviceType.Prosthesis)]
        [InlineData("Heart monitor prescribed", MedicalDeviceType.HeartMonitor)]
        [InlineData("No device needed", MedicalDeviceType.Unknown)]
        public void ParseDeviceType_Returns_Correct_Device(string note, MedicalDeviceType expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseDeviceType(note);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("CPAP with full face mask", MedicalDeviceType.CPAP, MaskType.FullFace)]
        [InlineData("CPAP but no mask specified", MedicalDeviceType.CPAP, MaskType.None)]
        public void ParseMaskType_Returns_Correct_Mask(string note, MedicalDeviceType device, MaskType expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseMaskType(note, device);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Use oxygen during sleep", new[] { Usage.Sleep })]
        [InlineData("Oxygen required for exertion", new[] { Usage.Exertion })]
        [InlineData("Needs oxygen for sleep and exertion", new[] { Usage.Sleep, Usage.Exertion })]
        [InlineData("", new Usage[0])]
        [InlineData(null, new Usage[0])]
        [InlineData("No usage mentioned", new Usage[0])]
        [InlineData("sleeping and exercising", new[] { Usage.Sleep })]
        [InlineData("SLEEP and EXERTION", new[] { Usage.Sleep, Usage.Exertion })]
        public void ParseUsage_DetectsUsageScenarios(string input, Usage[] expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseUsage(input);
            var expectedSet = new HashSet<Usage>(expected);

            Assert.True(result.SetEquals(expectedSet), $"Expected: {string.Join(",", expected)} | Actual: {string.Join(",", result)}");
        }

        [Theory]
        [InlineData("Patient requires CPAP with humidifier", new[] { "humidifier" })]
        [InlineData("CPAP with heated tubing and mask cushion recommended", new[] { "heated tubing", "mask cushion" })]
        [InlineData("Headgear and chin strap needed for CPAP", new[] { "headgear", "chin strap" })]
        [InlineData("No add-ons noted", new string[0])]
        [InlineData("Filter replacement required for oxygen device", new[] { "filter" })]
        [InlineData("Humidifier and heated tubing included", new[] { "humidifier", "heated tubing" })]
        public void ParseAddOns_Returns_Correct_Values(string note, string[] expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseAddOns(note);

            var expectedSet = new HashSet<string>(expected, StringComparer.OrdinalIgnoreCase);
            var resultSet = new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);

            Assert.True(expectedSet.SetEquals(resultSet),
                $"Expected: [{string.Join(", ", expectedSet)}], Actual: [{string.Join(", ", resultSet)}]");
        }

        [Theory]
        [InlineData("Patient has AHI > 20", new[] { "AHI>20" })]
        [InlineData("ahi:28 observed during sleep study", new[] { "AHI:28" })]
        [InlineData("AHI=15, CPAP recommended", new[] { "AHI=15" })]
        [InlineData("Patient requires a portable oxygen tank", new[] { "portable" })]
        [InlineData("Wheelchair is stationary and foldable", new[] { "stationary", "foldable" })]
        [InlineData("Walker is manual and foldable", new[] { "manual", "foldable" })]
        [InlineData("No qualifiers noted", new string[0])]
        public void ParseQualifiers_Returns_Correct_Values(string note, string[] expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseQualifiers(note);

            var expectedSet = new HashSet<string>(expected);
            var resultSet = new HashSet<string>(result);

            Assert.True(expectedSet.SetEquals(resultSet),
                $"Expected: [{string.Join(", ", expectedSet)}], Actual: [{string.Join(", ", resultSet)}]");
        }

        [Theory]
        [InlineData("Ordering Physician: Smith, John", "Smith, John")]
        [InlineData("Ordered By Jones, Sarah.", "Jones, Sarah")]
        [InlineData("No provider mentioned", "Unknown")]
        public void ParseOrderingProvider_Extracts_Correctly(string note, string expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseOrderingProvider(note);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Oxygen at 2L/min", MedicalDeviceType.OxygenTank, "2 L")]
        [InlineData("Needs 1.5 L", MedicalDeviceType.OxygenTank, "1.5 L")]
        public void ParseOxygenLiters_Formats_Correctly(string note, MedicalDeviceType device, string expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseOxygenLiters(note, device);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Patient Name: John Doe", "John Doe")]
        [InlineData("PATIENT NAME:  Jane Smith  ", "Jane Smith")]
        [InlineData("No name mentioned", "")]
        public void ParsePatientName_Extracts_Correctly(string note, string expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParsePatientName(note);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("DOB: 12/31/1990", "12/31/1990")]
        [InlineData("DOB: 04/12/1952", "04/12/1952")]
        [InlineData("dob: 1/1/2000", "1/1/2000")]
        [InlineData("No DOB recorded", "")]
        public void ParseDateOfBirth_Extracts_Correctly(string note, string expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseDateOfBirth(note);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Diagnosis: Sleep Apnea", "Sleep Apnea")]
        [InlineData("DIAGNOSIS:   COPD with exacerbation  ", "COPD with exacerbation")]
        [InlineData("No diagnosis section", "")]
        public void ParseDiagnosis_Extracts_Correctly(string note, string expected)
        {
            var parser = new PhysicianNoteParser(PhysicianNoteParserLogger, null, null);
            var result = parser.ParseDiagnosis(note);
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
                    && x.AddOns.SetEquals(y.AddOns)
                    && x.Qualifiers.SetEquals(y.Qualifiers);
            }

            public int GetHashCode(PatientDmeNeeds obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
