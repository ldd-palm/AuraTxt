using System.Text;
using AuraTxt.Cli.Commands;
using AuraTxt.Cli.Menus;
using AuraTxt.Core.Services;

Console.OutputEncoding = Encoding.UTF8;

var configService = new ConfigService();

if (args.Length == 0)
{
    await new InteractiveMenu(configService).RunAsync();
    return 0;
}

return args[0] switch
{
    "provider" => await new ProviderCommand(configService).ExecuteAsync(args[1..]),
    "model"    => await new ProviderCommand(configService).ExecuteAsync(args[1..]),
    "action"   => await new ActionCommand(configService).ExecuteAsync(args[1..]),
    "prompt"   => await new PromptCommand(configService).ExecuteAsync(args[1..]),
    "show"     => await new ShowCommand(configService).ExecuteAsync(args[1..]),
    "settings" => await new SettingsCommand(configService).ExecuteAsync(args[1..]),
    "doctor"   => new DoctorCommand(configService).Execute(),
    "restore"  => Restore(configService),
    _          => Help()
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
    Console.WriteLine("Usage:");
    Console.WriteLine("  auracfg                        Interactive menu");
    Console.WriteLine("  auracfg show                   Overview all config");
    Console.WriteLine("  auracfg show provider [id]     Show provider details");
    Console.WriteLine("  auracfg show action [id]       Show action details");
    Console.WriteLine("  auracfg provider  [options]    Manage model providers");
    Console.WriteLine("  auracfg action    [options]    Manage actions");
    Console.WriteLine("  auracfg prompt    [options]    Manage prompt files");
    Console.WriteLine("  auracfg settings  [options]    Manage UI settings");
    Console.WriteLine("  auracfg doctor                 Validate config.json");
    Console.WriteLine("  auracfg restore                Restore config from backup");
    return 1;
}
