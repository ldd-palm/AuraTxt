using System.Resources;

namespace AuraTxt.Resources;

/// Hand-written (not resx-designer-generated) so the class and its members are
/// public — WPF's XAML compiler can't resolve {x:Static} against the `internal`
/// class the MSBuild:Compile resx generator produces, even within the same
/// assembly. The .resx files are still the storage/translation format and still
/// drive satellite-assembly culture fallback via ResourceManager as normal;
/// only the C# accessor is hand-written. See SPEC.md §5.9.
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("AuraTxt.Resources.Strings", typeof(Strings).Assembly);

    public static string Tray_ServicePause         => Rm.GetString(nameof(Tray_ServicePause))!;
    public static string Tray_ServiceResume        => Rm.GetString(nameof(Tray_ServiceResume))!;
    public static string Tray_HideMenu             => Rm.GetString(nameof(Tray_HideMenu))!;
    public static string Tray_ShowMenu             => Rm.GetString(nameof(Tray_ShowMenu))!;
    public static string Tray_ReloadSettings       => Rm.GetString(nameof(Tray_ReloadSettings))!;
    public static string Tray_Settings             => Rm.GetString(nameof(Tray_Settings))!;
    public static string Tray_About                => Rm.GetString(nameof(Tray_About))!;
    public static string Tray_Exit                 => Rm.GetString(nameof(Tray_Exit))!;
    public static string About_WindowTitle         => Rm.GetString(nameof(About_WindowTitle))!;
    public static string About_RuntimeFormat       => Rm.GetString(nameof(About_RuntimeFormat))!;
    public static string About_CheckingForUpdates  => Rm.GetString(nameof(About_CheckingForUpdates))!;
    public static string About_UpToDate            => Rm.GetString(nameof(About_UpToDate))!;
    public static string About_UpdateAvailableFormat => Rm.GetString(nameof(About_UpdateAvailableFormat))!;
    public static string About_CheckFailed         => Rm.GetString(nameof(About_CheckFailed))!;
    public static string About_AutoUpdateLabel     => Rm.GetString(nameof(About_AutoUpdateLabel))!;
    public static string About_GitHubTooltip       => Rm.GetString(nameof(About_GitHubTooltip))!;
    public static string About_HomepageLabel       => Rm.GetString(nameof(About_HomepageLabel))!;
    public static string About_ReleasesLabel       => Rm.GetString(nameof(About_ReleasesLabel))!;
    public static string Common_Close              => Rm.GetString(nameof(Common_Close))!;
    public static string Common_Cancel             => Rm.GetString(nameof(Common_Cancel))!;
    public static string Common_EditPromptTooltip  => Rm.GetString(nameof(Common_EditPromptTooltip))!;
    public static string Common_RegenerateTooltip  => Rm.GetString(nameof(Common_RegenerateTooltip))!;
    public static string Common_ReplaceTooltip     => Rm.GetString(nameof(Common_ReplaceTooltip))!;
    public static string Common_CopyTooltip        => Rm.GetString(nameof(Common_CopyTooltip))!;
    public static string Common_PinTooltip         => Rm.GetString(nameof(Common_PinTooltip))!;
    public static string Interactive_InputLabel    => Rm.GetString(nameof(Interactive_InputLabel))!;
    public static string Interactive_ResultLabel   => Rm.GetString(nameof(Interactive_ResultLabel))!;
    public static string PromptEdit_Title          => Rm.GetString(nameof(PromptEdit_Title))!;
    public static string PromptEdit_BuiltinTitle   => Rm.GetString(nameof(PromptEdit_BuiltinTitle))!;
    public static string PromptEdit_GenerateButton => Rm.GetString(nameof(PromptEdit_GenerateButton))!;
}
