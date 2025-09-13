namespace Andy.Engine.Tests;

public class EngineInfoTests
{
    [Fact]
    public void Name_Is_AndyEngine()
    {
        Assert.Equal("Andy.Engine", Andy.Engine.EngineInfo.Name);
    }
}