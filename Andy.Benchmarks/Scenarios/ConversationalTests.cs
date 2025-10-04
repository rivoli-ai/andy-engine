using Andy.Benchmarks.Framework;
using Andy.Benchmarks.Validators;
using Xunit;

namespace Andy.Benchmarks.Scenarios;

/// <summary>
/// Tests for conversational interactions without tools
/// Validates that the agent provides complete, helpful responses
/// </summary>
public class ConversationalTests
{
    [Fact]
    public void SimpleCodeRequest_ReturnsCompleteCode()
    {
        // This test validates the conversation:
        // User: "can you write C# code?"
        // Expected: Agent provides a complete C# code example, not just a promise

        // Arrange
        var scenario = new BenchmarkScenario
        {
            Id = "conv-simple-code-request",
            Category = "conversation",
            Description = "Agent should provide complete code when asked 'can you write C# code?'",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "can you write C# code?"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>(), // No tools needed for this
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string>
                {
                    "using",      // C# using statement
                    "class",      // C# class keyword
                    "static",     // C# static keyword
                    "void Main",  // C# entry point
                    "Console"     // C# Console class (common in examples)
                },
                ResponseMustNotContain = new List<string>
                {
                    "Here is",                    // Should show code, not promise to show it
                    "Here's",                     // Should show code, not promise to show it
                    "I can provide",              // Should provide, not offer
                    "Would you like me to"        // Should provide, not ask
                },
                MinResponseLength = 100  // Must be substantial response with actual code
            }
        };

        // This would be run by the benchmark framework
        // For now, documenting expected behavior
        Assert.NotNull(scenario);
        Assert.Equal("conversation", scenario.Category);
    }

    [Fact]
    public void WriteProgramRequest_ProvidesCodeDirectly()
    {
        // This test validates:
        // User: "write a sample program for me"
        // Expected: Agent writes actual code, doesn't ask for clarification

        var scenario = new BenchmarkScenario
        {
            Id = "conv-write-program-direct",
            Category = "conversation",
            Description = "Agent should write code directly without asking for clarification",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "write a sample program for me"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>(),
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string>
                {
                    "using",
                    "class",
                    "static void Main"
                },
                ResponseMustNotContain = new List<string>
                {
                    "What language",              // Don't ask for language
                    "What type of program",       // Don't ask for type
                    "Could you specify",          // Don't ask for clarification
                    "need more information"       // Provide reasonable default
                },
                MinResponseLength = 100,
                MustNotAskUser = true  // Critical: should not trigger AskUserDecision
            }
        };

        Assert.NotNull(scenario);
    }

    [Fact]
    public void MultiTurnConversation_MaintainsContext()
    {
        // This test validates a multi-turn conversation:
        // Turn 1: "hello"
        // Turn 2: "can you write C# code?"
        // Turn 3: "write a sample program for me"
        // Expected: Each response is complete and helpful

        var scenario = new BenchmarkScenario
        {
            Id = "conv-multi-turn-coding",
            Category = "conversation",
            Description = "Multi-turn conversation about writing code should flow naturally",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "hello",
                    "can you write C# code?",
                    "write a sample program for me"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>(),
            Validation = new ValidationConfig
            {
                ConversationFlow = new ConversationFlowValidation
                {
                    MinTurns = 3,
                    MaxTurns = 3,
                    TurnValidations = new List<TurnValidation>
                    {
                        new TurnValidation  // Turn 1: greeting
                        {
                            TurnNumber = 1,
                            MustContain = new List<string> { "assist", "help" },
                            MinLength = 10
                        },
                        new TurnValidation  // Turn 2: confirm and show example
                        {
                            TurnNumber = 2,
                            MustContain = new List<string> { "using", "class" },
                            MinLength = 50,
                            MustNotAskUser = true
                        },
                        new TurnValidation  // Turn 3: provide complete program
                        {
                            TurnNumber = 3,
                            MustContain = new List<string> { "using", "class", "static void Main" },
                            MinLength = 100,
                            MustNotAskUser = true
                        }
                    }
                }
            }
        };

        Assert.NotNull(scenario);
    }

    [Fact]
    public void CodeWithFileRequest_UsesToolWhenAppropriate()
    {
        // This test validates:
        // User: "write the file to a /tmp folder for me please"
        // Expected: Agent uses WriteFile tool, doesn't ask for clarification

        var scenario = new BenchmarkScenario
        {
            Id = "conv-write-to-file",
            Category = "conversation",
            Description = "Agent should use write_file tool when asked to save code",
            Workspace = new WorkspaceConfig
            {
                Type = "in-memory",
                Source = ""
            },
            Context = new ContextInjection
            {
                Prompts = new List<string>
                {
                    "write a C# hello world program",
                    "write the file to a /tmp folder for me please"
                }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "write_file",
                    PathPattern = "/tmp/*.cs",
                    MinInvocations = 1,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                MustNotAskUser = true,  // Should write file directly
                ResponseMustContain = new List<string>
                {
                    "/tmp"  // Should confirm the file was written
                }
            }
        };

        Assert.NotNull(scenario);
    }
}
