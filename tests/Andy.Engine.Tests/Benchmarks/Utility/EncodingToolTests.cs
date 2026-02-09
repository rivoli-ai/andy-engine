using Andy.Engine.Benchmarks.Scenarios.Utility;
using Andy.Engine.Tests.Benchmarks.Common;
using Xunit;
using Xunit.Abstractions;

namespace Andy.Engine.Tests.Benchmarks.Utility;

public class EncodingToolTests : IntegrationTestBase
{
    public EncodingToolTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string GetSystemPrompt() =>
        "You are a utility assistant with access to encoding tools. When users ask about encoding, decoding, or hashing, use the encoding_tool. After getting results, summarize them clearly.";

    [Theory]
    [LlmTestData]
    public async Task Encoding_Base64Encode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateBase64Encode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("encoding_tool", result.ToolInvocations[0].ToolType);
            Assert.Equal("base64_encode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_Base64Decode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateBase64Decode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("base64_decode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_UrlEncode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateUrlEncode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("url_encode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_Sha256Hash_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateSha256Hash();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("sha256_hash", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_UrlDecode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateUrlDecode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("url_decode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_HtmlEncode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateHtmlEncode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("html_encode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_HexEncode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateHexEncode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("hex_encode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_Md5Hash_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateMd5Hash();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("md5_hash", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_HtmlDecode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateHtmlDecode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("html_decode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_HexDecode_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateHexDecode();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("hex_decode", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_Sha1Hash_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateSha1Hash();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("sha1_hash", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_Sha512Hash_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateSha512Hash();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("sha512_hash", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_GuidGenerate_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateGuidGenerate();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("guid_generate", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_PasswordGenerate_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreatePasswordGenerate();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("password_generate", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_BcryptHash_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateBcryptHash();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("bcrypt_hash", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_BcryptVerify_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateBcryptVerify();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("bcrypt_verify", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_ValidateHash_Success(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateValidateHash();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);

        if (mode == LlmMode.Mock)
        {
            Assert.Single(result.ToolInvocations);
            Assert.Equal("validate_hash", result.ToolInvocations[0].Parameters["operation"]?.ToString());
        }
    }

    [Theory]
    [LlmTestData]
    public async Task Encoding_MissingInput_HandlesError(LlmMode mode)
    {
        var scenario = EncodingScenarios.CreateMissingInput();
        var result = await RunAsync(scenario, mode);
        AssertBenchmarkSuccess(result, scenario);
    }
}
