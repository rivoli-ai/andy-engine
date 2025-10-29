# Andy.Engine.Tests

Integration tests for the Andy.Engine library.

## Running Tests with Real LLM

Tests ending with `WithRealLlm` require an OpenAI API key. There are two ways to configure this:

### Option 1: Environment Variable (Recommended for macOS/Linux)

Set the `OPENAI_API_KEY` environment variable:

**macOS/Linux:**
```bash
export OPENAI_API_KEY=sk-your-api-key-here
dotnet test
```

**Windows (PowerShell):**
```powershell
# Set for current session
$env:OPENAI_API_KEY="sk-your-api-key-here"
dotnet test

# Or set permanently for your user account
[System.Environment]::SetEnvironmentVariable('OPENAI_API_KEY','sk-your-api-key-here','User')
# Note: Restart PowerShell or your IDE after setting permanently
```

**Windows (Command Prompt):**
```cmd
REM Set for current session
set OPENAI_API_KEY=sk-your-api-key-here
dotnet test

REM Or set permanently for your user account
setx OPENAI_API_KEY "sk-your-api-key-here"
REM Note: Restart Command Prompt or your IDE after setx
```

> **⚠️ Windows Note:** If environment variables aren't working in your test execution context (common with some IDEs), use Option 2 below.

### Option 2: Configuration File (Recommended for Windows)

Edit `tests/Andy.Engine.Tests/appsettings.json` and replace `your-openai-api-key-here` with your actual API key:

```json
{
  "Llm": {
    "DefaultProvider": "OpenAI",
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-your-actual-api-key-here",
        "ApiBase": "https://api.openai.com/v1",
        "Model": "gpt-4o",
        "Enabled": true
      },
      "Cerebras": {
        "Enabled": false
      },
      "Ollama": {
        "Enabled": false
      },
      "Anthropic": {
        "Enabled": false
      }
    }
  }
}
```

**⚠️ Security Warning:**
- Do NOT commit your actual API key to source control!
- The `appsettings.json` file uses `your-openai-api-key-here` as a placeholder
- If you hardcode your key for testing, make sure to revert it before committing
- Consider adding `appsettings.json` to `.git/info/exclude` locally

**Why other providers are disabled:**
- Even if you have multiple LLM provider API keys in your environment (e.g., CEREBRAS_API_KEY, ANTHROPIC_API_KEY), the configuration explicitly disables them
- This ensures tests always use OpenAI for consistency
- `DefaultProvider` is set to "OpenAI" with other providers disabled

**⚠️ Known Limitation - Model Configuration:**
- The `Model` setting in appsettings.json may not be respected when environment variables are also present
- This is due to how `ConfigureLlmFromEnvironment()` and `AddLlmServices()` interact
- If you need to use a specific model, temporarily unset the `OPENAI_MODEL` environment variable:
  - PowerShell: `Remove-Item Env:OPENAI_MODEL`
  - Bash: `unset OPENAI_MODEL`
  - Or restart your terminal/IDE after unsetting it permanently

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
