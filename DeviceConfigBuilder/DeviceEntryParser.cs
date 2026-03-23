using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SnmpSimulator;

/// <summary>
/// Traverses JSON configuration trees and converts matching oid/tag/value objects into normalized device entries.
/// </summary>
public static class DeviceEntryParser
{
    private const string ModuleIdPropertyName = "id";
    private const string OidPropertyName = "oid";
    private const string TagPropertyName = "tag";
    private const string ValuePropertyName = "value";
    private const string SlotIdPlaceholder = "<slotId>";

    /// <summary>
    /// Extracts all entry objects from a JSON subtree and returns them as normalized config entries.
    /// </summary>
    public static IReadOnlyList<DeviceConfigEntry> ExtractEntriesFromJson(JsonElement element, string source,
        int? slotIndex = null)
    {
        var entries = new List<DeviceConfigEntry>();
        var sourceSegments = new List<SourceSegment>();
        ExtractEntriesFromJson(element, source, sourceSegments, entries, slotIndex);
        return entries;
    }

    /// <summary>
    /// Recursively walks JSON objects and arrays, collecting objects that contain oid/tag/value fields.
    /// </summary>
    private static void ExtractEntriesFromJson(JsonElement element, string sourceRoot,
        IList<SourceSegment> sourceSegments,
        ICollection<DeviceConfigEntry> entries,
        int? slotIndex = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsDeviceConfigEntry(element))
                {
                    entries.Add(ParseEntry(element, sourceRoot, sourceSegments, slotIndex));
                    return;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == ModuleIdPropertyName)
                        continue;

                    sourceSegments.Add(new SourceSegment(property.Name, false));
                    ExtractEntriesFromJson(property.Value, sourceRoot, sourceSegments, entries, slotIndex);
                    sourceSegments.RemoveAt(sourceSegments.Count - 1);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    sourceSegments.Add(new SourceSegment(null, true));
                    ExtractEntriesFromJson(item, sourceRoot, sourceSegments, entries, slotIndex);
                    sourceSegments.RemoveAt(sourceSegments.Count - 1);
                }

                break;
        }
    }

    /// <summary>
    /// Returns true when a JSON object has the shape of a device entry: oid, tag, and value.
    /// </summary>
    private static bool IsDeviceConfigEntry(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(OidPropertyName, out var oidElement)
           && oidElement.ValueKind == JsonValueKind.String
           && element.TryGetProperty(TagPropertyName, out _)
           && element.TryGetProperty(ValuePropertyName, out _);

    /// <summary>
    /// Validates and converts one JSON entry object into a normalized device config entry.
    /// </summary>
    private static DeviceConfigEntry ParseEntry(JsonElement element, string sourceRoot,
        IList<SourceSegment> sourceSegments, int? slotIndex = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Invalid entry in {BuildSource(sourceRoot, sourceSegments)}");
        if (!element.TryGetProperty(OidPropertyName, out var oidElement) ||
            oidElement.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Missing `oid` in {BuildSource(sourceRoot, sourceSegments)}");
        if (!element.TryGetProperty(TagPropertyName, out var tagElement) || !tagElement.TryGetInt32(out var tag))
            throw new InvalidOperationException($"Missing or invalid `tag` in {BuildSource(sourceRoot, sourceSegments)}");
        if (!element.TryGetProperty(ValuePropertyName, out var valueElement))
            throw new InvalidOperationException($"Missing `value` in {BuildSource(sourceRoot, sourceSegments)}");
        if (string.IsNullOrWhiteSpace(oidElement.GetString()))
            throw new InvalidOperationException($"Invalid `oid` in {BuildSource(sourceRoot, sourceSegments)}");
        
        var oid = ReplaceSlotIdPlaceholder(
            oidElement.GetString()!,
            slotIndex);
        var value = valueElement.ToString();
        var snmpData = ConvertToSnmpData(oid, tag, value);

        return new DeviceConfigEntry { Oid = oid, Tag = tag, Value = value, SnmpData = snmpData };
    }

    /// <summary>
    /// Builds a source path string from root and collected recursive segments for diagnostics.
    /// </summary>
    private static string BuildSource(string sourceRoot, IList<SourceSegment> sourceSegments)
    {
        if (sourceSegments.Count == 0)
            return sourceRoot;

        var builder = new StringBuilder(sourceRoot);
        foreach (var segment in sourceSegments)
        {
            if (segment.IsArray)
            {
                builder.Append("[]");
                continue;
            }

            builder.Append('.');
            builder.Append(segment.Name);
        }

        return builder.ToString();
    }

    private readonly record struct SourceSegment(string? Name, bool IsArray);

    /// <summary>
    /// Replaces the &lt;slotId&gt; placeholder with a one-based slot number when a slot index is available.
    /// </summary>
    private static string ReplaceSlotIdPlaceholder(string value, int? slotIndex)
    {
        if (slotIndex is null || string.IsNullOrEmpty(value))
            return value;
        if (!value.Contains(SlotIdPlaceholder, StringComparison.Ordinal))
            return value;

        var slotNumber = slotIndex.Value + 1;
        return value.Replace(SlotIdPlaceholder, slotNumber.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Maps an OID/tag/raw-value triple to the corresponding SharpSnmpLib data type.
    /// </summary>
    private static ISnmpData ConvertToSnmpData(string oid, int tag, string value)
        => tag switch
        {
            2 => new Integer32(ParseInt32Invariant(oid, tag, value)),
            4 => new OctetString(value),
            6 => new ObjectIdentifier(value),
            65 => new Counter32(ParseUInt32Invariant(oid, tag, value)),
            66 => new Gauge32(ParseUInt32Invariant(oid, tag, value)),
            67 => new TimeTicks(ParseUInt32Invariant(oid, tag, value)),
            70 => new Counter64(ParseUInt64Invariant(oid, tag, value)),
            _ => throw new InvalidOperationException($"Unsupported tag '{tag}' for OID '{oid}'.")
        };

    /// <summary>
    /// Parses a signed 32-bit integer using invariant culture and throws a contextual configuration error on failure.
    /// </summary>
    private static int ParseInt32Invariant(string oid, int tag, string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw new InvalidOperationException(
            $"Invalid Integer32 value '{value}' for OID '{oid}' (tag '{tag}').");
    }

    /// <summary>
    /// Parses an unsigned 32-bit integer using invariant culture and throws a contextual configuration error on failure.
    /// </summary>
    private static uint ParseUInt32Invariant(string oid, int tag, string value)
    {
        if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw new InvalidOperationException(
            $"Invalid UInt32 value '{value}' for OID '{oid}' (tag '{tag}').");
    }

    /// <summary>
    /// Parses an unsigned 64-bit integer using invariant culture and throws a contextual configuration error on failure.
    /// </summary>
    private static ulong ParseUInt64Invariant(string oid, int tag, string value)
    {
        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw new InvalidOperationException(
            $"Invalid UInt64 value '{value}' for OID '{oid}' (tag '{tag}').");
    }
}


