# Andy.Engine Benchmarking and Testing System

## Overview

A comprehensive framework for evaluating Andy.Engine's reliability and effectiveness as a code assistant through automated benchmarking and validation.

## Goals

- Verify correct tool invocation and outcomes
- Test multi-turn LLM interactions
- Validate context injection from various sources (prompts, MCP servers, API tools)
- Ensure code changes compile, run, and meet quality standards
- Track performance and reliability metrics over time

## Running Benchmarks

### Run All Benchmarks
```bash
dotnet run --project Andy.Benchmarks
```

### Run Specific Category
```bash
dotnet run --project Andy.Benchmarks -- --category bug-fixes
dotnet run --project Andy.Benchmarks -- --category feature-additions
dotnet run --project Andy.Benchmarks -- --category refactoring
```

### Run Specific Benchmark
```bash
dotnet run --project Andy.Benchmarks -- --id bug-fix-null-reference-001
```

### Run with Specific Reporter
```bash
# Console output (default)
dotnet run --project Andy.Benchmarks -- --reporter console

# JSON output
dotnet run --project Andy.Benchmarks -- --reporter json --output ./results.json

# HTML dashboard
dotnet run --project Andy.Benchmarks -- --reporter html --output ./results.html

# Multiple reporters
dotnet run --project Andy.Benchmarks -- --reporter console,json,html
```

### Run with Filters
```bash
# Run only scenarios with specific tag
dotnet run --project Andy.Benchmarks -- --tag single-tool

# Run only scenarios matching pattern
dotnet run --project Andy.Benchmarks -- --filter "*null-reference*"

# Exclude specific categories
dotnet run --project Andy.Benchmarks -- --exclude documentation
```

### Parallel Execution
```bash
# Run benchmarks in parallel (faster)
dotnet run --project Andy.Benchmarks -- --parallel

# Limit parallel execution
dotnet run --project Andy.Benchmarks -- --parallel --max-workers 4
```

### Comparison Mode
```bash
# Compare against baseline
dotnet run --project Andy.Benchmarks -- --compare-baseline main

# Fail on regressions
dotnet run --project Andy.Benchmarks -- --compare-baseline main --fail-on-regression
```

### Verbose Output
```bash
# Show detailed execution logs
dotnet run --project Andy.Benchmarks -- --verbose

# Show only failures
dotnet run --project Andy.Benchmarks -- --show-failures-only
```

### Using as Tests
```bash
# Run as xUnit tests (alternative approach)
dotnet test Andy.Benchmarks

# Run specific test class
dotnet test Andy.Benchmarks --filter "FullyQualifiedName~BugFixScenarios"

# Run specific test
dotnet test Andy.Benchmarks --filter "FullyQualifiedName~BugFixScenarios.NullReferenceException"
```

---

## Phase 1: Foundation and Tool Validation

### Task 1.1: Create Benchmark Framework Project
- [ ] Create `Andy.Benchmarks` project in solution
- [ ] Add necessary dependencies (xUnit, JSON parsing, process execution)
- [ ] Set up project structure:
  - [ ] `Framework/` - Core benchmark infrastructure
  - [ ] `Scenarios/` - Benchmark scenario definitions
  - [ ] `Validators/` - Validation logic
  - [ ] `Reporters/` - Result reporting
  - [ ] `TestData/` - Mini repositories and test data

### Task 1.2: Define Core Models
- [ ] Create `BenchmarkScenario` model
  - [ ] Scenario metadata (id, category, description)
  - [ ] Prompt/instruction data
  - [ ] Expected tool invocations
  - [ ] Validation criteria
  - [ ] Timeout configuration
- [ ] Create `ContextInjection` model
  - [ ] User prompts (single or multi-turn)
  - [ ] MCP server data
  - [ ] API tool responses
  - [ ] File system context
- [ ] Create `BenchmarkResult` model
  - [ ] Success/failure status
  - [ ] Tool invocations captured
  - [ ] Validation results
  - [ ] Performance metrics
  - [ ] Error details

### Task 1.3: Implement Basic Scenario Runner
- [ ] Create `ScenarioRunner` class
- [ ] Implement workspace initialization (clean state per run)
- [ ] Implement engine invocation with scenario prompt
- [ ] Capture all tool invocations made by engine
- [ ] Capture all LLM interactions (requests/responses)
- [ ] Implement basic cleanup

### Task 1.4: Single Tool Invocation Validation
- [ ] Create test scenarios for each tool type:
  - [ ] `ReadFile` - Verify file reading works correctly
  - [ ] `WriteFile` - Verify file writing with correct content
  - [ ] `EditFile` - Verify precise edits are made
  - [ ] `ListFiles` - Verify directory listing
  - [ ] `ExecuteCommand` - Verify command execution and output capture
  - [ ] `SearchFiles` - Verify file search functionality
