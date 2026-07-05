using System.Diagnostics;
using System.Text;

namespace AuraTxt.Core.Services;

/// Built-in "Terminal" model: runs a user-configured cmd.exe command template against
/// the selected text instead of calling an AI provider. The command template is untrusted
/// input's destination, not its source — {SelectedText} substituted into it is externally
/// untrusted (may contain shell metacharacters), so output is always echoed with the
/// resolved command for transparency rather than run silently.
public static class TerminalClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// Pure substitution — no I/O — kept separate from RunAsync so it's unit-testable
    /// without spawning a process.
    public static string BuildResolvedCommand(string commandTemplate, string selectedText, string userInput) =>
        PromptService.Resolve(commandTemplate)
            .Replace("{SelectedText}", selectedText)
            .Replace("{UserInput}",   userInput);

    public static async Task<string> RunAsync(
        string commandTemplate, string selectedText, string userInput, CancellationToken ct)
    {
        var resolved = BuildResolvedCommand(commandTemplate, selectedText, userInput);
        LogService.Raw($"──── TERMINAL COMMAND\n{resolved}");

        if (ConfigService.DefaultSettings?.TerminalUseConsoleWindow ?? false)
            return RunInConsoleWindow(resolved);

        var psi = new ProcessStartInfo
        {
            FileName               = "cmd.exe",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = AppContext.BaseDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };
        // "chcp 65001" switches the child console to UTF-8 so CJK text round-trips correctly.
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("chcp 65001>nul & " + resolved);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        using var timeoutCts = new CancellationTokenSource(Timeout);
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        process.Start();
        using var killReg = linkedCts.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* already exited */ }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (timeoutCts.IsCancellationRequested)
            return $"> {resolved}\n\n{stdout}{stderr}\n[Error] Command timed out after {Timeout.TotalSeconds:0}s.";
        ct.ThrowIfCancellationRequested();

        var combined = stdout + stderr;
        var footer = process.ExitCode != 0 ? $"\n[exit code: {process.ExitCode}]" : "";
        return $"> {resolved}\n\n{combined}{footer}";
    }

    /// Launches a real, visible, interactive cmd.exe window and returns immediately —
    /// no redirection, no timeout, no kill-on-cancel. The window's lifecycle is
    /// intentionally independent of the caller (long-running/interactive commands need
    /// to outlive whatever triggered them), so there is no captured output to return
    /// and no orphan-process cleanup here.
    private static string RunInConsoleWindow(string resolved)
    {
        var psi = new ProcessStartInfo
        {
            FileName         = "cmd.exe",
            UseShellExecute  = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("chcp 65001>nul & " + resolved);

        Process.Start(psi);
        return $"> {resolved}\n\n[Launched in a separate console window.]";
    }
}
