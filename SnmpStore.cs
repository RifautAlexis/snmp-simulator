namespace SnmpSimulator;

public enum SnmpSetResult
{
    Success,
    NotFound,
    NotWritable
}

public class SnmpStore
{
    private readonly object _sync = new();
    private Dictionary<string, SnmpObject> _store = new();

    public void Add(SnmpObject obj)
    {
        lock (_sync)
        {
            _store[obj.Oid] = obj;
        }
    }

    public void ReplaceAll(IEnumerable<SnmpObject> objects)
    {
        var next = new Dictionary<string, SnmpObject>();
        foreach (var item in objects)
        {
            next[item.Oid] = item;
        }

        lock (_sync)
        {
            _store = next;
        }
    }

    public SnmpObject? Get(string oid)
    {
        lock (_sync)
        {
            return _store.TryGetValue(oid, out var obj)
                ? new SnmpObject(obj.Oid, obj.Value)
                : null;
        }
    }

    public SnmpSetResult Set(string oid, ISnmpData value)
    {
        lock (_sync)
        {
            if (!_store.TryGetValue(oid, out var obj))
            {
                return SnmpSetResult.NotFound;
            }

            obj.Value = value;
            return SnmpSetResult.Success;
        }
    }

    public SnmpObject? GetNext(string oid)
    {
        lock (_sync)
        {
            string? nextOid = null;

            foreach (var candidateOid in _store.Keys)
            {
                if (CompareOid(candidateOid, oid) <= 0)
                {
                    continue;
                }

                if (nextOid is null || CompareOid(candidateOid, nextOid) < 0)
                {
                    nextOid = candidateOid;
                }
            }

            if (nextOid is null)
            {
                return null;
            }

            var obj = _store[nextOid];
            return new SnmpObject(obj.Oid, obj.Value);
        }
    }

    private static int CompareOid(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var maxLength = Math.Min(leftParts.Length, rightParts.Length);

        for (var i = 0; i < maxLength; i++)
        {
            var leftValue = ParseOidPart(leftParts[i]);
            var rightValue = ParseOidPart(rightParts[i]);
            var partComparison = leftValue.CompareTo(rightValue);

            if (partComparison != 0)
            {
                return partComparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static ulong ParseOidPart(string part)
    {
        return ulong.TryParse(part, out var value) ? value : 0;
    }
}