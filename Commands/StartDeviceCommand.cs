using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace SnmpSimulator.Commands;

public class StartDeviceSettings : CommandSettings
{
    [CommandOption("--module-ids")]
    [Description("Comma-separated list of module IDs to load (e.g., 1,2,3)")]
    [DefaultValue("")]
    public string ModuleIds { get; init; } = "";

    [CommandOption("--read-community")]
    [Description("SNMP read community string")]
    [DefaultValue(Constants.ReadCommunity)]
    public string ReadCommunity { get; init; } = Constants.ReadCommunity;

    [CommandOption("--write-community")]
    [Description("SNMP write community string")]
    [DefaultValue(Constants.WriteCommunity)]
    public string WriteCommunity { get; init; } = Constants.WriteCommunity;
}

public class StartDeviceCommand : AsyncCommand<StartDeviceSettings>
{
    private readonly SnmpStore _store = new();

    public override async Task<int> ExecuteAsync(CommandContext context, StartDeviceSettings settings, CancellationToken cancellation)
    {
        var configDirectory = Path.GetFullPath(Constants.ConfigDirectory);
        var outputPath = Path.Combine(Directory.GetParent(configDirectory)?.FullName ?? Directory.GetCurrentDirectory(), Constants.OutputFileName);

        var moduleIds = string.IsNullOrWhiteSpace(settings.ModuleIds)
            ? Array.Empty<int>()
            : settings.ModuleIds.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                .Where(id => id >= 0)
                .ToArray();


        LoadConfigIntoStore(configDirectory, outputPath, moduleIds);

        if (moduleIds.Length > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Module IDs:[/] {string.Join(", ", moduleIds)}");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]None modules specified.[/]");
        }

        AnsiConsole.MarkupLine($"[yellow]Read - Write community:[/] {settings.ReadCommunity} - {settings.WriteCommunity}");
        AnsiConsole.MarkupLine($"[yellow]Device config found in directory:[/] {configDirectory}");
        AnsiConsole.MarkupLine($"[yellow]Device config created at :[/] {outputPath}");

        var agent = new SnmpAgent("127.0.0.1", 161, _store, settings.ReadCommunity, settings.WriteCommunity);

        await agent.StartAsync();

        return 0;
    }

    private void LoadConfigIntoStore(string configDirectory, string outputPath, int[] moduleIds)
    {
        var objects = DeviceConfigLoader.LoadFromConfigDevice(configDirectory, outputPath, moduleIds);
        _store.ReplaceAll(objects);
    }
}