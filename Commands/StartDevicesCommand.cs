using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnmpSimulator.Commands;

public class DeviceConfig
{
    [JsonPropertyName("moduleIDs")]
    public int[] ModuleIds { get; set; } = Array.Empty<int>();

    [JsonPropertyName("readCommunity")]
    public string ReadCommunity { get; set; } = Constants.ReadCommunity;

    [JsonPropertyName("writeCommunity")]
    public string WriteCommunity { get; set; } = Constants.WriteCommunity;
}

[JsonSerializable(typeof(List<DeviceConfig>))]
public partial class DeviceConfigContext : JsonSerializerContext
{
}

public class StartDevicesSettings : CommandSettings
{
    [CommandArgument(0, "<devicesConfig>")]
    [Description("Path to JSON file containing device configurations")]
    public string DevicesConfigFile { get; init; } = "";

    [CommandArgument(1, "<oidsConfigDir>")]
    [Description("Path to OID configuration directory (expects system.json and modules/*.json)")]
    public string OidsConfigDirectory { get; init; } = "";

    [CommandArgument(2, "[ipaddress]")]
    [Description("Base IP address for devices (default: 127.0.0.1, last octet will be incremented)")]
    [DefaultValue("127.0.0.1")]
    public string IpAddress { get; init; } = "127.0.0.1";

    [CommandArgument(3, "[port]")]
    [Description("SNMP UDP port used by all devices (default: 161)")]
    [DefaultValue(161)]
    public int Port { get; init; } = 161;
}

public class StartDevicesCommand : AsyncCommand<StartDevicesSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StartDevicesSettings settings, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(settings.DevicesConfigFile))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Devices config file path is required");
            return 1;
        }

        if (!File.Exists(settings.DevicesConfigFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Devices config file not found: {settings.DevicesConfigFile}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.OidsConfigDirectory))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] OIDs config directory path is required");
            return 1;
        }

        var configDirectory = Path.GetFullPath(settings.OidsConfigDirectory);
        if (!Directory.Exists(configDirectory))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] OIDs config directory not found: {configDirectory}");
            return 1;
        }

        var systemConfigPath = Path.Combine(configDirectory, "system.json");
        if (!File.Exists(systemConfigPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Missing system OID file: {systemConfigPath}");
            return 1;
        }

        var modulesConfigPath = Path.Combine(configDirectory, "modules");
        if (!Directory.Exists(modulesConfigPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Missing modules directory: {modulesConfigPath}");
            return 1;
        }

        // Read the config file
        List<DeviceConfig> deviceConfigs;
        try
        {
            var json = File.ReadAllText(settings.DevicesConfigFile);
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            deviceConfigs = JsonSerializer.Deserialize(json, typeof(List<DeviceConfig>), new DeviceConfigContext(options)) as List<DeviceConfig> ?? new List<DeviceConfig>();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to read config file: {ex.Message}");
            return 1;
        }

        if (deviceConfigs.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No devices found in config file");
            return 1;
        }

        // Parse the base IP address
        if (!System.Net.IPAddress.TryParse(settings.IpAddress, out var baseIp))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid IP address: {settings.IpAddress}");
            return 1;
        }

        if (settings.Port is < 1 or > 65535)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid port: {settings.Port}. Expected range is 1-65535");
            return 1;
        }

        AnsiConsole.MarkupLine($"[yellow]Starting {deviceConfigs.Count} device(s)[/]");
        AnsiConsole.MarkupLine($"[yellow]Base IP address:[/] {settings.IpAddress}");
        AnsiConsole.MarkupLine($"[yellow]Port:[/] {settings.Port}");
        AnsiConsole.MarkupLine($"[yellow]Devices config file:[/] {settings.DevicesConfigFile}");
        AnsiConsole.MarkupLine($"[yellow]OIDs config directory:[/] {configDirectory}");

        var tasks = new List<Task>();

        for (int i = 0; i < deviceConfigs.Count; i++)
        {
            var deviceConfig = deviceConfigs[i];
            var ipAddress = IncrementLastOctet(settings.IpAddress, i);
            var store = new SnmpStore();
            LoadConfigIntoStore(configDirectory, deviceConfig.ModuleIds, store);

            var agent = new SnmpAgent(ipAddress, settings.Port, store, deviceConfig.ReadCommunity, deviceConfig.WriteCommunity);
            AnsiConsole.MarkupLine($"[green]Device {i + 1}:[/] IP {ipAddress}:{settings.Port}, Modules: {(deviceConfig.ModuleIds.Length > 0 ? string.Join(", ", deviceConfig.ModuleIds) : "none")}, Read: {deviceConfig.ReadCommunity}, Write: {deviceConfig.WriteCommunity}");

            tasks.Add(agent.StartAsync());
        }

        await Task.WhenAll(tasks);

        return 0;
    }

    private string IncrementLastOctet(string ipAddress, int increment)
    {
        var parts = ipAddress.Split('.');
        if (parts.Length != 4 || !int.TryParse(parts[3], out var lastOctet))
        {
            throw new ArgumentException($"Invalid IP address format: {ipAddress}");
        }

        var newLastOctet = lastOctet + increment;
        if (newLastOctet > 255)
        {
            throw new ArgumentException($"IP address last octet would exceed 255: {newLastOctet}");
        }

        return $"{parts[0]}.{parts[1]}.{parts[2]}.{newLastOctet}";
    }

    private void LoadConfigIntoStore(string configDirectory, int[] moduleIds, SnmpStore store)
    {
        var objects = DeviceConfigLoader.LoadFromConfigDevice(configDirectory, moduleIds);
        store.ReplaceAll(objects);
    }
}



