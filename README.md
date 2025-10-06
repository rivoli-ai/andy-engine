# Andy.Engine

C# framework for building LLM-driven agents with tool execution, planning, and state management.


> **ALPHA RELEASE WARNING**
>
> This software is in ALPHA stage. **NO GUARANTEES** are made about its functionality, stability, or safety.
>
> **CRITICAL WARNINGS:**
> - This tool performs **DESTRUCTIVE OPERATIONS** on files and directories
> - Permission management is **NOT FULLY TESTED** and may have security vulnerabilities
> - **DO NOT USE** in production environments
> - **DO NOT USE** on systems with critical or irreplaceable data
> - **DO NOT USE** on systems without complete, verified backups
> - The authors assume **NO RESPONSIBILITY** for data loss, system damage, or security breaches
>
> **USE AT YOUR OWN RISK**

## Features

- **Modular Architecture**: Planner, Executor, Critic, and Policy Engine components
- **Tool Management**: Schema validation, retry policies, error handling
- **State Management**: Persistent state tracking across agent turns
- **Observation Normalization**: Structured extraction of key facts and affordances
- **Policy Engine**: Intelligent retry, fallback, and error recovery strategies
- **Event-Driven**: Rich events for monitoring and debugging agent execution

## Installation

```bash
dotnet add package Andy.Engine --version 1.0.0-alpha.1
```

## Quick Start

```csharp
using Andy.Engine;
using Andy.Model.Llm;
using Andy.Tools.Core;

// Configure and build an agent
var agent = AgentBuilder.Create()
    .WithDefaults(llmProvider, toolRegistry, toolExecutor)
    .WithPlannerOptions(new PlannerOptions { Temperature = 0.0 })
    .Build();

// Define goal and constraints
var goal = new AgentGoal(
    UserGoal: "Find and summarize recent AI news",
    Constraints: new[] { "Focus on last week", "Include at least 3 sources" }
);

// Set budget limits
var budget = new Budget(
    MaxTurns: 10,
    MaxWallClock: TimeSpan.FromMinutes(5)
);

// Configure error handling
var errorPolicy = new ErrorHandlingPolicy(
    MaxRetries: 3,
    BaseBackoff: TimeSpan.FromSeconds(1),
    UseFallbacks: true,
    AskUserWhenMissingFields: true
);

// Run the agent
var result = await agent.RunAsync(goal, budget, errorPolicy);

if (result.Success)
{
    Console.WriteLine($"Goal achieved in {result.TotalTurns} turns");
}
```

## Architecture

The framework follows a modular architecture based on the agent loop:

1. **Planner**: Decides the next action (tool call, ask user, replan, stop)
2. **Executor**: Validates and executes tool calls with retry logic
3. **Critic**: Evaluates observations against goals
4. **Policy Engine**: Handles errors, retries, and fallbacks
5. **State Manager**: Tracks agent state and working memory
6. **Observation Normalizer**: Converts tool outputs to structured observations

## Components

### Planner
- `IPlanner` interface for custom implementations
- `LlmPlanner` for LLM-based planning with structured output
- Support for tool calls, user queries, replanning, and stopping

### Executor
- `IExecutor` interface for tool execution
- `ToolAdapter` with schema validation and error mapping
- Configurable retry policies and timeouts

### Critic
- `ICritic` interface for goal assessment
- `LlmCritic` for LLM-based evaluation
- Recommendations for continue, replan, clarify, or stop

### State Management
- `IStateStore` for persistence (in-memory and custom implementations)
- `StateManager` for state transitions and working memory
- Automatic memory compression when limits are reached

### Policy Engine
- Retry logic for transient failures
- Fallback tool selection
- User interaction for missing information
- Budget enforcement

## Events

The agent emits events for monitoring and debugging:

- `TurnStarted`: Fired at the beginning of each turn
- `TurnCompleted`: Fired when a turn completes with timing information
- `ToolCalled`: Fired when a tool is executed
- `UserInputRequested`: Fired when user input is needed

## Integration with Andy Ecosystem

Andy.Engine integrates seamlessly with other Andy packages:

- **Andy.Tools**: Tool registry and execution framework
- **Andy.Llm**: LLM provider abstractions
- **Andy.Model**: Shared data models
- **Andy.Context**: Context management
- **Andy.Configuration**: Configuration management

## License

Apache-2.0 License - see LICENSE file for details

## Contributing

Contributions are welcome! Please see CONTRIBUTING.md for guidelines.

## Support

For issues and questions, please use the GitHub issue tracker.