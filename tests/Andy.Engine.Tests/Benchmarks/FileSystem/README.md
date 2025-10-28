# File System Benchmark Tests

This directory contains comprehensive test scenarios for file system operations via the Andy Engine.

## Test Structure

### Scenario Definition Tests (Current)
The test files (ListDirectoryTests.cs, ReadFileTests.cs, etc.) define **BenchmarkScenario** objects that specify:
- User prompts
- Expected tool invocations
- Required parameters
- Validation criteria
- Safety guards and safeguards

These scenarios are complete specifications that validate:
1. Scenario structure is well-formed
2. Expected tools are correctly defined
3. Parameters match tool specifications
4. Safety requirements are documented

### Integration Testing (Future)
To actually execute these scenarios through the engine:

1. **Use ScenarioRunner**: Pass scenarios to `ScenarioRunner.RunAsync()`
2. **Provide Agent**: Create an Agent with:
   - LLM provider (real or mocked)
   - Tool registry with file system tools
   - Tool executor
3. **Validate Results**: Check `BenchmarkResult` for:
   - Tool invocation count
   - Correct parameters
   - Successful execution

Example:
```csharp
var agent = AgentBuilder.Create()
    .WithLlmProvider(llmProvider)
    .WithToolRegistry(toolRegistry)
    .WithToolExecutor(toolExecutor)
    .Build();

var runner = new ScenarioRunner(agent, workspaceRoot);
var result = await runner.RunAsync(scenario);

Assert.True(result.Success);
Assert.Single(result.ToolInvocations);
```

## Test Coverage

### ListDirectoryTests (5 scenarios)
- Basic directory listing
- Recursive listing with subdirectories
- Pattern filtering (*.txt)
- Hidden file inclusion
- Sorting by various criteria

### ReadFileTests (6 scenarios)
- Small text file reading
- Encoding specification (UTF-8, unicode)
- Partial file reading (line ranges)
- Multiple file reads
- Size limit enforcement
- JSON file parsing

### WriteFileTests (7 scenarios)
- ⚠️ **Guard Test**: Only write when explicitly requested
- ⚠️ **Guard Test**: No write without explicit request
- Backup creation before overwrite
- Append mode
- Encoding specification
- Nested directory auto-creation
- Overwrite protection

### CopyFileTests (6 scenarios)
- Simple file copy with content preservation
- Overwrite existing files
- Recursive directory copying
- Timestamp preservation
- Pattern exclusion
- Copy to directory

### MoveFileTests (7 scenarios)
- Simple file move/rename
- Rename in same directory
- Move to subdirectory
- Overwrite with confirmation
- Backup before overwrite
- Entire directory move
- Auto-create destination path

### DeleteFileTests (10 scenarios)
- Simple file deletion
- Default safety safeguards
- Empty directory deletion
- Recursive directory deletion
- Backup before deletion
- Size limit enforcement
- Pattern exclusion
- Read-only file deletion (force flag)
- Custom backup location
- Multiple file deletion

## Key Features

- **41 Total Test Scenarios**: Comprehensive coverage of all file system operations
- **Safety-Focused**: Special emphasis on write_file and delete_file guards
- **Isolated Environments**: Each test uses temp directories with automatic cleanup
- **Consistent with andy-tools**: Parameters and behaviors match the tool implementations
- **LLM Integration Ready**: Scenarios test the full Agent → LLM → Tool chain

## Running Tests

Current tests validate scenario definitions:
```bash
dotnet test --filter "FullyQualifiedName~FileSystem"
```

For full integration testing with engine execution, additional infrastructure is needed (see Integration Testing section above).
