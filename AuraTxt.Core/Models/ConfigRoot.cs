namespace AuraTxt.Core.Models;

public class ConfigRoot
{
    public SystemConfig                      System   { get; set; } = new();
    public Dictionary<string, ModelPlatform> Models   { get; set; } = new();
    public List<ActionItem>                  Actions  { get; set; } = new();
    public AppSettings                       Settings { get; set; } = new();
}

public class SystemConfig
{
    public SystemService GoogleTranslate { get; set; } = new()
        { Provider = "google-translate", DisplayName = "Google 翻译" };
    public SystemService YoudaoDict { get; set; } = new()
        { Provider = "youdao-dict", DisplayName = "有道词典" };
}

public class SystemService
{
    public string Provider    { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
