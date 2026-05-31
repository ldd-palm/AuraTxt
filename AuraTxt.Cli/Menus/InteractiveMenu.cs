using AuraTxt.Core.Services;
namespace AuraTxt.Cli.Menus;
public class InteractiveMenu(ConfigService config)
{
    public Task RunAsync() => Task.CompletedTask;
}
