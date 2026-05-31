using AuraTxt.Cli.Commands;
using AuraTxt.Cli.Menus;
using AuraTxt.Core.Services;

var configService = new ConfigService();

if (args.Length == 0)
{
    await new InteractiveMenu(configService).RunAsync();
    return 0;
}

return args[0] switch
{
    "model"    => await new ModelCommand(configService).ExecuteAsync(args[1..]),
    "action"   => await new ActionCommand(configService).ExecuteAsync(args[1..]),
    "settings" => await new SettingsCommand(configService).ExecuteAsync(args[1..]),
    _          => Help()
};

static int Help()
{
    Console.WriteLine("auracfg — AuraTxt 配置工具");
    Console.WriteLine("用法:");
    Console.WriteLine("  auracfg                        交互式菜单");
    Console.WriteLine("  auracfg model    [--list|--set|--update|--delete]");
    Console.WriteLine("  auracfg action   [--list|--set|--update|--delete]");
    Console.WriteLine("  auracfg settings [--show|--set]");
    return 1;
}
