// AuraTxt.Core.Tests/Services/AiClientTests.cs
using System.Text.Json.Nodes;
using AuraTxt.Core.Adapters;
using AuraTxt.Core.Models;
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class AiClientTests
{
    // Fake adapter that captures the last AdapterRequest for inspection
    private sealed class CaptureAdapter(string name) : IAdapter
    {
        public string Name => name;
        public AdapterRequest? LastRequest { get; private set; }
        public Task<string> CompleteAsync(AdapterRequest req, CancellationToken ct)
        { LastRequest = req; return Task.FromResult("ok"); }
        public async IAsyncEnumerable<string> StreamAsync(AdapterRequest req,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        { LastRequest = req; yield return "ok"; }
    }

    private static AiClient MakeClient(out CaptureAdapter oai, out CaptureAdapter gem)
    {
        ProfileService.EnsureScaffold();
        oai = new CaptureAdapter("openai_compatible");
        gem = new CaptureAdapter("gemini_native");
        var oaiSnap = oai; var gemSnap = gem;
        return new AiClient(type => type == "gemini_native" ? gemSnap : oaiSnap);
    }

    private static ProviderConfig OaiProvider(string url = "https://example.com/v1") =>
        new() { BaseUrl = url, ApiKey = "test", AdapterType = "openai_compatible" };

    private static ProviderConfig GemProvider() =>
        new() { BaseUrl = "https://generativelanguage.googleapis.com", ApiKey = "key", AdapterType = "gemini_native" };

    private static ActionItem DisableAction() =>
        new() { ThinkingMode = "disable", Prompt = "{SelectedText}" };

    private static ActionItem EnableAction() =>
        new() { ThinkingMode = "enable_high", Prompt = "{SelectedText}" };

    [Fact]
    public void DeepSeekV4_Disable_HasChatTemplateKwargs()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "deepseek-ai/deepseek-v4-flash" };
        var (req, _) = client.BuildRequest(OaiProvider(), model, DisableAction(), "hi", "");
        Assert.True(req.ExtraBody.ContainsKey("chat_template_kwargs"), "should have chat_template_kwargs");
        var ctk = (JsonObject)req.ExtraBody["chat_template_kwargs"]!;
        Assert.Equal(false, ctk["thinking"]!.GetValue<bool>());
    }

    [Fact]
    public void DeepSeekV4_EnableHigh_HasReasoningEffort()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "deepseek-ai/deepseek-v4-flash" };
        var (req, _) = client.BuildRequest(OaiProvider(), model, EnableAction(), "hi", "");
        var ctk = (JsonObject)req.ExtraBody["chat_template_kwargs"]!;
        Assert.Equal(true,   ctk["thinking"]!.GetValue<bool>());
        Assert.Equal("high", ctk["reasoning_effort"]!.GetValue<string>());
    }

    [Fact]
    public void Llama_Disable_NoChatTemplateKwargs()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "meta/llama-3.3-70b-instruct" };
        var (req, _) = client.BuildRequest(OaiProvider(), model, DisableAction(), "hi", "");
        Assert.False(req.ExtraBody.ContainsKey("chat_template_kwargs"), "Llama must NOT have chat_template_kwargs");
    }

    [Fact]
    public void QwenNextInstruct_Disable_NoChatTemplateKwargs()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "qwen/qwen3-next-80b-a3b-instruct" };
        var (req, _) = client.BuildRequest(OaiProvider(), model, DisableAction(), "hi", "");
        Assert.False(req.ExtraBody.ContainsKey("chat_template_kwargs"));
    }

    [Fact]
    public void GeminiFlash_Disable_ThinkingBudgetZero()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "gemini-2.5-flash-preview" };
        var (req, _) = client.BuildRequest(GemProvider(), model, DisableAction(), "hi", "");
        var gc = (JsonObject)req.ExtraBody["generationConfig"]!;
        Assert.Equal(0, gc["thinkingConfig"]!["thinkingBudget"]!.GetValue<int>());
    }

    [Fact]
    public void Gemma4_Disable_ThinkingLevelNone()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "gemma-4-26b-a4b-it" };
        var (req, _) = client.BuildRequest(GemProvider(), model, DisableAction(), "hi", "");
        var gc = (JsonObject)req.ExtraBody["generationConfig"]!;
        Assert.Equal("none", gc["thinkingConfig"]!["thinkingLevel"]!.GetValue<string>());
    }

    [Fact]
    public void GeminiLegacy_Disable_NoThinkingConfig()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "gemini-2.0-flash" };
        var (req, _) = client.BuildRequest(GemProvider(), model, DisableAction(), "hi", "");
        Assert.False(req.ExtraBody.ContainsKey("generationConfig"));
    }

    [Fact]
    public void MiniMax_HasStripPatterns()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "minimaxai/minimax-m2.7" };
        var (_, strip) = client.BuildRequest(OaiProvider(), model, DisableAction(), "hi", "");
        Assert.NotEmpty(strip);
        Assert.Contains(strip, p => p.Contains("<think>"));
    }

    [Fact]
    public void GLM5_Disable_BothThinkingKeys()
    {
        var client = MakeClient(out _, out _);
        var model = new ModelEntry { TargetModel = "z-ai/glm-5.1" };
        var (req, _) = client.BuildRequest(OaiProvider(), model, DisableAction(), "hi", "");
        var ctk = (JsonObject)req.ExtraBody["chat_template_kwargs"]!;
        Assert.Equal(false, ctk["thinking"]!.GetValue<bool>());
        Assert.Equal(false, ctk["enable_thinking"]!.GetValue<bool>());
    }
}
