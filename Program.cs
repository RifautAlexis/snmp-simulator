using SnmpSimulator.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<StartDeviceCommand>("startDevice")
        .WithDescription("Instantiate a number of device.");
});
return await app.RunAsync(args);