- [ ] Implement `ToolInvocationValidator`
  - [ ] Verify correct tool was called
  - [ ] Verify parameters are correct
  - [ ] Verify tool result matches expectation
  - [ ] Verify side effects (files created, modified, etc.)
- [ ] Create 10+ simple scenarios covering all tools
- [ ] Run and validate all single-tool scenarios

---

## Phase 2: Multi-Tool and Multi-Turn Scenarios

### Task 2.1: Multi-Tool Invocation Scenarios
- [ ] Create scenarios requiring multiple tools in sequence:
  - [ ] Search → Read → Edit (find and fix pattern)
  - [ ] Read → Analyze → Write (create new file based on existing)
  - [ ] List → Read multiple → Synthesize → Write (aggregate information)
  - [ ] Execute → Parse output → Write (capture and process command output)
- [ ] Implement `ToolSequenceValidator`
  - [ ] Verify correct order of tool invocations
  - [ ] Verify data flows between tools
  - [ ] Verify intermediate states
- [ ] Create 5+ multi-tool scenarios
- [ ] Validate tool chaining works correctly

### Task 2.2: Multi-Turn Conversation Scenarios
- [ ] Create `MultiTurnScenario` model
  - [ ] Array of user prompts
  - [ ] Expected responses/actions per turn
  - [ ] Context accumulation validation
- [ ] Extend `ScenarioRunner` for multi-turn execution
  - [ ] Support sequential user inputs
  - [ ] Maintain conversation history
  - [ ] Validate context preservation across turns
- [ ] Create multi-turn scenarios:
  - [ ] Clarification flow (user asks → engine asks clarifying question → user answers → engine proceeds)
  - [ ] Iterative refinement (user requests change → engine makes change → user requests modification → engine modifies)
  - [ ] Error recovery (engine fails → user provides correction → engine retries)
  - [ ] Complex task breakdown (engine asks for confirmation at each step)
- [ ] Implement `ConversationFlowValidator`
  - [ ] Verify appropriate questions are asked
  - [ ] Verify context is maintained
  - [ ] Verify final outcome is correct
- [ ] Create 5+ multi-turn scenarios

### Task 2.3: Context Injection Framework
- [ ] Implement `ContextProvider` interface
- [ ] Create `UserPromptContextProvider`
  - [ ] Single prompt injection
  - [ ] Multi-turn prompt sequences
  - [ ] Prompts with embedded data (code snippets, error messages)
- [ ] Create `FileSystemContextProvider`
  - [ ] Inject file contents into context
  - [ ] Inject directory structures
  - [ ] Simulate file system state
- [ ] Create `McpServerContextProvider` (future)
  - [ ] Mock MCP server responses
  - [ ] Inject database query results
  - [ ] Inject external API data
- [ ] Create `ApiToolContextProvider` (future)
  - [ ] Mock API responses
  - [ ] Inject external service data
  - [ ] Simulate tool outputs
- [ ] Integrate context providers into `ScenarioRunner`

---

## Phase 3: Code Quality Validation

### Task 3.1: Compilation Validation
- [ ] Implement `CompilationValidator`
  - [ ] Run `dotnet build` on modified code
  - [ ] Capture build errors/warnings
  - [ ] Compare warning count against baseline
- [ ] Add compilation validation to all code-change scenarios
- [ ] Create scenarios that should fail compilation (negative tests)

### Task 3.2: Test Execution Validation
- [ ] Implement `TestValidator`
  - [ ] Run `dotnet test` on modified code
  - [ ] Capture test results
  - [ ] Verify all tests pass
  - [ ] Compare test count against baseline
- [ ] Create scenarios with existing tests
  - [ ] Bug fix that makes failing test pass
  - [ ] Refactoring that maintains test pass rate
  - [ ] Feature addition with new tests
- [ ] Implement test coverage tracking
  - [ ] Capture coverage before/after
  - [ ] Validate coverage doesn't decrease (configurable)

### Task 3.3: Behavioral Validation
- [ ] Implement `BehavioralValidator` base class
- [ ] Create `ConsoleAppValidator`
  - [ ] Execute console app with inputs
  - [ ] Capture stdout/stderr
  - [ ] Verify expected output patterns
  - [ ] Verify exit codes
- [ ] Create `WebAppValidator` (future)
  - [ ] Start web application
  - [ ] Make HTTP requests
  - [ ] Validate responses
  - [ ] Shutdown gracefully
