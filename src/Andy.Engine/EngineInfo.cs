namespace Andy.Engine;

public static class EngineInfo
{
    public static string Name => "Andy.Engine";
    public static string Version => typeof(EngineInfo).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
