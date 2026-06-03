namespace AuraTxt.Core.Models;

public class ActionItem
{
    public string Id            { get; set; } = "";
    public string Name          { get; set; } = "";
    public string Icon          { get; set; } = "";
    public string ModelId       { get; set; } = "";
    public bool   IsInteractive { get; set; }
    public string Hotkey        { get; set; } = "";
    public string Prompt        { get; set; } = "";
    public bool   Enabled       { get; set; } = true;
    public int    Order         { get; set; } = 0;
    public bool   IsSystem      { get; set; }

    public bool IsSystemModel => ModelId.StartsWith("default/", StringComparison.OrdinalIgnoreCase);
}
