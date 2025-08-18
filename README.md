# Durable Medical Equipment (DME) Needs Parser

## 📖 Overview
A refactored system for extracting and processing physician notes containing DME prescriptions. Converts unstructured clinical notes into structured JSON data for documenting patient DME needs.

## ✨ Key Features

* **Note Processing**: Extracts DME needs from free-text or JSON-wrapped physician notes
* **Data Validation**: Ensures clinical validity of extracted data
* **API Integration**: Posts structured data to external systems (simulated)
* **Configurable Retry Logic**: API client with exponential backoff, transient-failure handling, and configurable retry policy
* **Extended Device Support**: Handles multiple DME device types, qualifiers, and add-ons
* **LLM Support**: (Optional) Uses the OpenRouter API for enhanced parsing of physician notes
* **Error Resilience**: Graceful handling of malformed input

---

## 🛠️ Development Environment

**Tools**

* IDE: Visual Studio 2022
* AI Assistants:

  * DeepSeek / ChatGPT (architecture design & code generation)
  * GitHub Copilot (code generation)
  * Claude + Windsurf (code review)
* Testing Framework: xUnit

---

## ✅ Implemented Requirements

### Code Quality Improvements

* Decomposed monolithic `Main` into:

  * `PhysicianNoteParser` (core logic)
  * `ResourceFileHelper` (I/O operations)
  * `PatientDmeNeeds` (data model)
* Abstracted JSON parsing logic into a dedicated component
* Eliminated technical debt:

  * Removed unused variables
  * Replaced cryptic names (`JObject r` → `PatientDmeNeeds`)
  * Updated misleading comments

### Reliability Enhancements

* Comprehensive error handling:

  * Input validation
  * Try/catch blocks
  * Diagnostic logging
* Intelligent retry logic for API calls:

  * Configurable attempts and delays (`BaseRetryMs`, `MaxRetries`)
  * Exponential backoff for transient failures
  * Immediate abort on fatal errors
* Preserved all original functionality

### Testing Suite

* **Unit Tests**: Core parsing logic
* **Multiple Test Notes**: Several physician notes in `/Resources`, each with corresponding expected outputs
* Tests evolve with parsing improvements (qualifiers, add-ons, portable qualifier adjustments)

---

## ⚙️ Configuration

This project uses `appsettings.json` for configuration.

### Example `appsettings.json`:

```json
{
  "Api": {
    "BaseUrl": "https://alert-api.com",
    "ExtractEndpoint": "/DrExtract",
    "BaseRetryMs": 1000,
    "MaxRetries": 3
  },
  "Files": {
    "DefaultPhysicianNoteFile": "physician_note1.txt",
    "DefaultExpectedOutputFile": "expected_output1.json"
  },
  "Llm": {
    "OpenRouterApiKey": "<PUT_YOUR_API_KEY_HERE>",
    "BaseUrl": "https://openrouter.ai/api/v1/chat/completions",
    "Model": "deepseek/deepseek-chat-v3-0324:free",
    "Temperature": 0.0,
    "IsEnabled": true
  }
}
```

### 🔒 Secrets

* **Never commit real API keys** to source control.
* Configure secrets using:

  * **.NET User Secrets** (local development)

    ```sh
    dotnet user-secrets set "Llm:OpenRouterApiKey" "sk-xxxxxx"
    ```
  * **Environment variables** (CI/CD, Docker, production)

    ```sh
    export LLM__OPENROUTERAPIKEY="sk-xxxxxx"
    ```
  * **Secret Managers** (Azure Key Vault, AWS Secrets Manager, etc.)

### Disabling LLM Processing

* Set `"IsEnabled": false` in the `Llm` section to disable LLM usage and rely on rule-based parsing only.

---

## ⚠️ Assumptions & Limitations

### Assumptions

* Each physician note is assumed to map to **a single DME device** (may be updated with PM guidance).
* Input can be **raw text notes** or **JSON-wrapped notes**.
* Clinical notes follow consistent formatting patterns.
* `expected_output1.json` represents the canonical output structure for testing purposes.
* Sensitive data handling: no fallback patient data included (privacy).
* Due to lack of a formal API spec, assumptions were made about the source of truth for API requests and responses.
* Unit tests had mixed or incomplete data sources; the source of truth was assumed to be a combination of provided notes and expected outputs.

### Limitations

* API endpoint (`alert-api.com/DrExtract`) is non-functional (sample only).
* Currently supports only **one device per note**.
* Limited to English-language notes.
* OpenRouter LLM usage requires a valid API key.

---

## 🛠️ Future Improvements

* Further **abstract parsing logic** to separate manual extraction from LLM-based processing.
* Review and **improve consistency of names/variables** across the codebase.
* Enhance **async/await usage** throughout to improve concurrency and performance.
* Incorporate additional recommendations from **Windsurf/Claude** regarding architecture and parsing patterns.
* Extend support for **multiple devices per note** once business rules are clarified.
* Continue refining **unit tests** with clearer source-of-truth mappings as specs become available.

---

## 🏃 Getting Started

### Requirements

* .NET 9.0
* Visual Studio 2022 (recommended)

### Running the Project

1. Clone repository
2. Open `PatientDmeNeedsUtility.sln` in Visual Studio
3. Build solution (`Ctrl+Shift+B`)
4. Configure `appsettings.json` and/or secrets
5. Run unit tests via Test Explorer
6. Set startup project and run (`F5`)

### Sample Command
```bash
dotnet run --project PatientDmeNeedsUtility