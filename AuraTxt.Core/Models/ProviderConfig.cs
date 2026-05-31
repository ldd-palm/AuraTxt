namespace AuraTxt.Core.Models;

public class ProviderConfig
{
    public string           DisplayName { get; set; } = "";
    public string           BaseUrl     { get; set; } = "";
    public string           ApiKey      { get; set; } = "";
    public List<ModelEntry> Models      { get; set; } = new();
}
