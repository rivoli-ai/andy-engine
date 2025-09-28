# Andy.Engine Examples

This directory contains examples demonstrating the Andy.Engine framework.

## SimpleAgent

A basic demonstration of the agent framework concepts showing the agent execution loop phases:
- Planning
- Executing
- Observing
- Critiquing

To run:
```bash
dotnet run --project examples/SimpleAgent
```

## Full Integration Example

For a complete working example with real tool implementations, you'll need to:

1. Implement the Andy.Tools interfaces properly
2. Configure an actual LLM provider (OpenAI, Azure, etc.)
3. Set up proper tool registrations

The framework is designed to work with:
- **Andy.Tools**: Tool execution framework
- **Andy.Llm**: LLM provider abstractions
- **Andy.Context**: Context management
- **Andy.Model**: Shared data models

## Creating Your Own Agent

```csharp
// 1. Set up dependencies
var llmProvider = new OpenAIProvider(options);
var toolRegistry = new ToolRegistry();
var toolExecutor = new ToolExecutor(toolRegistry);

// 2. Build the agent
var agent = AgentBuilder.Create()
    .WithDefaults(llmProvider, toolRegistry, toolExecutor)
    .Build();

// 3. Define goal and constraints
var goal = new AgentGoal(
    UserGoal: "Your task description",
    Constraints: new[] { "Constraint 1", "Constraint 2" }
);

// 4. Run the agent
var result = await agent.RunAsync(goal, budget, errorPolicy);
```

## Testing

The framework includes comprehensive unit tests in the `tests/` directory covering:
- State management
- Observation normalization
- Policy engine decisions
- JSON schema validation

Run tests with:
```bash
dotnet test
```