using Andy.Benchmarks;
using Andy.Engine.SweBench.Orchestration;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || SweBenchCliOptions.WantsHelp(args))
        {
            Console.WriteLine(SweBenchCliOptions.Usage);
            return args.Length == 0 ? 1 : 0;
        }

        RunContext ctx;
        try
        {
            var defaultRunId = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            ctx = SweBenchCliOptions.Parse(args, defaultRunId);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(SweBenchCliOptions.Usage);
            return 64; // EX_USAGE
        }

        try
        {
            var runner = new SweBenchRunner(ctx);
            return await runner.RunAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Run failed: {ex.Message}");
            return 1;
        }
    }
}
