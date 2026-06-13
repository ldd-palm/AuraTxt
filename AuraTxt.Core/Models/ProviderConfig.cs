namespace AuraTxt.Core.Models;

public class ProviderConfig
{
    public string           DisplayName { get; set; } = "";
    public string           BaseUrl     { get; set; } = "";
    public string           ApiKey      { get; set; } = "";
    // Explicit; no URL sniffing. Valid: "openai_compatible" | "gemini_native"
    public string           AdapterType { get; set; } = "openai_compatible";
    public List<ModelEntry> Models      { get; set; } = new();
}
