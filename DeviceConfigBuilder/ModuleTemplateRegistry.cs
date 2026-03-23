using System.Text.Json;

namespace SnmpSimulator;

/// <summary>
/// Loads module JSON templates and provides fast lookup by module id for slot expansion.
/// </summary>
public sealed class ModuleTemplateRegistry
{
    private const string ModuleIdPropertyName = "id";

    private readonly ILookup<int, ModuleTemplate> _templatesById;

    private ModuleTemplateRegistry(ILookup<int, ModuleTemplate> templatesById)
    {
        _templatesById = templatesById;
    }

    /// <summary>
    /// Builds a registry from module JSON files and validates that root ids are unique.
    /// </summary>
    public static ModuleTemplateRegistry Build(string modulesPath)
    {
        var moduleTemplates = Directory.GetFiles(modulesPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(LoadModuleTemplate)
            .ToList();

        EnsureUniqueModuleIds(moduleTemplates);

        return new ModuleTemplateRegistry(moduleTemplates.ToLookup(template => template.ModuleId));
    }

    /// <summary>
    /// Builds selected module ids into module templates by slot order, skipping slot id 0.
    /// </summary>
    public IEnumerable<ModuleSelection> BuildSelectedModules(int[] selectedModuleIds)
    {
        for (var slotIndex = 0; slotIndex < selectedModuleIds.Length; slotIndex++)
        {
            var selectedModuleId = selectedModuleIds[slotIndex];
            
            if (selectedModuleId == 0)
                continue;

            foreach (var moduleTemplate in _templatesById[selectedModuleId])
            {
                yield return new ModuleSelection(
                    moduleTemplate.RootElement,
                    $"{moduleTemplate.Source}[slot:{slotIndex}]",
                    slotIndex);
            }
        }
    }

    /// <summary>
    /// Ensures each module id is declared by a single module file to avoid ambiguous slot expansion.
    /// </summary>
    private static void EnsureUniqueModuleIds(IReadOnlyCollection<ModuleTemplate> moduleTemplates)
    {
        var duplicateGroups = moduleTemplates
            .GroupBy(template => template.ModuleId)
            .Where(group => group.Count() > 1)
            .ToList();

        if (duplicateGroups.Count == 0)
            return;

        var duplicateDetails = string.Join(
            "; ",
            duplicateGroups.Select(group =>
                $"id={group.Key} -> {string.Join(", ", group.Select(template => template.Source).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}"));

        throw new InvalidOperationException(
            $"Duplicate module `id` detected in `{Constants.ModulesConfigDirectoryName}`: {duplicateDetails}");
    }

    /// <summary>
    /// Loads a module JSON file as a reusable template and reads its root module id.
    /// </summary>
    private static ModuleTemplate LoadModuleTemplate(string moduleFilePath)
    {
        using var moduleStream = File.OpenRead(moduleFilePath);
        using var moduleContent = JsonDocument.Parse(moduleStream);
        
        if (!moduleContent.RootElement.TryGetProperty(ModuleIdPropertyName, out var idElement) ||
            !idElement.TryGetInt32(out var moduleId))
            throw new InvalidOperationException($"Missing or invalid root `id` in module file: {moduleFilePath}");

        return new ModuleTemplate(moduleId, moduleContent.RootElement.Clone(), Path.GetFileName(moduleFilePath));
    }

    private readonly record struct ModuleTemplate(int ModuleId, JsonElement RootElement, string Source);

    public readonly record struct ModuleSelection(JsonElement RootElement, string Source, int SlotIndex);
}



