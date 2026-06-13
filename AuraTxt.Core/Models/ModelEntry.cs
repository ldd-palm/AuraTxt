namespace AuraTxt.Core.Models;

public class ModelEntry
{
    public string TargetModel { get; set; } = "";
    public string Alias       { get; set; } = "";
    public bool   Enabled     { get; set; } = true;
    // Empty = auto-match by TargetModel name; non-empty = explicit profile id
    public string ProfileId   { get; set; } = "";
}
