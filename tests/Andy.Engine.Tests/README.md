# Andy.Engine.Tests

Integration tests for the Andy.Engine library.

## Running Tests with Real LLM

Tests ending with `WithRealLlm` require an OpenAI API key. There are two ways to configure this:

### Option 1: Environment Variable (Recommended)

Set the `OPENAI_API_KEY` environment variable:

**macOS/Linux:**
```bash
export OPENAI_API_KEY=sk-your-api-key-here
dotnet test
```

**Windows (PowerShell):**
```powershell
$env:OPENAI_API_KEY="sk-your-api-key-here"
dotnet test
```

**Windows (Command Prompt):**
```cmd
set OPENAI_API_KEY=sk-your-api-key-here
dotnet test
```

### Option 2: Configuration File (For Local Development Only)

**IMPORTANT:** The `${OPENAI_API_KEY}` syntax in `appsettings.json` is just a placeholder - it does NOT automatically expand environment variables. You must either:

1. **Set the environment variable** (see Option 1 above), OR
2. **Edit `appsettings.json`** and replace the placeholder with your actual API key:

```json
{
  "Llm": {
    "DefaultProvider": "openai",
    "Providers": {
      "openai": {
        "ApiKey": "sk-your-actual-api-key-here",
        "ApiBase": "https://api.openai.com/v1",
        "Model": "gpt-4o",
        "Enabled": true
      }
    }
  }
}
```

**⚠️ Security Warning:**
- Do NOT commit your actual API key to source control!
- The `appsettings.json` file in this repository uses `${OPENAI_API_KEY}` as a placeholder
- If you hardcode your key for testing, make sure to revert it before committing

### Using a Different Model

Set the `OPENAI_MODEL` environment variable or edit the `Model` field in `appsettings.json`:

```bash
export OPENAI_MODEL=gpt-4o-mini
```

## Running Tests

Run all tests:
```bash
dotnet test
```

Run only mocked tests (no API key needed):
```bash
dotnet test --filter "WithMockedLlm"
```

Run only real LLM tests:
```bash
dotnet test --filter "WithRealLlm"
```

Run a specific test:
```bash
dotnet test --filter "ListDirectory_BasicListing_WithRealLlm"
```

## Test Structure

- **Mocked LLM Tests**: Use a mocked LLM provider that returns predefined responses. Fast and don't require an API key.
- **Real LLM Tests**: Use actual OpenAI API calls. Slower and require an API key, but test real-world behavior.

Both test types use the same scenarios and validation logic, ensuring consistency between mocked and real executions.
