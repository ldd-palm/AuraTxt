namespace AuraTxt.Core.Models;

public class ModelPlatform
{
    public string DisplayName  { get; set; } = "";
    public string Provider     { get; set; } = "openai-compatible";
    public string BaseUrl      { get; set; } = "";
    public string ApiKey       { get; set; } = "";
    public string TargetModel  { get; set; } = "";
    public string Alias        => $"{Provider}/{TargetModel}";
}
