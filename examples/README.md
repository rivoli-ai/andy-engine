# Andy.Engine Examples

This directory contains examples for the Andy.Engine framework.

## SimpleAgent

A complete example showing the Andy.Engine framework using real LLM providers and tool implementations:

- **Planning phase**: Real LLM (OpenAI, Cerebras, etc.) decides next action
- **Executing phase**: Real tool execution from Andy.Tools library
- **Observing phase**: Extract key facts from actual tool results
- **Critiquing phase**: LLM assesses progress toward goal

### Prerequisites

You need to configure at least one LLM provider by setting environment variables:

```bash
# For OpenAI (recommended)
export OPENAI_API_KEY="your-api-key-here"

# For Cerebras
export CEREBRAS_API_KEY="your-api-key-here"

# For Anthropic Claude
export ANTHROPIC_API_KEY="your-api-key-here"

# For local Ollama
# Install Ollama and ensure it's running
```

### Running the Example

```bash
cd examples/SimpleAgent
dotnet run
```

The example will:
1. Initialize the Andy.Tools framework with built-in tools
2. Connect to your configured LLM provider
3. Create an agent with a specific goal (get current time and encode to base64)
4. Execute the agent loop with real tool calls
5. Display the results and execution trace

### What the Example Demonstrates

- **Real LLM Integration**: Uses Andy.Llm to connect to OpenAI, Cerebras, or other providers
- **Real Tool Execution**: Uses Andy.Tools built-in tools like `datetime_tool` and `encoding_tool`
- **Event-Driven Monitoring**: Subscribe to agent events for visibility
- **State Management**: Track agent state through turns
- **Error Handling**: Robust error policies with retries
- **Budget Constraints**: Limit turns and execution time

### Customizing the Example

You can modify the goal to test different scenarios:

```csharp
var goal = new AgentGoal(
    UserGoal: "Read a file and count the words in it",
    Constraints: new[] {
        "Use the read_file tool",
        "Use the text_processing tool",
        "Complete within 5 turns"
    }
);
```

### Available Tools

The Andy.Tools library provides many built-in tools:
- `datetime_tool` - Date and time operations
- `encoding_tool` - Base64 encoding/decoding
- `read_file` - File reading
- `write_file` - File writing
- `text_processing` - Text manipulation
- `system_info` - System information
- `web_fetch` - HTTP requests
- And many more...

## Framework Architecture

The Andy.Engine framework provides:
- **Agent**: Main orchestration loop
- **Planner**: LLM-based decision making with structured JSON output
- **Executor**: Tool execution with retry policies
- **Critic**: Goal assessment and recommendations
- **State Manager**: Working memory and persistence
- **Policy Engine**: Error handling and recovery
- **Observation Normalizer**: Context extraction from tool results

## Testing

Unit tests demonstrate the framework components in isolation:
```bash
dotnet test
```

Tests verify component behavior with mocking for external dependencies.