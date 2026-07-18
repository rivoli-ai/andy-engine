# Andy.Engine.Tests

Integration tests for the Andy.Engine library.

## Default (deterministic, offline)

```bash
dotnet test
```

The default suite is **Mock-only**: it makes **no LLM network calls** and is deterministic, even
on a machine that has `OPENAI_API_KEY` / `ANTHROPIC_API_KEY` configured. Live-LLM cases are opt-in
(see below), so a plain `dotnet test` never spends money and never fails because of model-output
variation.

## Running the live-LLM suite (opt-in)

Live cases are gated by the `ANDY_LIVE_LLM_TESTS` environment variable **in addition to** a
configured API key — an API key alone is not enough. To run them:

```bash
ANDY_LIVE_LLM_TESTS=1 OPENAI_API_KEY=sk-your-api-key-here dotnet test
```

> **Cost & variability:** live cases make **paid** calls to the configured provider and exercise a
> real model, so their outputs vary between runs and occasional flakes are expected. Run them
> intentionally (e.g. before a benchmark or release), not on every `dotnet test`.

Accepted truthy values for `ANDY_LIVE_LLM_TESTS`: `1`, `true`, `yes` (case-insensitive). Each
live-capable `[Theory]` still runs its Mock row too; the opt-in only adds the `Real` row.

## Configuring credentials for the live suite

There are two ways to configure the API key:

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

Run all (default, Mock-only) tests:
```bash
dotnet test
```

Live-LLM cases only appear when opted in. Without the opt-in, the `Real` rows are not emitted at
all, so a `--filter` for them matches nothing:
```bash
# Opt in first, then filter to the live rows if desired:
ANDY_LIVE_LLM_TESTS=1 OPENAI_API_KEY=sk-... dotnet test --filter "DisplayName~Real"
```

Run a specific test:
```bash
dotnet test --filter "ListDirectory_BasicListing"
```

## Test Structure

- **Mocked LLM Tests**: Use a mocked LLM provider that returns predefined responses. Fast and don't require an API key.
- **Real LLM Tests**: Use actual OpenAI API calls. Slower and require an API key, but test real-world behavior.

Both test types use the same scenarios and validation logic, ensuring consistency between mocked and real executions.
