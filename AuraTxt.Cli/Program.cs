using System.Text;
using AuraTxt.Cli.Commands;
using AuraTxt.Cli.Tui;
using AuraTxt.Core.Services;

Console.OutputEncoding = Encoding.UTF8;

var configService = new ConfigService();

if (args.Length == 0)
{
    await new TuiApp(configService).RunAsync();
    return 0;
}

return args[0] switch
{
    "provider"         => await new ProviderCommand(configService).ExecuteAsync(args[1..]),
    "model"            => await new ProviderCommand(configService).ExecuteAsync(args[1..]),
    "action"           => await new ActionCommand(configService).ExecuteAsync(args[1..]),
    "prompt"           => await new PromptCommand(configService).ExecuteAsync(args[1..]),
    "show"             => await new ShowCommand(configService).ExecuteAsync(args[1..]),
    "settings"         => await new SettingsCommand(configService).ExecuteAsync(args[1..]),
    "doctor"           => new DoctorCommand(configService).Execute(),
    "restore"          => Restore(configService),
    "--help" or "-h"   => Help(),
    _                  => Help()
};

static int Restore(ConfigService svc)
{
    try
    {
        svc.Restore();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Config restored from config.json.bak");
        Console.ResetColor();
        return 0;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Restore failed: {ex.Message}");
        Console.ResetColor();
        return 3;
    }
}

static int Help()
{
    Console.WriteLine("auracfg — AuraTxt Config Tool");
    Console.WriteLine();
    Console.WriteLine("USAGE");
    Console.WriteLine("  auracfg                    Interactive TUI menu");
    Console.WriteLine("  auracfg <command> --help   Show command usage");
    Console.WriteLine();
    Console.WriteLine("COMMANDS");
    Console.WriteLine("  show");
    Console.WriteLine("    auracfg show                          Overview of full config");
    Console.WriteLine("    auracfg show provider [id]            List providers or show one");
    Console.WriteLine("    auracfg show action   [id]            List actions or show one");
    Console.WriteLine();
    Console.WriteLine("  provider  (alias: model)");
    Console.WriteLine("    auracfg provider --list");
    Console.WriteLine("    auracfg provider --set          --id <id> --display <name> --url <url> --key <key> [--model <name>] [--alias <alias>] [--thinking]");
    Console.WriteLine("    auracfg provider --update       --id <id> [--display <name>] [--url <url>] [--key <key>]");
    Console.WriteLine("    auracfg provider --delete       --id <id> [--force]");
    Console.WriteLine("    auracfg provider --add-model    --id <id> --model <name> [--alias <alias>] [--thinking]");
    Console.WriteLine("    auracfg provider --update-model --id <id> --model <name> [--alias <alias>] [--thinking|--no-thinking] [--enabled <true|false>]");
    Console.WriteLine("    auracfg provider --delete-model --id <id> --model <name>");
    Console.WriteLine();
    Console.WriteLine("  action");
    Console.WriteLine("    auracfg action --list");
    Console.WriteLine("    auracfg action --set    --id <id> --name <name> --icon <lucide> --model-id <provider/model> --interactive <true|false> --prompt \"<text>\" [--hotkey <key>] [--enabled <true|false>] [--order <int>]");
    Console.WriteLine("    auracfg action --update --id <id> [--name] [--icon] [--model-id] [--interactive] [--prompt] [--hotkey] [--enabled] [--order]");
    Console.WriteLine("    auracfg action --delete --id <id>");
    Console.WriteLine();
    Console.WriteLine("  prompt");
    Console.WriteLine("    auracfg prompt --list");
    Console.WriteLine("    auracfg prompt --show   --name <name>");
    Console.WriteLine("    auracfg prompt --add    --name <name> [--content \"<text>\" | --file <path>]");
    Console.WriteLine("    auracfg prompt --update --name <name>  --content \"<text>\" | --file <path>");
    Console.WriteLine("    auracfg prompt --delete --name <name>");
    Console.WriteLine();
    Console.WriteLine("  settings");
    Console.WriteLine("    auracfg settings --show");
    Console.WriteLine("    auracfg settings --set [--font-size <n>] [--opacity <0-1>] [--delay-ms <n>] [--target-lang <code>] [--theme <id>] [--voice <name>] [--prompt-editor <exe>] [--config-editor <exe>]");
    Console.WriteLine();
    Console.WriteLine("  doctor                             Validate config and report issues");
    Console.WriteLine("  restore                            Restore config.json from .bak");
    return 0;
}
