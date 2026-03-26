# Config Devices Format

This folder defines the input files used by `startDevices`:

- `devices.json`: list of devices to start
- `system.json`: system OIDs loaded for every device
- `modules/*.json`: module templates selected by each device `moduleIDs`

## Folder Layout

Example layout (this is not mandatory):

```text
config-devices/
  devices.json
  system.json
  modules/
    <module-name>.json
```

`devices.json` can be stored in a different location than `system.json` and `modules/*.json`.
`startDevices` accepts these as separate arguments:

- `<devicesConfig>`: path to `devices.json`
- `<oidsConfigDir>`: directory containing `system.json` and `modules/*.json`

Example with separated paths:

```powershell
.\SnmpSimulator.exe startDevices .\configs\devices.json .\snmp-templates
```

## `devices.json`

`devices.json` must be a JSON array.

Each item supports:

- `moduleIDs` (array of integers, required): ordered module IDs by slot
  - `0` means slot is empty
  - non-zero IDs must match a module file root `id`
- `readCommunity` (string, optional, default: `public`)
- `writeCommunity` (string, optional, default: `private`)

Example:

```json
[
  {
    "moduleIDs": [1, 2, 0, 1],
    "readCommunity": "public",
    "writeCommunity": "private"
  }
]
```

## `system.json`

`system.json` can contain nested objects/arrays. Any object that has all three fields below is treated as an SNMP entry:

- `oid` (string)
- `tag` (integer)
- `value` (string/number; parsed according to `tag`)

Example:

```json
{
  "sysDescr": {
    "oid": "1.3.6.1.2.1.1.1.0",
    "tag": 4,
    "value": "SNMP Simulator Device"
  },
  "sysServices": {
    "oid": "1.3.6.1.2.1.1.7.0",
    "tag": 2,
    "value": "72"
  }
}
```

## `modules/*.json`

Each module file must:

- be a JSON object
- include a root `id` integer (unique across all module files)
- include one or more entry objects with `oid`, `tag`, `value`

For module entries, `oid` may include `<slotId>`. It is replaced at runtime with a one-based slot index from `moduleIDs`.

Example:

```json
{
  "id": 1,
  "metrics": [
    {
      "oid": "1.3.6.1.4.1.55555.3.1.2.<slotId>.1.0",
      "tag": 2,
      "value": "57"
    }
  ]
}
```

## Supported `tag` Values

- `2`: Integer32
- `4`: OctetString
- `6`: ObjectIdentifier
- `65`: Counter32
- `66`: Gauge32
- `67`: TimeTicks
- `70`: Counter64

## Validation Notes

At load time, the simulator validates:

- config directory exists
- `system.json` exists and is not empty
- `modules/` exists and contains at least one `*.json`
- module root `id` exists and is unique
- each SNMP entry has valid `oid`, `tag`, and `value`
