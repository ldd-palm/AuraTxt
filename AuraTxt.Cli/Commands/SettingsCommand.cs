using AuraTxt.Core.Services;
namespace AuraTxt.Cli.Commands;
public class SettingsCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(0);
}
