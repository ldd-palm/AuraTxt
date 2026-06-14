namespace AuraTxt.Cli.Tui;

public enum ItemValueStyle { Muted, Success, Danger, Warning }

public sealed record MenuItem(
    string  Key,
    string  Label,
    string? Value       = null,
    ItemValueStyle ValueStyle  = ItemValueStyle.Muted,
    bool    IsSeparator = false,
    string? Value2      = null,
    ItemValueStyle ValueStyle2 = ItemValueStyle.Muted)
{
    public static MenuItem Sep() => new("", "", IsSeparator: true);
}

public enum NoticeKind { Success, Warning, Error, Info }

public abstract record MenuKey
{
    public sealed record Arrow(bool Up)  : MenuKey;
    public sealed record Number(int N)   : MenuKey;
    public sealed record Letter(char C)  : MenuKey;
    public sealed record Confirm         : MenuKey;
    public sealed record Escape          : MenuKey;
    public sealed record Quit            : MenuKey;
    public sealed record Unknown         : MenuKey;
}
