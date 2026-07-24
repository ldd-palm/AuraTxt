# Auto-Update Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On startup and on manual demand from the tray menu, check GitHub Releases for a newer AuraTxt version and surface it via a balloon notification plus a persistent tray menu indicator — notify-only, no downloading/installing.

**Architecture:** A new static `UpdateService` in `AuraTxt.Core` does the GitHub API call and version comparison (pure logic unit-tested, network call excluded from the automated suite per project convention). `TrayIconManager` (WPF layer) owns all update-related UI state and behavior — a `_pendingUpdate` field, a dynamically-relabeled menu item, and balloon notifications via `H.NotifyIcon`'s `TaskbarIcon.ShowNotification`. `App.xaml.cs` fires one silent check at startup; the tray menu item triggers checks manually and always gives feedback.

**Tech Stack:** .NET 8, `System.Net.Http` + `System.Text.Json` (matching `GoogleTranslateClient`'s existing style), `H.NotifyIcon.Wpf` 2.1.0 (`TaskbarIcon.ShowNotification`, already a project dependency), xunit.

**Spec:** `docs/superpowers/specs/2026-07-24-auto-update-design.md`

---

## Implementation note vs. spec

The spec's `UpdateService.CheckAsync` signature omitted a version parameter, implying it would read `Assembly.GetExecutingAssembly().GetName().Version` internally. That's wrong: `UpdateService` lives in `AuraTxt.Core.dll`, so `GetExecutingAssembly()` there would return **Core's** assembly (which never gets a `<Version>`), not `AuraTxt.exe`'s. Fixed by having `CheckAsync` take `Version currentVersion` as a parameter — the WPF layer (where `GetExecutingAssembly()` correctly resolves to `AuraTxt.exe`) supplies it. No behavior change, just correct wiring.

---

### Task 1: Embed the app version

**Files:**
- Modify: `AuraTxt/AuraTxt.csproj:9`

- [ ] **Step 1: Add the `<Version>` property**

In `AuraTxt/AuraTxt.csproj`, change:

```xml
    <AssemblyName>AuraTxt</AssemblyName>
    <ApplicationIcon>Resources\aruatxt_active.ico</ApplicationIcon>
```

to:

```xml
    <AssemblyName>AuraTxt</AssemblyName>
    <ApplicationIcon>Resources\aruatxt_active.ico</ApplicationIcon>
    <Version>1.3</Version>
```

`1.3` matches the current released git tag `v1.3` (see README.md download links) — bump this by hand each release alongside the tag, same checklist entry as updating those download links.

- [ ] **Step 2: Build and confirm the version is embedded**

Run: `dotnet build AuraTxt/AuraTxt.csproj`
Expected: `Build succeeded`, no errors.

- [ ] **Step 3: Commit**

```bash
git add AuraTxt/AuraTxt.csproj
git commit -m "feat(update): embed app version number"
```

---

### Task 2: Add the dedupe field to AppSettings

**Files:**
- Modify: `AuraTxt.Core/Models/AppSettings.cs:39-42`

- [ ] **Step 1: Add `LastNotifiedUpdateVersion`**

In `AuraTxt.Core/Models/AppSettings.cs`, change:

```csharp
    /// When true, Terminal actions launch a real, visible cmd.exe window instead of
    /// capturing output into ResultWindow. Default false = today's redirected-buffer behavior.
    public bool TerminalUseConsoleWindow { get; set; } = false;
}
```

to:

```csharp
    /// When true, Terminal actions launch a real, visible cmd.exe window instead of
    /// capturing output into ResultWindow. Default false = today's redirected-buffer behavior.
    public bool TerminalUseConsoleWindow { get; set; } = false;

    /// Last release version (e.g. "1.4") a tray balloon notification was already shown
    /// for. Prevents re-notifying on every launch for a release the user hasn't acted on
    /// yet. Internal bookkeeping only — not exposed in auracfg's General Settings page.
    public string LastNotifiedUpdateVersion { get; set; } = "";
}
```

- [ ] **Step 2: Build and confirm existing config tests still pass**

Run: `dotnet test AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj --filter FullyQualifiedName~ConfigServiceTests`
Expected: all `ConfigServiceTests` pass (new field defaults to `""`, doesn't break serialization round-trip).

- [ ] **Step 3: Commit**

```bash
git add AuraTxt.Core/Models/AppSettings.cs
git commit -m "feat(update): add LastNotifiedUpdateVersion setting for update-check dedupe"
```

---

### Task 3: UpdateService — version comparison (TDD) + GitHub check

**Files:**
- Create: `AuraTxt.Core/Services/UpdateService.cs`
- Test: `AuraTxt.Core.Tests/Services/UpdateServiceTests.cs`

- [ ] **Step 1: Write the failing tests for `IsNewer`**

Create `AuraTxt.Core.Tests/Services/UpdateServiceTests.cs`:

```csharp
using AuraTxt.Core.Services;
using Xunit;

namespace AuraTxt.Core.Tests.Services;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.4", "1.3")]
    [InlineData("1.4", "1.3")]
    [InlineData("V1.4", "1.3")]
    public void IsNewer_ReturnsTrue_WhenTagIsNewer(string tag, string current)
    {
        Assert.True(UpdateService.IsNewer(tag, Version.Parse(current)));
    }

    [Theory]
    [InlineData("v1.3", "1.3")]
    [InlineData("v1.2", "1.3")]
    [InlineData("v1.3", "1.3.0.0")]
    public void IsNewer_ReturnsFalse_WhenTagIsSameOrOlder(string tag, string current)
    {
        Assert.False(UpdateService.IsNewer(tag, Version.Parse(current)));
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    [InlineData("vX.Y")]
    public void IsNewer_ReturnsFalse_ForUnparsableTag(string tag)
    {
        Assert.False(UpdateService.IsNewer(tag, Version.Parse("1.3")));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (UpdateService doesn't exist yet)**

Run: `dotnet test AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj --filter FullyQualifiedName~UpdateServiceTests`
Expected: build error — `UpdateService` / `IsNewer` not found.

- [ ] **Step 3: Create UpdateService.cs**

Create `AuraTxt.Core/Services/UpdateService.cs`:

```csharp
using System.Net.Http;
using System.Text.Json;

namespace AuraTxt.Core.Services;

public record UpdateInfo(string Version, string Url);

/// Checks GitHub Releases for a newer AuraTxt version. Notify-only — never
/// downloads or installs anything.
/// See docs/superpowers/specs/2026-07-24-auto-update-design.md.
public static class UpdateService
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/ldd-palm/AuraTxt/releases/latest";

    // Shared instance — GitHub API + JSON GET, no reason to ever construct a
    // second one (SPEC.md §6.9: no per-call `new HttpClient`).
    private static readonly HttpClient _http = CreateShared();

    private static HttpClient CreateShared()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub's API rejects requests with no User-Agent header.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AuraTxt-UpdateCheck");
        return http;
    }

    /// Returns null when the check succeeded but no newer version exists.
    /// Throws (HttpRequestException, JSON errors) when the check itself failed —
    /// callers that need to tell the two apart (manual "Check for Updates") catch
    /// separately from callers that treat both the same (silent startup check).
    public static async Task<UpdateInfo?> CheckAsync(Version currentVersion, CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(ReleasesUrl, ct);
        using var doc = JsonDocument.Parse(json);

        var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? "";

        return IsNewer(tagName, currentVersion)
            ? new UpdateInfo(tagName.TrimStart('v', 'V'), htmlUrl)
            : null;
    }

    /// Pure — no network. Malformed tagName yields false, never throws.
    internal static bool IsNewer(string tagName, Version current)
    {
        var trimmed = tagName.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var tagVersion) && tagVersion > current;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj --filter FullyQualifiedName~UpdateServiceTests`
Expected: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 5: Run the full Core test suite to confirm no regressions**

Run: `dotnet test AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj`
Expected: all tests pass (109 total: the 100 pre-existing + 9 new `UpdateServiceTests`).

- [ ] **Step 6: Commit**

```bash
git add AuraTxt.Core/Services/UpdateService.cs AuraTxt.Core.Tests/Services/UpdateServiceTests.cs
git commit -m "feat(update): add UpdateService for GitHub release version checks"
```

---

### Task 4: Tray integration — menu item, balloon notifications, state

**Files:**
- Modify: `AuraTxt/Services/TrayIconManager.cs`

- [ ] **Step 1: Add the using directive for NotificationIcon**

Change:

```csharp
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using AuraTxt.Core.Services;
```

to:

```csharp
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using AuraTxt.Core.Services;
```

- [ ] **Step 2: Add fields for config access, the menu item, and pending-update state**

Change:

```csharp
public class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _toggleMonitorItem = null!;
    private readonly MenuItem _toggleMenuItem = null!;
    private readonly MenuItem _settingsItem = null!;

    public TrayIconManager(ConfigService config, Action onReload, Action onExit, Action? onToggleMonitor = null)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "AuraTxt"
        };
