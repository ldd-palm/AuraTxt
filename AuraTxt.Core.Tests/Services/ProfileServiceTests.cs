// AuraTxt.Core.Tests/Services/ProfileServiceTests.cs
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class ProfileServiceTests : IDisposable
{
    private readonly string _tmpDir;

    public ProfileServiceTests() => _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    public void Dispose() { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); }

    [Fact]
    public void EnsureScaffold_SeedsEmbeddedProfiles()
    {
        ProfileService.EnsureScaffold();
        var profiles = ProfileService.All();
        Assert.NotEmpty(profiles);
        Assert.Contains(profiles, p => p.Id == "default-openai");
        Assert.Contains(profiles, p => p.Id == "default-gemini");
    }

    [Fact]
    public void Resolve_AutoMatch_DeepSeekV4()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "deepseek-ai/deepseek-v4-flash" };
        var profile = ProfileService.Resolve(entry, "openai_compatible");
        Assert.Equal("deepseek-v4", profile.Id);
    }

    [Fact]
    public void Resolve_AutoMatch_Llama_DoesNotGetChatTemplateKwargs()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "meta/llama-3.3-70b-instruct" };
        var profile = ProfileService.Resolve(entry, "openai_compatible");
        Assert.Null(profile.Thinking);
    }

    [Fact]
    public void Resolve_QwenNextInstruct_HigherPriorityThanQwenThinking()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "qwen/qwen3-next-80b-a3b-instruct" };
        var profile = ProfileService.Resolve(entry, "openai_compatible");
        Assert.Equal("qwen3-next-instruct", profile.Id);
        Assert.Null(profile.Thinking);
    }

    [Fact]
    public void Resolve_GeminiFlash_GeminiNativeAdapter()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "gemini-2.5-flash-preview" };
        var profile = ProfileService.Resolve(entry, "gemini_native");
        Assert.Equal("gemini-flash", profile.Id);
        Assert.NotNull(profile.Thinking);
    }

    [Fact]
    public void Resolve_Fallback_UnknownModel()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "some-totally-unknown-model-xyz" };
        var profile = ProfileService.Resolve(entry, "openai_compatible");
        Assert.Equal("default-openai", profile.Id);
    }

    [Fact]
    public void Resolve_ExplicitProfileId_Found()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "anything", ProfileId = "glm-5" };
        var profile = ProfileService.Resolve(entry, "openai_compatible");
        Assert.Equal("glm-5", profile.Id);
    }

    [Fact]
    public void Resolve_ExplicitProfileId_NotFound_Throws()
    {
        ProfileService.EnsureScaffold();
        var entry = new ModelEntry { TargetModel = "anything", ProfileId = "nonexistent-profile" };
        Assert.Throws<ProfileNotFoundException>(() =>
            ProfileService.Resolve(entry, "openai_compatible"));
    }

    [Fact]
    public void Resolve_ExplicitProfileId_WrongAdapter_Throws()
    {
        ProfileService.EnsureScaffold();
        // gemini-flash is adapter_compatibility: ["gemini_native"]
        var entry = new ModelEntry { TargetModel = "anything", ProfileId = "gemini-flash" };
        Assert.Throws<ProfileAdapterMismatchException>(() =>
            ProfileService.Resolve(entry, "openai_compatible"));
    }

    [Fact]
    public void Resolve_AdapterFiltering_OpenAiProfileNotReturnedForGemini()
    {
        ProfileService.EnsureScaffold();
        // deepseek-v4 is openai_compatible only; requesting gemini_native should NOT return it
        var entry = new ModelEntry { TargetModel = "deepseek-ai/deepseek-v4-flash" };
        var profile = ProfileService.Resolve(entry, "gemini_native");
        Assert.NotEqual("deepseek-v4", profile.Id); // falls through to default-gemini
    }
}
