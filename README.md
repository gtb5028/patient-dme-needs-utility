# Durable Medical Equipment (DME) Needs Parser

## 📖 Overview
A refactored system for extracting and processing physician notes containing DME prescriptions. Converts unstructured clinical notes into structured JSON data for documenting patient DME needs.

### Key Features
- **Note Processing**: Extracts DME needs from free-text physician notes
- **Data Validation**: Ensures clinical validity of extracted data
- **API Integration**: Posts structured data to external systems (simulated)
- **Error Resilience**: Graceful handling of malformed input

## 🛠️ Development Environment
### Tools
- **IDE**: Visual Studio 2022
- **AI Assistants**:
  - DeepSeek/ChatGPT (architecture design/code generation)
  - GitHub Copilot (code generation)
  - Claude + Windsurf (code review)
- **Testing Framework**: xUnit

## ✅ Implemented Requirements
### Code Quality Improvements
- Decomposed monolithic `Main` into:
  - `PhysicianNoteParser` (core logic)
  - `ResourceFileHelper` (I/O operations)
  - `PatientDmeNeeds` (data model)
- Eliminated technical debt:
  - Removed unused variables
  - Replaced cryptic names (`JObject r` → `PatientDmeNeeds`)
  - Updated misleading comments

### Reliability Enhancements
- Comprehensive error handling:
  - Input validation
  - Try/catch blocks
  - Diagnostic logging
- Preserved all original functionality

### Testing Suite
- **Unit Tests**: Core parsing logic
- **Test Data**: Sample notes in `/Resources`

## ⚠️ Assumptions & Limitations
### Assumptions
1. Clinical notes follow consistent formatting patterns
2. `expected_output1.json` represents the canonical output structure
3. Sensitive data handling:
   - Removed fallback patient data (privacy concerns)

### Limitations
- API endpoint (`alert-api.com/DrExtract`) is non-functional (sample only)
- Limited to English-language notes

## 🏃 Getting Started
### Requirements
- .NET 9.0
- Visual Studio 2022 (recommended)

### Running the Project
1. Clone repository
2. Open `PatientDmeNeedsUtility.sln` in Visual Studio
3. Build solution (Ctrl+Shift+B)
4. Run unit tests via Test Explorer
5. Set startup project and run (F5)

### Sample Command
```bash
dotnet run --project PatientDmeNeedsUtility