```

to:

```csharp
public class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly ConfigService _config;
    private readonly MenuItem _toggleMonitorItem = null!;
    private readonly MenuItem _toggleMenuItem = null!;
    private readonly MenuItem _settingsItem = null!;
    private readonly MenuItem _updateItem = null!;
    private UpdateInfo? _pendingUpdate;

    public TrayIconManager(ConfigService config, Action onReload, Action onExit, Action? onToggleMonitor = null)
    {
        _config = config;
        _icon = new TaskbarIcon
        {
            ToolTipText = "AuraTxt"
        };
```

- [ ] **Step 3: Add the menu item, wire its click handler, extend the Opened handler**

Change:

```csharp
        menu.Opened += (_, _) =>
        {
            var editor = config.Load().Settings.ConfigEditor;
            var name = string.IsNullOrEmpty(editor)
                ? "auracfg"
                : System.IO.Path.GetFileNameWithoutExtension(editor);
            _settingsItem.Header = $"Settings ({name})";
        };

        var aboutItem = new MenuItem { Header = "About" };
        aboutItem.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/ldd-palm/AuraTxt") { UseShellExecute = true });

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => onExit();

        menu.Items.Add(_toggleMonitorItem);
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);
```

to:

```csharp
        _updateItem = new MenuItem { Header = "Check for Updates" };
        _updateItem.Click += (_, _) =>
        {
            if (_pendingUpdate is { } info)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    info.Url) { UseShellExecute = true });
            else
                _ = CheckForUpdatesAsync(manual: true);
        };

        menu.Opened += (_, _) =>
        {
            var editor = config.Load().Settings.ConfigEditor;
            var name = string.IsNullOrEmpty(editor)
                ? "auracfg"
                : System.IO.Path.GetFileNameWithoutExtension(editor);
            _settingsItem.Header = $"Settings ({name})";

            _updateItem.Header = _pendingUpdate is { } pending
                ? $"⬆ Update available (v{pending.Version})"
                : "Check for Updates";
        };

        var aboutItem = new MenuItem { Header = "About" };
        aboutItem.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/ldd-palm/AuraTxt") { UseShellExecute = true });

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => onExit();

        menu.Items.Add(_toggleMonitorItem);
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);
```

- [ ] **Step 4: Add the `CheckForUpdatesAsync` method**

Change:

```csharp
    public void RefreshIcon() => SetTrayIcon();

    public void Dispose() => _icon.Dispose();
}
```

to:

```csharp
    public void RefreshIcon() => SetTrayIcon();

    /// Checks GitHub for a newer release. `manual` controls whether "no update"
    /// and "check failed" outcomes also get a balloon — they don't for the silent
    /// startup check. See docs/superpowers/specs/2026-07-24-auto-update-design.md §3/§4.
    public async Task CheckForUpdatesAsync(bool manual)
    {
        var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(0, 0);

        UpdateInfo? info;
        try
        {
            info = await UpdateService.CheckAsync(current);
        }
        catch
        {
            if (manual)
                _icon.ShowNotification("AuraTxt", "Could not check for updates.", NotificationIcon.Error);
            return;
        }

        if (info is null)
        {
            if (manual)
                _icon.ShowNotification("AuraTxt", $"You're up to date (v{current.ToString(2)}).", NotificationIcon.Info);
            return;
        }

        _pendingUpdate = info;

        var cfg = _config.Load();
        if (info.Version != cfg.Settings.LastNotifiedUpdateVersion)
        {
            _icon.ShowNotification("AuraTxt", $"Version {info.Version} is available.", NotificationIcon.Info);
            cfg.Settings.LastNotifiedUpdateVersion = info.Version;
            _config.Save(cfg);
        }
    }

    public void Dispose() => _icon.Dispose();
}
```

- [ ] **Step 5: Build**

Run: `dotnet build AuraTxt/AuraTxt.csproj`
Expected: `Build succeeded`, no errors. (`H.NotifyIcon.Core.NotificationIcon` and `TaskbarIcon.ShowNotification(string, string, NotificationIcon, ...)` are both confirmed present in the referenced `H.NotifyIcon.Wpf` 2.1.0 package via reflection during plan authoring.)

- [ ] **Step 6: Commit**

```bash
git add AuraTxt/Services/TrayIconManager.cs
git commit -m "feat(update): add tray balloon notification and menu indicator for updates"
```

---

### Task 5: Wire the silent startup check

**Files:**
- Modify: `AuraTxt/App.xaml.cs:68-70`

- [ ] **Step 1: Fire the check after the hook starts**

Change:

```csharp
            _hook    = new GlobalHookService(_config, _hotkeys);
            _hook.Start();
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
```

to:

```csharp
            _hook    = new GlobalHookService(_config, _hotkeys);
            _hook.Start();
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

            _ = _tray.CheckForUpdatesAsync(manual: false);
        }
