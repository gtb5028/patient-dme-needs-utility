using Newtonsoft.Json;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Represents a patient's Durable Medical Equipment (DME) needs, including oxygen therapy requirements.
    /// This class captures details such as device specifications, usage instructions, diagnosis,
    /// provider information, and patient demographics.
    /// </summary>
    public class PatientDmeNeeds
    {
        /// <summary>The type of medical device prescribed (e.g., "Oxygen Tank").</summary>
        [JsonProperty("device")]
        public string Device { get; set; }

        /// <summary>The prescribed oxygen flow rate in liters (e.g., "2 L").</summary>
        [JsonProperty("liters")]
        public string Liters { get; set; }

        /// <summary>When the device should be used.</summary>
        [JsonProperty("usage")]
        public HashSet<string> Usage { get; set; }

        /// <summary>The medical diagnosis justifying the DME (e.g., "COPD").</summary>
        [JsonProperty("diagnosis")]
        public string Diagnosis { get; set; }

        /// <summary>The name of the healthcare provider ordering the DME (e.g., "Dr. Cuddy").</summary>
        [JsonProperty("ordering_provider")]
        public string OrderingProvider { get; set; }

        /// <summary>The full name of the patient (e.g., "Harold Finch").</summary>
        [JsonProperty("patient_name")]
        public string PatientName { get; set; }

        /// <summary>The patient's date of birth in MM/DD/YYYY format (e.g., "04/12/1952").</summary>
        [JsonProperty("dob")]
        public string DOB { get; set; }

        /// <summary>The type of mask required, if applicable (e.g., "Nasal Cannula"). May be empty.</summary>
        [JsonProperty("mask_type")]
        public string MaskType { get; set; }

        /// <summary>Additional accessories or notes related to the DME. May be null or empty.</summary>
        [JsonProperty("add_ons")]
        public string AddOns { get; set; }

        /// <summary>Any qualifying conditions or codes for insurance purposes. May be empty.</summary>
        [JsonProperty("qualifier")]
        public string Qualifier { get; set; }
    }
}
 