- [ ] Create behavioral test scenarios:
  - [ ] Fix bug that changes program output
  - [ ] Add feature that produces new output
  - [ ] Performance optimization (measure execution time)

### Task 3.4: Code Quality Metrics
- [ ] Implement `CodeQualityValidator`
- [ ] Add linting validation
  - [ ] Run `dotnet format --verify-no-changes`
  - [ ] Ensure code follows formatting standards
- [ ] Add complexity metrics (optional)
  - [ ] Cyclomatic complexity
  - [ ] Code duplication detection
- [ ] Add diff analysis
  - [ ] Files changed count
  - [ ] Lines added/removed
  - [ ] Verify minimal, focused changes

---

## Phase 4: Scenario Library

### Task 4.1: Bug Fix Scenarios
- [ ] Null reference exception fix
- [ ] Off-by-one error correction
- [ ] Logic error in conditional
- [ ] Resource leak (file handle, connection)
- [ ] Concurrency issue (race condition)
- [ ] Error handling improvement
- [ ] Edge case handling

### Task 4.2: Feature Addition Scenarios
- [ ] Add new public method to class
- [ ] Implement interface on existing class
- [ ] Add new configuration option
- [ ] Add logging/telemetry
- [ ] Add input validation
- [ ] Add caching layer
- [ ] Extend API with new endpoint

### Task 4.3: Refactoring Scenarios
- [ ] Extract method
- [ ] Rename symbol (class, method, variable)
- [ ] Move class to different namespace
- [ ] Simplify complex conditional
- [ ] Replace conditional with polymorphism
- [ ] Consolidate duplicate code
- [ ] Improve naming consistency

### Task 4.4: Testing Scenarios
- [ ] Add unit tests for untested method
- [ ] Increase code coverage to target %
- [ ] Add integration test
- [ ] Add edge case tests
- [ ] Refactor tests for better readability
- [ ] Mock external dependencies in tests

### Task 4.5: Documentation Scenarios
- [ ] Add XML documentation comments
- [ ] Update README with new feature
- [ ] Add code examples to docs
- [ ] Document API breaking change
- [ ] Add inline comments for complex logic

### Task 4.6: Multi-File Scenarios
- [ ] Refactor across multiple files
- [ ] Add feature spanning multiple classes
- [ ] Rename symbol used in multiple files
- [ ] Update interface and all implementations
- [ ] Reorganize project structure

### Task 4.7: Context Injection Scenarios
- [ ] User provides error message, engine fixes issue
- [ ] User provides API documentation, engine implements client
- [ ] Engine queries MCP server for database schema, generates models
- [ ] Engine calls API tool to get current weather, uses in calculation
- [ ] Multi-turn: user provides partial info, engine asks for more, user provides, engine completes

---

## Phase 5: Reporting and CI/CD Integration

### Task 5.1: Result Reporting
- [ ] Implement `JsonReporter`
  - [ ] Export results in structured JSON format
  - [ ] Include all metrics and validation details
- [ ] Implement `HtmlReporter`
  - [ ] Generate visual dashboard
  - [ ] Show pass/fail rates by category
  - [ ] Display detailed results per scenario
  - [ ] Include charts and graphs
- [ ] Implement `ConsoleReporter`
  - [ ] Real-time progress output
  - [ ] Summary statistics
  - [ ] Failed scenario details

### Task 5.2: Metrics Collection
- [ ] Implement `MetricsCollector`
- [ ] Track performance metrics:
  - [ ] Time per scenario
  - [ ] Total tokens used
  - [ ] API calls made
  - [ ] Tool invocations count
- [ ] Track accuracy metrics:
  - [ ] Pass rate by category
  - [ ] Compilation success rate
  - [ ] Test pass rate
  - [ ] Behavioral correctness rate
- [ ] Track code quality metrics:
  - [ ] Average files changed
  - [ ] Average lines changed
  - [ ] Complexity delta
  - [ ] Coverage delta

### Task 5.3: Regression Tracking
- [ ] Implement baseline comparison
  - [ ] Store benchmark results per commit/version
  - [ ] Compare current run against baseline
  - [ ] Flag regressions
- [ ] Create regression report
  - [ ] Show scenarios that regressed
  - [ ] Show scenarios that improved
  - [ ] Highlight new failures

### Task 5.4: CI/CD Integration
- [ ] Create benchmark runner CLI
  - [ ] Support filtering scenarios by category/tag
  - [ ] Support parallel execution
  - [ ] Exit with error code on failure
- [ ] Create GitHub Actions workflow
  - [ ] Run benchmarks on every PR
  - [ ] Compare against main branch
  - [ ] Post results as PR comment
  - [ ] Fail PR on regressions (configurable)
