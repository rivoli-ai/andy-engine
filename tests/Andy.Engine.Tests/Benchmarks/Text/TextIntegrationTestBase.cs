using Andy.Engine.Tests.Benchmarks.Common;
using Andy.Tools.Framework;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Text;

/// <summary>
/// Base class for text tool integration tests that need filesystem access
/// </summary>
public abstract class TextIntegrationTestBase : IntegrationTestBase
{
    protected readonly string TestDirectory;
    private readonly string _testWorkspaceRoot;

    protected TextIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
        _testWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"andy_text_test_{Guid.NewGuid():N}");
        TestDirectory = Path.Combine(_testWorkspaceRoot, "workspace");
        Directory.CreateDirectory(TestDirectory);
    }

    protected override void ConfigureToolOptions(ToolFrameworkOptions options)
    {
        options.DefaultPermissions.AllowedPaths = new HashSet<string> { TestDirectory };
    }

    protected override string GetSystemPrompt() =>
        "You are a text processing assistant with access to tools for searching, replacing, and formatting text. When users ask you to perform text operations, use the provided tools. After getting results, summarize them clearly.";

    protected override string GetWorkingDirectory() => TestDirectory;

    protected string CreateTestFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(TestDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    protected void CreateTextTestFiles()
    {
        CreateTestFile("readme.txt", "This is a readme file.\nIt has multiple lines.\nLine 3 here.");
        CreateTestFile("data.json", "{\"name\": \"test\", \"value\": 42}");
        CreateTestFile("script.sh", "#!/bin/bash\necho 'Hello World'");
        CreateTestFile("documents/report.txt", "Annual Report 2024\n\nSection 1: Overview");
        CreateTestFile("documents/notes.md", "# Notes\n\n- Item 1\n- Item 2");
    }

    public override void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkspaceRoot))
                Directory.Delete(_testWorkspaceRoot, recursive: true);
        }
        catch { }
        base.Dispose();
    }
}
