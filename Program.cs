using SnmpSimulator.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<GreetCommand>("greet")
        .WithDescription("Greet someone with a message.");
});
return app.Run(args);