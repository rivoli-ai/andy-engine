using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Utility;

/// <summary>
/// Provides benchmark scenarios for the datetime_tool
/// </summary>
public static class DateTimeScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateGetCurrentTime(),
            CreateParseDate(),
            CreateAddDays(),
            CreateDayOfWeek(),
            CreateFormatDate(),
            CreateDateDiff(),
            CreateIsValidDate(),
            CreateSubtractHours(),
            CreateIsLeapYear(),
            CreateBusinessDays(),
            CreateAgeCalculation(),
            CreateConvertTimezone(),
            CreateDaysInMonth(),
            CreateDayOfYear(),
            CreateMissingOperation()
        };
    }

    /// <summary>
    /// Get the current date and time
    /// </summary>
    public static BenchmarkScenario CreateGetCurrentTime()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-now",
            Category = "utility",
            Description = "Get current date and time",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "What is the current date and time?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "now"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "202", "date", "time" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Parse a date string
    /// </summary>
    public static BenchmarkScenario CreateParseDate()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-parse",
            Category = "utility",
            Description = "Parse an ISO date string",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Parse the date '2024-06-15T10:30:00'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "parse",
                        ["date_input"] = "2024-06-15T10:30:00"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "2024", "June", "15" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Add days to a date
    /// </summary>
    public static BenchmarkScenario CreateAddDays()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-add",
            Category = "utility",
            Description = "Add 30 days to a date",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Add 30 days to the date 2024-01-01" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "add",
                        ["date_input"] = "2024-01-01",
                        ["amount"] = 30,
                        ["unit"] = "days"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "2024-01-31", "January 31", "Jan 31" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Get day of week for a date
    /// </summary>
    public static BenchmarkScenario CreateDayOfWeek()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-day-of-week",
            Category = "utility",
            Description = "Get the day of week for a specific date",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "What day of the week is 2024-12-25?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "day_of_week",
                        ["date_input"] = "2024-12-25"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Wednesday", "wednesday" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Format a date with a target format string
    /// </summary>
    public static BenchmarkScenario CreateFormatDate()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-format",
            Category = "utility",
            Description = "Format a date to a specific output format",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Format the date 2024-06-15 as 'dd/MM/yyyy'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "format",
                        ["date_input"] = "2024-06-15",
                        ["target_format"] = "dd/MM/yyyy"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "15/06/2024", "15", "06", "2024" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Calculate difference between two dates
    /// </summary>
    public static BenchmarkScenario CreateDateDiff()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-diff",
            Category = "utility",
            Description = "Calculate the difference between two dates",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "How many days between 2024-01-01 and 2024-03-01?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "diff",
                        ["date_input"] = "2024-01-01",
                        ["end_date"] = "2024-03-01",
                        ["unit"] = "days"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "60", "59", "days", "difference" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Validate whether a date string is valid
    /// </summary>
    public static BenchmarkScenario CreateIsValidDate()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-is-valid",
            Category = "utility",
            Description = "Check if a date string is valid",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Is '2024-02-30' a valid date?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "is_valid",
                        ["date_input"] = "2024-02-30"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "invalid", "not valid", "false", "not a valid", "February" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Subtract time from a date
    /// </summary>
    public static BenchmarkScenario CreateSubtractHours()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-subtract",
            Category = "utility",
            Description = "Subtract hours from a date/time",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Subtract 6 hours from 2024-01-15T12:00:00" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "subtract",
                        ["date_input"] = "2024-01-15T12:00:00",
                        ["amount"] = 6,
                        ["unit"] = "hours"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "06:00", "6:00", "2024-01-15" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Check if a year is a leap year
    /// </summary>
    public static BenchmarkScenario CreateIsLeapYear()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-is-leap-year",
            Category = "utility",
            Description = "Check if a year is a leap year",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Is the year 2024 a leap year?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "is_leap_year",
                        ["date_input"] = "2024"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "true", "yes", "leap", "2024" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Count business days between two dates
    /// </summary>
    public static BenchmarkScenario CreateBusinessDays()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-business-days",
            Category = "utility",
            Description = "Count business days between two dates",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "How many business days are between 2024-01-01 and 2024-01-15?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "business_days",
                        ["date_input"] = "2024-01-01",
                        ["end_date"] = "2024-01-15"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "10", "business", "days", "working" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Calculate age from a birthdate
    /// </summary>
    public static BenchmarkScenario CreateAgeCalculation()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-age",
            Category = "utility",
            Description = "Calculate age from a birthdate",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Calculate the age for someone born on 1990-06-15" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "age_calculation",
                        ["date_input"] = "1990-06-15"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "age", "years", "3" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Convert between timezones
    /// </summary>
    public static BenchmarkScenario CreateConvertTimezone()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-convert-timezone",
            Category = "utility",
            Description = "Convert a time between timezones",
            Tags = new List<string> { "utility", "datetime", "timezone" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Convert 2024-06-15T10:00:00 from UTC to America/New_York timezone" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "convert_timezone",
                        ["date_input"] = "2024-06-15T10:00:00",
                        ["timezone"] = "UTC",
                        ["target_timezone"] = "America/New_York"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "06:00", "6:00", "New York", "EDT", "EST", "Eastern", "converted" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Get number of days in a month
    /// </summary>
    public static BenchmarkScenario CreateDaysInMonth()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-days-in-month",
            Category = "utility",
            Description = "Get the number of days in a specific month",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "How many days are in February 2024?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "days_in_month",
                        ["date_input"] = "2024-02"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "29", "days", "February" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Get day of year for a date
    /// </summary>
    public static BenchmarkScenario CreateDayOfYear()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-day-of-year",
            Category = "utility",
            Description = "Get the day number within the year",
            Tags = new List<string> { "utility", "datetime", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "What day of the year is 2024-03-01?" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "day_of_year",
                        ["date_input"] = "2024-03-01"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // 2024 is a leap year, so March 1 = day 61
                ResponseMustContainAny = new List<string> { "61", "day", "year" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Missing operation parameter should produce an error
    /// </summary>
    public static BenchmarkScenario CreateMissingOperation()
    {
        return new BenchmarkScenario
        {
            Id = "util-datetime-missing-op",
            Category = "utility",
            Description = "Call datetime_tool without specifying an operation",
            Tags = new List<string> { "utility", "datetime", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Use the datetime tool but do not specify any operation" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "datetime_tool",
                    MinInvocations = 0,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "operation", "required", "parameter" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
