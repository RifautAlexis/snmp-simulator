namespace SnmpSimulator;

/// <summary>
/// Builds selected modules by slot order and converts matching module templates to normalized device entries.
/// </summary>
public static class ModuleEntryBuilder
{
    /// <summary>
    /// Builds module-derived entries for selected slots, skipping slot id 0.
    /// </summary>
    public static IReadOnlyList<DeviceConfigEntry> BuildEntriesFromModules(string modulesPath,
        int[] selectedModuleIds)
    {
        var moduleTemplateRegistry = ModuleTemplateRegistry.Build(modulesPath);

        var entries = new List<DeviceConfigEntry>();

        foreach (var moduleSelection in moduleTemplateRegistry.BuildSelectedModules(selectedModuleIds))
        {
            entries.AddRange(DeviceEntryParser.ExtractEntriesFromJson(
                moduleSelection.RootElement,
                moduleSelection.Source,
                moduleSelection.SlotIndex));
        }

        return entries;
    }
}

