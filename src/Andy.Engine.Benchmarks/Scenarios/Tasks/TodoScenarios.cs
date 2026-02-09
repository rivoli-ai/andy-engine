using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Tasks;

/// <summary>
/// Provides benchmark scenarios for the todo_management and todo_executor tools
/// </summary>
public static class TodoScenarios
{
    public static List<BenchmarkScenario> CreateManagementScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateAddTodo(),
            CreateListTodos(),
            CreateCompleteTodo(),
            CreateRemoveTodo(),
            CreateSearchTodos(),
            CreateAddBatchTodos(),
            CreateClearCompletedTodos(),
            CreateUpdateProgress(),
            CreateMissingAction()
        };
    }

    public static List<BenchmarkScenario> CreateExecutorScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateAnalyzeTodos(),
            CreateDryRunTodos(),
            CreateExecuteSingle(),
            CreateExecuteAll()
        };
    }

    public static BenchmarkScenario CreateAddTodo()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-add",
            Category = "tasks",
            Description = "Add a new todo item",
            Tags = new List<string> { "tasks", "todo", "management" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Add a todo: 'Review pull request #123' with high priority" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "add",
                        ["text"] = "Review pull request #123",
                        ["priority"] = "high"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "add", "todo", "created", "Review" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateListTodos()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-list",
            Category = "tasks",
            Description = "List all todos",
            Tags = new List<string> { "tasks", "todo", "management" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "List all my todos" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "list"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "todo", "list", "item", "task", "no todo", "empty" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateCompleteTodo()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-complete",
            Category = "tasks",
            Description = "Mark a todo as complete",
            Tags = new List<string> { "tasks", "todo", "management" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Complete todo item #1" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "complete",
                        ["id"] = 1
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "complete", "done", "marked", "todo", "not found" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateRemoveTodo()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-remove",
            Category = "tasks",
            Description = "Remove a todo item",
            Tags = new List<string> { "tasks", "todo", "management" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Remove todo item #2" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "remove",
                        ["id"] = 2
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "remove", "delete", "todo", "not found" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateSearchTodos()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-search",
            Category = "tasks",
            Description = "Search for todos by keyword",
            Tags = new List<string> { "tasks", "todo", "search" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Search my todos for 'review'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "search",
                        ["query"] = "review"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "search", "review", "found", "todo", "no result", "no match" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateAddBatchTodos()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-batch-add",
            Category = "tasks",
            Description = "Add multiple todos at once",
            Tags = new List<string> { "tasks", "todo", "batch" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Add these todos: 'Write tests', 'Update docs', 'Deploy to staging'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "add_batch",
                        ["todos"] = new[] { "Write tests", "Update docs", "Deploy to staging" }
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "add", "todo", "created", "batch", "3", "Write", "Update", "Deploy" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateClearCompletedTodos()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-clear-completed",
            Category = "tasks",
            Description = "Clear all completed todos",
            Tags = new List<string> { "tasks", "todo", "management" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Clear all completed todos" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "clear_completed"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "clear", "completed", "removed", "todo", "no completed" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateUpdateProgress()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-update-progress",
            Category = "tasks",
            Description = "Update progress on a todo item",
            Tags = new List<string> { "tasks", "todo", "management" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Update the progress of todo item #1 to 50%" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "update_progress",
                        ["id"] = 1,
                        ["progress"] = 50
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "progress", "50", "update", "todo", "not found" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateMissingAction()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-todo-missing-action",
            Category = "tasks",
            Description = "Call todo_management without specifying an action",
            Tags = new List<string> { "tasks", "todo", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Use the todo management tool but don't specify what action to take" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_management",
                    MinInvocations = 0,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "action", "required", "specify", "todo" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateAnalyzeTodos()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-executor-analyze",
            Category = "tasks",
            Description = "Analyze todos for execution",
            Tags = new List<string> { "tasks", "executor", "analyze" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Analyze my todos to see which ones can be automated" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_executor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "analyze"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "analyz", "todo", "task", "automat", "no todo" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateDryRunTodos()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-executor-dry-run",
            Category = "tasks",
            Description = "Dry run todo execution",
            Tags = new List<string> { "tasks", "executor", "dry-run" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Do a dry run of executing all my todos" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_executor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "dry_run"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "dry run", "todo", "task", "execut", "no todo" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExecuteSingle()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-executor-single",
            Category = "tasks",
            Description = "Execute a single todo item",
            Tags = new List<string> { "tasks", "executor", "execute" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Execute todo item #1" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_executor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "execute",
                        ["todo_id"] = 1
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "execut", "todo", "task", "complet", "not found", "no todo" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    public static BenchmarkScenario CreateExecuteAll()
    {
        return new BenchmarkScenario
        {
            Id = "tasks-executor-all",
            Category = "tasks",
            Description = "Execute all pending todos",
            Tags = new List<string> { "tasks", "executor", "execute" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Execute all pending todos" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "todo_executor",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["action"] = "execute_all"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "execut", "todo", "task", "all", "complet", "no todo", "pending" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
