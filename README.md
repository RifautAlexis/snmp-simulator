# SNMP Simulator

A small SNMP simulator built on .NET and SharpSnmpLib.

## Run

```powershell
dotnet run --project "C:\Users\rifaut\Desktop\work\clone\snmp-simulator\SnmpSimulator.csproj" -- startDevice
```

Optional command options:

- `--config-dir <path>`: path to the config root directory (default: `configDevice`)
- `--read-community <value>`: SNMP read community (default: `public`)
- `--write-community <value>`: SNMP write community (default: `private`)

## Config Structure (`configDevice`)

The simulator reads:

- `configDevice/system.json`
- all `configDevice/modules/*.json` files (sorted by filename)

If the same OID appears multiple times, later module files override earlier values.
After each successful load/reload, a merged output file is generated: `device-config.snmprec`.

Example layout:

```text
configDevice/
  system.json
  modules/
    module01.json
    module02.json
device-config.snmprec
```

`system.json` entry example:

```json
{
  "sysDescr": {
    "oid": "1.3.6.1.4.1.1.1.0",
    "tag": "4",
    "value": "SNMP Simulator Device"
  }
}
```

`modules/module01.json` entry example:

```json
{
  "metrics": [
    {
      "oid": "1.3.6.1.4.1.9999.1.2.1.0",
      "tag": "2",
      "value": "42"
    }
  ]
}
```

Supported SNMP ASN.1 `tag` values:

- `2` (Integer32)
- `4` (OctetString)
- `6` (ObjectIdentifier)
- `65` (Counter32)
- `66` (Gauge32)
- `67` (TimeTicks)
- `70` (Counter64)

## Live Reload

The simulator watches `configDevice` recursively. Creating, updating, renaming, or deleting `system.json` or files under
`modules/*.json` triggers a hot reload and regenerates `device-config.snmprec`.

## OID Definitions

1.3.6.1.4.1.55555
├── 1 → system (extensions)
└── 2 → module table

### System extension

| OID  | Name            | Type   | Meaning      |
|------|-----------------|--------|--------------|
| .1.0 | deviceType      | string | product type |
| .2.0 | serialNumber    | string | serial       |
| .3.0 | softwareVersion | string | firmware     |
| .4.0 | hardwareVersion | string | hardware     |

### Module Table

1.3.6.1.4.1.55555.2.1.<column>.<row>

#### Module Static OID

| OID  | Name            | Type | Meaning     |
|------|-----------------|------|-------------|
| .1.0 | slotNumber      | int  | slot number |
| .2.0 | moduleType      | int  | module type |
| .3.0 | firmwareVersion | int  | firmware    |
| .4.0 | hardwareVersion | int  | hardware    |

### Metric Table

1.3.6.1.4.1.55555.3.1.<column>.<row>.<metric index>
<column> represents the column name (e.g., temperature)
<row> represent the physical slot number
<metric index> represents the index of the metric for that slot, allowing multiple metrics per module

#### Tabular OID

| OID | Name        | Type          | Meaning      |
|-----|-------------|---------------|--------------|
| .1  | metricName  | string        | metric name  |
| .2  | metricValue | int OR string | metric value |
| .3  | metricUnit  | string        | metric unit  |

### Alarms Table

1.3.6.1.4.1.55555.4.1.<column>.<row>.<alarm index>
<column> represents the column name (e.g., alarmStatus)
<row> represent the physical slot number
<alarm index> represents the index of the alarm for that slot, allowing multiple alarms per module

#### Static column

| OID | Name             | Type   | Meaning                                 |
|-----|------------------|--------|-----------------------------------------|
| .1  | alarmDescription | string | Description of the alarm                |
| .2  | alarmStatus      | int    | 0=clear, 1=active                       |
| .3  | alarmSeverity    | int    | 1=nominal, 2=minor, 3=major, 4=critical |

##### Example OID

*Get status of the sixth alarm on slot 5* => 1.3.6.1.4.1.55555.4.1.2.5.6.0
*Get severity of the ninth alarm on slot 3* => 1.3.6.1.4.1.55555.4.1.3.3.9.0