- [ ] Create scheduled benchmark runs
  - [ ] Daily full suite execution
  - [ ] Track trends over time
  - [ ] Alert on degradation

---

## Phase 6: Advanced Features

### Task 6.1: Real-World Scenarios
- [ ] Source scenarios from actual GitHub issues
- [ ] Import scenarios from open-source projects
- [ ] Create scenarios from user bug reports
- [ ] Validate against production codebases

### Task 6.2: Performance Benchmarks
- [ ] Measure time to completion
- [ ] Measure token efficiency
- [ ] Compare against baseline models
- [ ] Optimize for speed vs. quality tradeoffs

### Task 6.3: Reliability Testing
- [ ] Run scenarios multiple times (consistency check)
- [ ] Test with different random seeds
- [ ] Test with different LLM parameters (temperature, etc.)
- [ ] Measure variance in outcomes

### Task 6.4: Interactive Scenarios
- [ ] Simulate user interruptions
- [ ] Test cancel/retry functionality
- [ ] Test error recovery flows
- [ ] Validate undo/rollback capabilities

### Task 6.5: Semantic Validation
- [ ] LLM-based code review
  - [ ] Does the change solve the problem?
  - [ ] Is the approach reasonable?
  - [ ] Are there better alternatives?
- [ ] Architecture validation
  - [ ] Does it follow project patterns?
  - [ ] Are dependencies appropriate?
  - [ ] Is it maintainable?

---

## Success Criteria

### Phase 1 Complete When:
- [ ] Benchmark framework project exists and builds
- [ ] All core models defined
- [ ] Basic scenario runner implemented
- [ ] 10+ single-tool scenarios pass validation

### Phase 2 Complete When:
- [ ] 5+ multi-tool scenarios pass
- [ ] 5+ multi-turn conversation scenarios pass
- [ ] Context injection framework operational
- [ ] Tool sequencing validated

### Phase 3 Complete When:
- [ ] Compilation validation works for all scenarios
- [ ] Test execution validation works
- [ ] Behavioral validation works for console apps
- [ ] Code quality metrics collected

### Phase 4 Complete When:
- [ ] 50+ scenarios across all categories
- [ ] All scenario types represented
- [ ] Context injection scenarios working

### Phase 5 Complete When:
- [ ] All three reporters implemented (JSON, HTML, Console)
- [ ] Metrics collected and tracked
- [ ] Regression tracking functional
- [ ] CI/CD integration complete

### Phase 6 Complete When:
- [ ] Real-world scenarios imported and passing
- [ ] Performance benchmarks established
- [ ] Reliability metrics tracked
- [ ] Interactive scenarios validated

---

## Example Scenario Definition

```json
{
  "id": "bug-fix-null-reference-001",
  "category": "bug-fixes",
  "description": "Fix null reference exception in user validation",
  "workspace": {
    "type": "git-clone",
    "source": "./test-data/sample-project"
  },
  "context": {
    "prompts": [
      "The application crashes with a NullReferenceException in UserValidator.Validate() when the user's email is null. Fix this issue."
    ]
  },
  "expected_tools": [
    {
      "type": "ReadFile",
      "path_pattern": "**/UserValidator.cs"
    },
    {
      "type": "EditFile",
      "path_pattern": "**/UserValidator.cs"
    }
  ],
  "validation": {
    "compilation": {
      "must_succeed": true
    },
    "tests": {
      "must_pass": true,
      "min_coverage": 80
    },
    "behavioral": {
      "type": "unit-tests",
      "specific_test": "UserValidator_ValidateWithNullEmail_ShouldNotThrow"
    },
    "diff": {
      "max_files_changed": 2,
      "expected_files": ["UserValidator.cs"]
    }
  },
  "timeout_seconds": 120
}
```

---

## Notes

- Start simple: Get Phase 1 working before moving to complex scenarios
- Iterate quickly: Add scenarios as you discover engine weaknesses
- Automate everything: Manual validation doesn't scale
- Track trends: Regression detection is as important as current pass rate
- Real-world focus: Eventually scenarios should reflect actual user needs

### Tool Permissions Considerations

Tool permissions will affect benchmark execution but can be addressed later:

- **Initial approach**: Run benchmarks with all permissions granted (unrestricted mode)
- **Future enhancement**: Add permission scenarios to test:
  - Engine requests permission appropriately
  - Engine handles permission denials gracefully
  - Engine adapts when permissions are restricted
- **Benchmark isolation**: Permissions should not interfere with validation - scenarios run in isolated environments
- **Testing strategy**: Add dedicated permission-testing scenarios in Phase 6 once core functionality is stable