```

- [ ] **Step 2: Build the full solution**

Run: `dotnet build`
Expected: `Build succeeded`, no errors, across all 4 projects.

- [ ] **Step 3: Commit**

```bash
git add AuraTxt/App.xaml.cs
git commit -m "feat(update): check for updates once on startup"
```

---

### Task 6: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test AuraTxt.Core.Tests/AuraTxt.Core.Tests.csproj`
Expected: all tests pass (109 total).

- [ ] **Step 2: Publish a test build**

Stop any running `AuraTxt.exe`/`auracfg.exe` first (file lock — see SPEC.md §12), then:

```bash
dotnet publish AuraTxt/AuraTxt.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/release
```

- [ ] **Step 3: Manually verify the "update available" path**

Temporarily edit `AuraTxt/AuraTxt.csproj` to set `<Version>0.1</Version>` (below any real release), rebuild/republish, then run `publish/release/AuraTxt.exe`:
- Confirm exactly one balloon notification appears shortly after startup, worded "Version {X} is available."
- Open the tray menu; confirm the item reads "⬆ Update available (v{X})" instead of "Check for Updates".
- Click that menu item; confirm it opens `https://github.com/ldd-palm/AuraTxt/releases/...` in the default browser, and does **not** re-run a check.
- Exit and relaunch `AuraTxt.exe` without changing anything else; confirm **no second balloon** appears, but the tray menu still shows "Update available" (dedupe via `LastNotifiedUpdateVersion`, written into `publish/release/config.json`).

- [ ] **Step 4: Manually verify the "up to date" and "check failed" paths**

Revert `<Version>` back to `1.3`, rebuild/republish, delete `LastNotifiedUpdateVersion` from `publish/release/config.json` (or delete the file to regenerate defaults), relaunch:
- Open the tray menu, confirm it reads "Check for Updates" (no pending update).
- Click it; confirm a balloon reading "You're up to date (v1.3)" appears.
- Disconnect network (or block `api.github.com`), click "Check for Updates" again; confirm a balloon reading "Could not check for updates" appears, with no crash and no hang beyond the ~10s timeout.

- [ ] **Step 5: Restore AuraTxt.csproj to the real version**

Confirm `AuraTxt/AuraTxt.csproj` has `<Version>1.3</Version>` (or whatever the actual current release is) before finishing — Step 3/4 temporarily changed it for testing.
