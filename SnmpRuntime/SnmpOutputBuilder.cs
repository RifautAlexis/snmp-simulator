namespace SnmpSimulator;

/// <summary>
/// Builds SNMPREC lines and runtime SNMP objects from normalized device entries.
/// </summary>
public static class SnmpOutputBuilder
{
    /// <summary>
    /// Builds SNMPREC lines and runtime SNMP objects in a single pass over normalized entries.
    /// </summary>
    public static (IReadOnlyList<string> SnmprecLines, IReadOnlyList<SnmpObject> SnmpObjects) BuildSnmpOutputs(
        IReadOnlyList<DeviceConfigEntry> entries)
    {
        var snmprecLines = new List<string>(entries.Count);
        var snmpObjects = new List<SnmpObject>(entries.Count);

        foreach (var entry in entries)
        {
            snmprecLines.Add($"{entry.Oid}|{entry.Tag}|{entry.Value}");
            snmpObjects.Add(new SnmpObject(entry.Oid, entry.SnmpData));
        }

        return (snmprecLines, snmpObjects);
    }
}


