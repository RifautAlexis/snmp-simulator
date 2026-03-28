using System.Text.Json;

namespace SnmpSimulator;

public sealed class DeviceConfigEntry
{
    public required string Oid { get; init; }
    public required int Tag { get; init; }
    public required string Value { get; init; }
    public required ISnmpData SnmpData { get; init; }
}

public static class DeviceConfigLoader
{
    /// <summary>
    /// Loads system and selected module JSON configs and returns SNMP objects.
    /// </summary>
    public static IReadOnlyCollection<SnmpObject> LoadFromConfigDevice(string configDeviceDirectory, int[] selectedModuleIds)
    {
        var configRoot = Path.GetFullPath(configDeviceDirectory);
        DeviceConfigFileService.ValidateConfigLayout(configRoot);

        var systemPath = Path.Combine(configRoot, Constants.SystemConfigFileName);
        var modulesPath = Path.Combine(configRoot, Constants.ModulesConfigDirectoryName);

        using var systemStream = File.OpenRead(systemPath);
        using var systemContent = JsonDocument.Parse(systemStream);

        // Extract and normalize JSON to SnmpObject
        var systemEntries = DeviceEntryParser.ExtractEntriesFromJson(systemContent.RootElement, "system");

        // Build selected modules and normalize JSON entries to SNMP-ready objects.
        var modulesEntries = ModuleEntryBuilder.BuildEntriesFromModules(modulesPath, selectedModuleIds);

        var deviceOidList = new List<DeviceConfigEntry>(systemEntries.Count + modulesEntries.Count);
        deviceOidList.AddRange(systemEntries);
        deviceOidList.AddRange(modulesEntries);

        var snmpObjectList = SnmpOutputBuilder.BuildSnmpOutputs(deviceOidList);
        
        return snmpObjectList;
    }
}
