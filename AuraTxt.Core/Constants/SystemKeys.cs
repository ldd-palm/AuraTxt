namespace AuraTxt.Core.Constants;

public static class SystemKeys
{
    public static readonly HashSet<string> Reserved =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Win+L","Win+D","Win+E","Win+R","Win+I","Win+S","Win+A","Win+X",
            "Win+P","Win+K","Win+H","Win+G","Win+M","Win+T","Win+U","Win+W",
            "Win+Tab","Win+Space","Win+Enter",
            "Win+Left","Win+Right","Win+Up","Win+Down",
            "Alt+F4","Alt+Tab","Alt+Esc",
            "Ctrl+Alt+Delete","PrintScreen","Win+PrintScreen","Alt+PrintScreen"
        };
}
