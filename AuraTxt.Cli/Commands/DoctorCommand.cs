using AuraTxt.Core.Models;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Commands;

public class DoctorCommand(ConfigService config)
{
    private int _errors, _warnings;

    public int Execute()
    {
        _errors = _warnings = 0;
        Console.WriteLine("Running config diagnostics...");

        ConfigRoot cfg;
        try
        {
            cfg = config.Load();
            Ok("JSON syntax valid");
        }
        catch (Exception ex)
        {
            Error($"JSON parse error: {ex.Message}");
            PrintSummary();
            return 2;
        }

        var dupIds = cfg.Actions
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        foreach (var id in dupIds)
            Error($"Duplicate Action ID: \"{id}\"");
        if (!dupIds.Any())
            Ok("No duplicate Action IDs");

        int badRefs = 0;
        foreach (var action in cfg.Actions)
        {
            if (action.IsSystem) continue;   // system actions (copy/speech) have no ModelId
            if (!action.ModelId.Contains('/'))
            {
                Error($"Action \"{action.Id}\": ModelId \"{action.ModelId}\" missing '/' — expected format providerId/TargetModel");
                badRefs++;
                continue;
            }
            if (cfg.ResolveModel(action.ModelId) is null)
            {
                Error($"Action \"{action.Id}\": ModelId \"{action.ModelId}\" — provider or model not found");
                badRefs++;
            }
        }
        if (badRefs == 0 && cfg.Actions.Any())
            Ok("All Action ModelIds resolve correctly");

        foreach (var (id, p) in cfg.Models.Where(kv => kv.Key != "default"))
            if (p.Models.Count == 0)
                Warn($"Provider \"{id}\": has no models configured");

        var hv   = new HotkeyValidator();
        var seen = new List<ActionItem>();
        foreach (var a in cfg.Actions.Where(a => !string.IsNullOrEmpty(a.Hotkey)))
        {
            var (res, conflictName) = hv.Validate(a.Hotkey, seen);
            if (res == HotkeyValidationResult.InvalidFormat)
                Error($"Action \"{a.Id}\": invalid hotkey format \"{a.Hotkey}\"");
            else if (res == HotkeyValidationResult.Conflict)
                Warn($"Action \"{a.Id}\" and \"{conflictName}\" share hotkey {a.Hotkey}");
            seen.Add(a);
        }

        // Prompt file references must point to existing files
        int badPrompts = 0;
        foreach (var a in cfg.Actions)
            if (PromptService.IsFileRef(a.Prompt) && !File.Exists(a.Prompt))
            {
                Error($"Action \"{a.Id}\": prompt file not found — {a.Prompt}");
                badPrompts++;
            }
        if (PromptService.IsFileRef(cfg.Settings.SystemPrompt) && !File.Exists(cfg.Settings.SystemPrompt))
        {
            Error($"System prompt file not found — {cfg.Settings.SystemPrompt}");
            badPrompts++;
        }
        if (badPrompts == 0)
            Ok("All prompt files exist");

        // 1. ThinkingMode legality
        int badThinking = 0;
        foreach (var action in cfg.Actions)
        {
            if (action.ThinkingMode is not ("disable" or "enable_high"))
            { Error($"Action \"{action.Id}\": invalid ThinkingMode \"{action.ThinkingMode}\""); badThinking++; }
        }
        if (badThinking == 0)
            Ok("All Action ThinkingMode values are valid");

        // 2. Profile resolution success
        int badProfiles = 0;
        foreach (var (pid, provider) in cfg.Models.Where(kv => kv.Key != "default"))
        foreach (var model in provider.Models)
        {
            try { ProfileService.Resolve(model, provider.AdapterType); }
            catch (ProfileNotFoundException ex) { Error(ex.Message); badProfiles++; }
            catch (ProfileAdapterMismatchException ex) { Error(ex.Message); badProfiles++; }
        }
        if (badProfiles == 0 && cfg.Models.Any(kv => kv.Key != "default"))
            Ok("All models resolve to a compatible profile");

        // 3. enable_high with null-thinking profile
        foreach (var action in cfg.Actions.Where(a => a.ThinkingMode == "enable_high" && !string.IsNullOrEmpty(a.ModelId)))
        {
            var resolved = cfg.ResolveModel(action.ModelId);
            if (resolved is null) continue;
            var (provider, model) = resolved.Value;
            try
            {
                var profile = ProfileService.Resolve(model, provider.AdapterType);
                if (profile.Thinking is null)
                    Warn($"Action \"{action.Id}\" has ThinkingMode=enable_high but profile \"{profile.Id}\" has thinking=null (will have no effect)");
            }
            catch { }
        }

        // 4. StripPatterns format
        foreach (var profile in ProfileService.All())
        foreach (var pat in profile.StripPatterns)
            if (!pat.Contains("..."))
                Warn($"Profile \"{profile.Id}\" has strip_pattern without '...': \"{pat}\"");

        // 5. Capabilities vs action type
        foreach (var action in cfg.Actions.Where(a => !a.IsSystem && !string.IsNullOrEmpty(a.ModelId)))
        {
            var resolved = cfg.ResolveModel(action.ModelId);
            if (resolved is null) continue;
            var (provider, model) = resolved.Value;
            try
            {
                var profile = ProfileService.Resolve(model, provider.AdapterType);
                if (action.IsInteractive && !profile.Capabilities.MultiTurn)
                    Warn($"Action \"{action.Id}\" is interactive but profile \"{profile.Id}\" declares multi_turn=false");
                if (!action.IsInteractive && !profile.Capabilities.Streaming)
                    Warn($"Action \"{action.Id}\" uses streaming but profile \"{profile.Id}\" declares streaming=false");
            }
            catch { }
        }

        PrintSummary();
        return _errors > 0 ? 2 : _warnings > 0 ? 1 : 0;
    }

    private void PrintSummary()
    {
        Console.WriteLine();
        if (_errors == 0 && _warnings == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ 0 errors, 0 warnings — config is healthy");
        }
        else
        {
            Console.WriteLine($"{_errors} error(s), {_warnings} warning(s) found. Edit config.json manually to fix.");
        }
        Console.ResetColor();
    }

    private void Ok(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  [OK]    ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private void Warn(string msg)
    {
        _warnings++;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  [WARN]  ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private void Error(string msg)
    {
        _errors++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  [ERROR] ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }
}
