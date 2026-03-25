using SnmpSimulator.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<StartDevicesCommand>("startDevices")
        .WithDescription("Instantiate one or multiple devices from a config file.");
});
return await app.RunAsync(args);
