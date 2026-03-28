namespace SnmpSimulator;

public class SnmpObject
{
    public string Oid { get; }
    public ISnmpData Value { get; set; }

    public SnmpObject(string oid, ISnmpData value)
    {
        Oid = oid;
        Value = value;
    }
}
