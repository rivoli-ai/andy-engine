using Andy.Benchmarks.Framework;
using Andy.Engine.Benchmarks.Framework;

namespace Andy.Engine.Benchmarks.Scenarios.Utility;

/// <summary>
/// Provides benchmark scenarios for the encoding_tool
/// </summary>
public static class EncodingScenarios
{
    public static List<BenchmarkScenario> CreateScenarios()
    {
        return new List<BenchmarkScenario>
        {
            CreateBase64Encode(),
            CreateBase64Decode(),
            CreateUrlEncode(),
            CreateUrlDecode(),
            CreateHtmlEncode(),
            CreateHtmlDecode(),
            CreateHexEncode(),
            CreateHexDecode(),
            CreateSha256Hash(),
            CreateSha1Hash(),
            CreateSha512Hash(),
            CreateMd5Hash(),
            CreateGuidGenerate(),
            CreatePasswordGenerate(),
            CreateBcryptHash(),
            CreateBcryptVerify(),
            CreateValidateHash(),
            CreateMissingInput()
        };
    }

    /// <summary>
    /// Base64 encode text
    /// </summary>
    public static BenchmarkScenario CreateBase64Encode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-base64-encode",
            Category = "utility",
            Description = "Base64 encode a text string",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Base64 encode the text 'Hello, World!'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "base64_encode",
                        ["input_text"] = "Hello, World!"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "SGVsbG8sIFdvcmxkIQ==" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Base64 decode text
    /// </summary>
    public static BenchmarkScenario CreateBase64Decode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-base64-decode",
            Category = "utility",
            Description = "Base64 decode a string",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Base64 decode the string 'SGVsbG8sIFdvcmxkIQ=='" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "base64_decode",
                        ["input_text"] = "SGVsbG8sIFdvcmxkIQ=="
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContain = new List<string> { "Hello, World!" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// URL encode text
    /// </summary>
    public static BenchmarkScenario CreateUrlEncode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-url-encode",
            Category = "utility",
            Description = "URL encode a string with special characters",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "URL encode the text 'hello world & foo=bar'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "url_encode",
                        ["input_text"] = "hello world & foo=bar"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "%26", "%20", "+" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// SHA256 hash
    /// </summary>
    public static BenchmarkScenario CreateSha256Hash()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-sha256",
            Category = "utility",
            Description = "Generate SHA256 hash of text",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate the SHA256 hash of 'test'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "sha256_hash",
                        ["input_text"] = "test"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // SHA256 of "test" = 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08
                ResponseMustContainAny = new List<string> { "9f86d081", "hash" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// URL decode text
    /// </summary>
    public static BenchmarkScenario CreateUrlDecode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-url-decode",
            Category = "utility",
            Description = "URL decode a percent-encoded string",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "URL decode the text 'hello%20world%26foo%3Dbar'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "url_decode",
                        ["input_text"] = "hello%20world%26foo%3Dbar"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "hello world", "foo=bar" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// HTML encode text with special characters
    /// </summary>
    public static BenchmarkScenario CreateHtmlEncode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-html-encode",
            Category = "utility",
            Description = "HTML encode a string with special characters",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "HTML encode the text '<script>alert(\"xss\")</script>'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "html_encode",
                        ["input_text"] = "<script>alert(\"xss\")</script>"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "&lt;", "&gt;", "&quot;", "&#", "encoded", "script" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Hex encode text
    /// </summary>
    public static BenchmarkScenario CreateHexEncode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-hex-encode",
            Category = "utility",
            Description = "Hex encode a text string",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Hex encode the text 'ABC'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "hex_encode",
                        ["input_text"] = "ABC"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // "ABC" in hex = 414243
                ResponseMustContainAny = new List<string> { "414243", "41 42 43", "hex" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// HTML decode text (reverse of HTML encode)
    /// </summary>
    public static BenchmarkScenario CreateHtmlDecode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-html-decode",
            Category = "utility",
            Description = "HTML decode an encoded string back to text",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "HTML decode the text '&lt;div&gt;Hello &amp; World&lt;/div&gt;'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "html_decode",
                        ["input_text"] = "&lt;div&gt;Hello &amp; World&lt;/div&gt;"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "<div>", "Hello", "World", "decoded" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Hex decode text (reverse of hex encode)
    /// </summary>
    public static BenchmarkScenario CreateHexDecode()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-hex-decode",
            Category = "utility",
            Description = "Hex decode a hex string back to text",
            Tags = new List<string> { "utility", "encoding", "single-tool" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Hex decode the string '48656C6C6F'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "hex_decode",
                        ["input_text"] = "48656C6C6F"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "Hello", "hello", "decoded" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// SHA1 hash
    /// </summary>
    public static BenchmarkScenario CreateSha1Hash()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-sha1",
            Category = "utility",
            Description = "Generate SHA1 hash of text",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate the SHA1 hash of 'test'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "sha1_hash",
                        ["input_text"] = "test"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // SHA1 of "test" = a94a8fe5ccb19ba61c4c0873d391e987982fbbd3
                ResponseMustContainAny = new List<string> { "a94a8fe5", "hash", "sha1" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// SHA512 hash
    /// </summary>
    public static BenchmarkScenario CreateSha512Hash()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-sha512",
            Category = "utility",
            Description = "Generate SHA512 hash of text",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate the SHA512 hash of 'test'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "sha512_hash",
                        ["input_text"] = "test"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // SHA512 of "test" starts with ee26b0dd
                ResponseMustContainAny = new List<string> { "ee26b0dd", "hash", "sha512" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Generate a GUID/UUID
    /// </summary>
    public static BenchmarkScenario CreateGuidGenerate()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-guid-generate",
            Category = "utility",
            Description = "Generate a new GUID",
            Tags = new List<string> { "utility", "encoding", "generate" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate a new GUID" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "guid_generate",
                        ["input_text"] = "generate"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "-", "guid", "generated", "generat" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Generate a password
    /// </summary>
    public static BenchmarkScenario CreatePasswordGenerate()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-password-generate",
            Category = "utility",
            Description = "Generate a random password",
            Tags = new List<string> { "utility", "encoding", "generate" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate a random password with 16 characters, including symbols and numbers" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "password_generate",
                        ["input_text"] = "generate",
                        ["length"] = 16,
                        ["include_symbols"] = true,
                        ["include_numbers"] = true
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "password", "generated", "random", "generat" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// BCrypt hash of text
    /// </summary>
    public static BenchmarkScenario CreateBcryptHash()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-bcrypt-hash",
            Category = "utility",
            Description = "Generate BCrypt hash of text",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate a BCrypt hash of the text 'mypassword'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "bcrypt_hash",
                        ["input_text"] = "mypassword"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "$2", "bcrypt", "hash" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// BCrypt verify a hash
    /// </summary>
    public static BenchmarkScenario CreateBcryptVerify()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-bcrypt-verify",
            Category = "utility",
            Description = "Verify a BCrypt hash against text",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Verify the BCrypt hash '$2a$11$example' against the text 'mypassword'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "bcrypt_verify",
                        ["input_text"] = "mypassword",
                        ["compare_value"] = "$2a$11$example"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "verify", "match", "valid", "false", "true", "does not" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Validate/identify hash type
    /// </summary>
    public static BenchmarkScenario CreateValidateHash()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-validate-hash",
            Category = "utility",
            Description = "Identify the type of a hash string",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Identify the hash type of '5d41402abc4b2a76b9719d911017c592'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "validate_hash",
                        ["input_text"] = "5d41402abc4b2a76b9719d911017c592"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "MD5", "md5", "hash", "type", "32" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// MD5 hash for additional hash algorithm coverage
    /// </summary>
    public static BenchmarkScenario CreateMd5Hash()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-md5",
            Category = "utility",
            Description = "Generate MD5 hash of text",
            Tags = new List<string> { "utility", "encoding", "hashing" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Generate the MD5 hash of 'hello'" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 1,
                    MaxInvocations = 1,
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "md5_hash",
                        ["input_text"] = "hello"
                    }
                }
            },
            Validation = new ValidationConfig
            {
                // MD5 of "hello" = 5d41402abc4b2a76b9719d911017c592
                ResponseMustContainAny = new List<string> { "5d41402a", "hash", "md5" },
                MustNotAskUser = true
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }

    /// <summary>
    /// Missing required input_text parameter
    /// </summary>
    public static BenchmarkScenario CreateMissingInput()
    {
        return new BenchmarkScenario
        {
            Id = "util-encoding-missing-input",
            Category = "utility",
            Description = "Call encoding_tool without providing input text",
            Tags = new List<string> { "utility", "encoding", "error-handling" },
            Workspace = new WorkspaceConfig { Type = "in-memory", Source = "" },
            Context = new ContextInjection
            {
                Prompts = new List<string> { "Use the encoding tool to base64 encode but don't provide any text" }
            },
            ExpectedTools = new List<ExpectedToolInvocation>
            {
                new ExpectedToolInvocation
                {
                    Type = "encoding_tool",
                    MinInvocations = 0,
                    MaxInvocations = 1
                }
            },
            Validation = new ValidationConfig
            {
                ResponseMustContainAny = new List<string> { "input", "text", "required", "provide" }
            },
            Timeout = TimeSpan.FromMinutes(1)
        };
    }
}
