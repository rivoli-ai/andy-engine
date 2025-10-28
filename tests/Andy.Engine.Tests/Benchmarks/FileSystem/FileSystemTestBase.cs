using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Tests.Benchmarks.FileSystem;

/// <summary>
/// Base class for file system benchmark tests providing common test infrastructure
/// </summary>
public abstract class FileSystemTestBase : IDisposable
{
    protected readonly string TestDirectory;
    protected readonly string TestWorkspaceRoot;

    protected FileSystemTestBase()
    {
        // Create isolated test directory
        TestWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"andy_fs_test_{Guid.NewGuid():N}");
        TestDirectory = Path.Combine(TestWorkspaceRoot, "workspace");
        Directory.CreateDirectory(TestDirectory);
    }

    /// <summary>
    /// Creates a test file with specified content
    /// </summary>
    protected string CreateTestFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(TestDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    /// Creates a test directory
    /// </summary>
    protected string CreateTestDirectory(string relativePath)
    {
        var fullPath = Path.Combine(TestDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a test file structure for scenarios
    /// </summary>
    protected void CreateTestFileStructure()
    {
        // Create some sample files and directories
        CreateTestFile("readme.txt", "This is a readme file.\nIt has multiple lines.\nLine 3 here.");
        CreateTestFile("data.json", "{\"name\": \"test\", \"value\": 42}");
        CreateTestFile("script.sh", "#!/bin/bash\necho 'Hello World'");

        // Create subdirectories with files
        CreateTestDirectory("documents");
        CreateTestFile("documents/report.txt", "Annual Report 2024\n\nSection 1: Overview");
        CreateTestFile("documents/notes.md", "# Notes\n\n- Item 1\n- Item 2");

        CreateTestDirectory("source");
        CreateTestFile("source/program.cs", "using System;\n\nclass Program\n{\n    static void Main() { }\n}");

        // Hidden file (Unix-style)
        CreateTestFile(".hidden", "Hidden file content");
    }

    /// <summary>
    /// Verifies a file exists with expected content
    /// </summary>
    protected bool VerifyFileContent(string relativePath, string expectedContent)
    {
        var fullPath = Path.Combine(TestDirectory, relativePath);
        if (!File.Exists(fullPath)) return false;

        var actualContent = File.ReadAllText(fullPath);
        return actualContent == expectedContent;
    }

    /// <summary>
    /// Verifies a file exists
    /// </summary>
    protected bool FileExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDirectory, relativePath);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Verifies a directory exists
    /// </summary>
    protected bool DirectoryExists(string relativePath)
    {
        var fullPath = Path.Combine(TestDirectory, relativePath);
        return Directory.Exists(fullPath);
    }

    /// <summary>
    /// Gets the full path for a relative path
    /// </summary>
    protected string GetFullPath(string relativePath)
    {
        return Path.Combine(TestDirectory, relativePath);
    }

    /// <summary>
    /// Creates a workspace configuration for in-memory testing
    /// </summary>
    protected WorkspaceConfig CreateWorkspaceConfig()
    {
        return new WorkspaceConfig
        {
            Type = "directory-copy",
            Source = TestDirectory
        };
    }

    /// <summary>
    /// Creates a validation configuration for tool invocation checking
    /// </summary>
    protected ValidationConfig CreateValidationConfig(
        List<string>? mustContain = null,
        List<string>? mustNotContain = null)
    {
        return new ValidationConfig
        {
            ResponseMustContain = mustContain ?? new List<string>(),
            ResponseMustNotContain = mustNotContain ?? new List<string>(),
            MustNotAskUser = true
        };
    }

    /// <summary>
    /// Verifies tool invocation count for a specific tool type
    /// </summary>
    protected void AssertToolInvocationCount(
        BenchmarkResult result,
        string toolType,
        int expectedCount)
    {
        var actualCount = result.ToolInvocations.Count(t => t.ToolType == toolType);
        if (actualCount != expectedCount)
        {
            throw new Exception(
                $"Expected {expectedCount} invocations of {toolType}, but found {actualCount}");
        }
    }

    /// <summary>
    /// Verifies tool invocation count within a range
    /// </summary>
    protected void AssertToolInvocationCountInRange(
        BenchmarkResult result,
        string toolType,
        int minCount,
        int maxCount)
    {
        var actualCount = result.ToolInvocations.Count(t => t.ToolType == toolType);
        if (actualCount < minCount || actualCount > maxCount)
        {
            throw new Exception(
                $"Expected {minCount}-{maxCount} invocations of {toolType}, but found {actualCount}");
        }
    }

    /// <summary>
    /// Verifies tool was invoked with specific parameter
    /// </summary>
    protected void AssertToolParameterContains(
        BenchmarkResult result,
        string toolType,
        string parameterName,
        string expectedValue)
    {
        var invocations = result.ToolInvocations
            .Where(t => t.ToolType == toolType)
            .ToList();

        if (!invocations.Any())
        {
            throw new Exception($"No invocations of {toolType} found");
        }

        var found = invocations.Any(inv =>
            inv.Parameters?.TryGetValue(parameterName, out var value) == true &&
            value?.ToString()?.Contains(expectedValue) == true);

        if (!found)
        {
            throw new Exception(
                $"No invocation of {toolType} found with parameter {parameterName} containing '{expectedValue}'");
        }
    }

    public virtual void Dispose()
    {
        try
        {
            if (Directory.Exists(TestWorkspaceRoot))
            {
                Directory.Delete(TestWorkspaceRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
