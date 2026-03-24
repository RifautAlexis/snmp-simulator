namespace SnmpSimulator;

/// <summary>
/// Provides filesystem operations used by device configuration loading.
/// </summary>
public static class DeviceConfigFileService
{
    /// <summary>
    /// Validates that the config directory contains a non-empty system file and at least one module JSON file.
    /// </summary>
    public static void ValidateConfigLayout(string configRoot)
    {
        if (!Directory.Exists(configRoot))
            throw new InvalidOperationException($"Missing config directory: {configRoot}");

        var systemPath = Path.Combine(configRoot, Constants.SystemConfigFileName);
        if (!File.Exists(systemPath))
            throw new InvalidOperationException($"Missing system config: {systemPath}");

        if (IsFileEmptyOrWhitespace(systemPath))
            throw new InvalidOperationException($"System config is empty: {systemPath}");

        var modulesPath = Path.Combine(configRoot, Constants.ModulesConfigDirectoryName);
        if (!Directory.Exists(modulesPath))
            throw new InvalidOperationException($"Missing modules directory: {modulesPath}");

        var moduleFiles = Directory.GetFiles(modulesPath, "*.json", SearchOption.TopDirectoryOnly);
        if (moduleFiles.Length == 0)
            throw new InvalidOperationException($"No module JSON files found in: {modulesPath}");
    }

    /// <summary>
    /// Returns true when a file has no content or only whitespace characters.
    /// </summary>
    private static bool IsFileEmptyOrWhitespace(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        if (stream.Length == 0)
            return true;

        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        while (reader.Read() is var current && current != -1)
        {
            if (!char.IsWhiteSpace((char)current))
                return false;
        }

        return true;
    }
}


