# SNMP Simulator

A small SNMP simulator built on .NET and SharpSnmpLib.

## Run

```powershell
dotnet run -- startDevice --help
```

Optional command options:

- `--module-ids <csv>`: comma-separated module IDs by slot order (example: `0,1,2,1`)
  - each value has an index in the list, and `slot = index + 1`
  - `0` means an empty slot (no module loaded for that slot)
  - any non-zero value is matched against a module file root `id`
- `--read-community <value>`: SNMP read community (default: `public`)
- `--write-community <value>`: SNMP write community (default: `private`)

## Config Structure (`config-devices`)

The simulator reads:

- `config-devices/system.json`
- all `config-devices/modules/*.json` files (indexed by root `id`)

Each selected non-zero module ID from `--module-ids` maps to one slot.
`0` keeps the slot empty.
The `<slotId>` placeholder is replaced with the one-based slot index.
Module files must have unique root `id` values.
After each successful load, entries are normalized and loaded into the in-memory SNMP store.

Example layout:

```text
config-devices/
  system.json
  modules/
    module01.json
    module02.json
```

Supported SNMP ASN.1 `tag` values:

- `2` (Integer32)
- `4` (OctetString)
- `6` (ObjectIdentifier)
- `65` (Counter32)
- `66` (Gauge32)
- `67` (TimeTicks)
- `70` (Counter64)


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