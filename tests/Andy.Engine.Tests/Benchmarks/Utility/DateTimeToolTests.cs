using Andy.Engine.Benchmarks.Scenarios.Utility;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Utility;

public class DateTimeToolTests : IntegrationTestBase
{
    public DateTimeToolTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a utility assistant with access to date/time tools. When users ask about dates or times, use the datetime_tool to get the answer. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task DateTime_GetCurrentTime_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateGetCurrentTime();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("datetime_tool", result.ToolInvocations[0].ToolType);
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_ParseDate_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateParseDate();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("parse", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_AddDays_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateAddDays();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("add", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_DayOfWeek_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateDayOfWeek();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("day_of_week", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_FormatDate_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateFormatDate();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("format", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_DateDiff_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateDateDiff();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("diff", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_IsValidDate_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateIsValidDate();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("is_valid", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_SubtractHours_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateSubtractHours();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("subtract", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_IsLeapYear_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateIsLeapYear();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("is_leap_year", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_BusinessDays_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateBusinessDays();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("business_days", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_AgeCalculation_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateAgeCalculation();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("age_calculation", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_ConvertTimezone_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateConvertTimezone();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("convert_timezone", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_DaysInMonth_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateDaysInMonth();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("days_in_month", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_DayOfYear_Success(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateDayOfYear();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("day_of_year", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task DateTime_MissingOperation_HandlesError(LlmMode mode)
    {
        var scenario = DateTimeScenarios.CreateMissingOperation();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
