namespace SnmpSimulator;

/// <summary>
/// Builds runtime SNMP objects from normalized device entries.
/// </summary>
public static class SnmpOutputBuilder
{
    /// <summary>
    /// Builds runtime SNMP objects in a single pass over normalized entries.
    /// </summary>
    public static IReadOnlyList<SnmpObject> BuildSnmpOutputs(
        IReadOnlyList<DeviceConfigEntry> entries)
    {
        var snmpObjects = new List<SnmpObject>(entries.Count);

        foreach (var entry in entries)
        {
            snmpObjects.Add(new SnmpObject(entry.Oid, entry.SnmpData));
        }

        return snmpObjects;
    }
}


