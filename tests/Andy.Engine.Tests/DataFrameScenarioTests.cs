using Andy.Data;
using Andy.Engine;
using Andy.Model.Llm;
using Andy.Model.Model;
using Andy.Tools;
using Andy.Tools.Core;
using Andy.Tools.Data;
using Andy.Tools.Framework;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Andy.Engine.Tests;

/// <summary>
/// End-to-end scenario: a scripted model drives the real dataframe_* tools through SimpleAgent over
/// three related CSVs (sales / products / regions) — load, group-by (array-of-objects aggregations),
/// and join. This is the regression for the tool-argument bug: SimpleAgent must hand the tools real
/// nested dictionaries/lists, not raw JSON strings. Asserts on the shared dataset catalog, which the
/// tools and the test share.
/// </summary>
public class DataFrameScenarioTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"andy_df_scn_{Guid.NewGuid():N}");

    public DataFrameScenarioTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "sales.csv"),
            "order_id,region,product_id,amount\n" +
            "1,NA,P1,100\n2,EU,P2,200\n3,APAC,P1,50\n4,LATAM,P3,300\n" +
            "5,NA,P2,150\n6,EU,P1,80\n7,APAC,P3,120\n8,LATAM,P2,90\n");
        File.WriteAllText(Path.Combine(_dir, "products.csv"),
            "product_id,product_name,category\nP1,Widget,Hardware\nP2,Gadget,Hardware\nP3,Gizmo,Electronics\n");
        File.WriteAllText(Path.Combine(_dir, "regions.csv"),
            "region,country\nNA,USA\nEU,Germany\nAPAC,Japan\nLATAM,Brazil\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string Path_(string f) => Path.Combine(_dir, f).Replace("\\", "\\\\");

    private static LlmResponse ToolCall(string name, string argsJson) => new()
    {
        AssistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = "",
            ToolCalls = new List<ToolCall> { new() { Id = "c_" + name, Name = name, ArgumentsJson = argsJson } },
        },
    };

    private static LlmResponse Stop() => new()
    {
        AssistantMessage = new Message { Role = Role.Assistant, Content = "Done." },
        FinishReason = "stop",
    };

    [Fact]
    public async Task Three_dataset_scenario_runs_load_groupby_and_join_through_the_agent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAndyTools();
        services.AddAndyDataFrameTools();
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IToolLifecycleManager>().InitializeAsync();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var executor = provider.GetRequiredService<IToolExecutor>();
        var catalog = provider.GetRequiredService<IDatasetCatalog>();

        // The scripted model: load each CSV, group sales by region with an array-of-objects
        // aggregation list, join sales with products, then stop.
        var llm = new Mock<ILlmProvider>();
        llm.SetupSequence(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCall("dataframe_load_csv", $"{{\"path\":\"{Path_("sales.csv")}\",\"dataset_id\":\"sales\"}}"))
            .ReturnsAsync(ToolCall("dataframe_load_csv", $"{{\"path\":\"{Path_("products.csv")}\",\"dataset_id\":\"products\"}}"))
            .ReturnsAsync(ToolCall("dataframe_load_csv", $"{{\"path\":\"{Path_("regions.csv")}\",\"dataset_id\":\"regions\"}}"))
            .ReturnsAsync(ToolCall("dataframe_group_by",
                "{\"dataset_id\":\"sales\",\"into\":\"by_region\",\"group_by\":[\"region\"]," +
                "\"aggregations\":[{\"column\":\"amount\",\"function\":\"sum\",\"alias\":\"total\"}," +
                "{\"column\":\"*\",\"function\":\"count\",\"alias\":\"orders\"}]}"))
            .ReturnsAsync(ToolCall("dataframe_join",
                "{\"left\":\"sales\",\"right\":\"products\",\"into\":\"enriched\",\"how\":\"inner\",\"on\":[\"product_id\"]}"))
            .ReturnsAsync(Stop());

        var agent = new SimpleAgent(llm.Object, registry, executor, systemPrompt: "system", maxTurns: 10);
        await agent.ProcessMessageAsync("Load the three CSVs, total/average amount by region, and join sales with products.");

        // Loads registered all three sources.
        Assert.Equal(8L, catalog.Get("sales")?.RowCount);
        Assert.Equal(3L, catalog.Get("products")?.RowCount);
        Assert.Equal(4L, catalog.Get("regions")?.RowCount);

        // group_by with array-of-objects aggregations succeeded → one row per region with total+orders.
        var byRegion = catalog.Get("by_region");
        Assert.NotNull(byRegion);
        Assert.Equal(4L, byRegion!.RowCount);
        var cols = byRegion.Schema.Select(c => c.Name).ToList();
        Assert.Contains("total", cols);
        Assert.Contains("orders", cols);

        // join with an 'on' array succeeded → all 8 rows match a product.
        Assert.Equal(8L, catalog.Get("enriched")?.RowCount);
    }
}
