using SnmpSimulator.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<StartDeviceCommand>("startDevice")
        .WithDescription("Instantiate a single device.");
    config.AddCommand<StartDevicesCommand>("startDevices")
        .WithDescription("Instantiate multiple devices.");
});
return await app.RunAsync(args);
