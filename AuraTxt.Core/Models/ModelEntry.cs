namespace AuraTxt.Core.Models;

public class ModelEntry
{
    public string TargetModel     { get; set; } = "";
    public string Alias           { get; set; } = "";
    public bool   DisableThinking { get; set; } = true;
    public bool   Enabled         { get; set; } = true;
}
