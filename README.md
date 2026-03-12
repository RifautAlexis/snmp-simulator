# SNMP Simulator

Minimal project to run the maintained LeXtudio SNMP simulator locally with your own `.snmprec` data.

## Package used

This project uses the maintained `snmpsim` package from LeXtudio.

Install source:

- PyPI package: `snmpsim`
- CLI used: `snmpsim-command-responder`

## Prerequisites

- Windows PowerShell
- Python 3.10+ installed

## Project layout

```text
snmp-simulator/
├─ README.md
├─ requirements.txt
├─ start_sim.py
└─ data/
   ├─ device1.snmprec
   ├─ system_oid.json
   └─ modules_catalog.json
```

- [data](data) is always the simulator data root for this project.
- Each `.snmprec` file is one simulated device profile.
- In this project, [data/device1.snmprec](data/device1.snmprec) is the current sample device.

## Setup

From the project root in PowerShell:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
```

To update dependencies later:

```powershell
python -m pip install -r requirements.txt
```

## Start the simulator

Use the Typer-based Python launcher at [start_sim.py](start_sim.py). It always runs SNMP simulator with:

- `--data-dir=./data`
- `--agent-udpv4-endpoint=<IP>:<PORT>`

At startup, it generates [data/oid.snmprec](data/oid.snmprec) from:

- system OIDs in [data/system_oid.json](data/system_oid.json)
- optional module IDs from [data/modules_catalog.json](data/modules_catalog.json)

### Using the Python launcher

Run:

```powershell
python .\start_sim.py 127.0.0.1 16100
```

To build [data/oid.snmprec](data/oid.snmprec) from module IDs and then start the simulator:

```powershell
python .\start_sim.py 127.0.0.1 16100 --module-ids 1,2,3
```

JSON-style arrays are also accepted:

```powershell
python .\start_sim.py 127.0.0.1 16100 --module-ids "[1, 2, 3]"
```

If your virtual environment is active, this uses the project dependencies directly.

Typer also gives automatic help:

```powershell
python .\start_sim.py --help
```

Arguments:

- first argument: IP address
- second argument: UDP port

Example:

```bash
python ./start_sim.py 0.0.0.0 16100
```

This makes the simulator listen on all interfaces on UDP port `16100`.

### Run manually from PowerShell

If you do not want to use the launcher, run:

```powershell
.\.venv\Scripts\python.exe -m snmpsim.commands.responder --data-dir=./data --agent-udpv4-endpoint=127.0.0.1:16100
```

You can also use the installed CLI:

```powershell
snmpsim-command-responder --data-dir=./data --agent-udpv4-endpoint=127.0.0.1:16100
```

## Test the simulator

Once started, query it with your SNMP client.

For the sample file [data/device1.snmprec](data/device1.snmprec), the SNMP v1/v2c community name is typically `device1`.

Example with Net-SNMP:

```powershell
snmpwalk -v2c -c device1 127.0.0.1:16100 1.3.6.1.2.1.1
```

Example expected data includes:

- `1.3.6.1.2.1.1.1.0` → device description
- `1.3.6.1.2.1.1.3.0` → uptime
- `1.3.6.1.2.1.1.5.0` → device name

## Simulation data notes

SNMP simulator reads records in this form:

```text
OID|TAG|VALUE
```

Example from [data/device1.snmprec](data/device1.snmprec):

```text
1.3.6.1.2.1.1.1.0|4|Simulated SNMP Device
1.3.6.1.2.1.1.3.0|67|1000
1.3.6.1.2.1.1.5.0|4|device1
```

You can keep values static, or use SNMP simulator variation modules to make them dynamic.

### Module-based merge into oid.snmprec

If you use `--module-ids`, the launcher reads [data/modules_catalog.json](data/modules_catalog.json).

Catalog shape:

```json
{
   "modules": {
      "1": {
         "alarms": [
            { "oid": "1.3.6.1...", "tag": "2", "value": "0" }
         ],
         "metrics": [
            { "oid": "1.3.6.1...", "tag": "67", "value": "1000" }
         ]
      }
   }
}
```

Behavior:

- selected module IDs are read in the order given
- `alarms` and `metrics` for those modules are concatenated into `.snmprec` lines
- the merged result is written to [data/oid.snmprec](data/oid.snmprec)
- duplicate OIDs across selected modules are rejected

## Recreate a clean virtual environment

```powershell
Remove-Item -Recurse -Force .venv
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
```
