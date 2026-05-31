using AuraTxt.Core.Services;
namespace AuraTxt.Cli.Commands;
public class ModelCommand(ConfigService config)
{
    public Task<int> ExecuteAsync(string[] args) => Task.FromResult(0);
}
