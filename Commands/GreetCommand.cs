using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SnmpSimulator.Commands;

public class GreetSettings : CommandSettings
{
    [CommandArgument(0, "<name>")]
    [Description("The name to greet")]
    public required string Name { get; init; }
  
    [CommandOption("-c|--count")]
    [Description("Number of times to greet")]
    [DefaultValue(1)]
    public int Count { get; init; }
}
  
public class GreetCommand : Command<GreetSettings>
{
    public override int Execute(CommandContext context, GreetSettings settings, CancellationToken cancellation)
    {
        for (var i = 0; i < settings.Count; i++)
        {
            AnsiConsole.MarkupLine($"Hello, [green]{settings.Name}[/]!");
        }
        return 0;
    }
}