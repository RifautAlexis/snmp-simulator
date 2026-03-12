# SNMP Simulator

Local SNMP simulator launcher built on top of LeXtudio's `snmpsim` package.

This project generates `.snmprec` files from JSON definitions, then starts one or more SNMP simulator responders using those generated records.

## What this project does
# SNMP Simulator

Small local toolkit to generate SNMP simulator data and run one device, a full lab, or SNMP walk checks against the generated simulators.

## Install dependencies

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
```

## Run scripts

### start_device.py

Start one SNMP simulator device:

```powershell
python .\start_device.py 127.0.0.1 1161
```

Example with module IDs:

```powershell
python .\start_device.py 127.0.0.1 1161 --module-ids 1,2
```

### start_lab.py

Start multiple devices from [devices_config.json](devices_config.json):

The [devices_config.json](devices_config.json) file is used to configure the devices to launch, including how many devices to start, their base IP address, port, and module assignments.

```powershell
python .\start_lab.py
```

Use a custom config file:

```powershell
python .\start_lab.py --config devices_config.json
```

### snmp_walk_list.py

Run SNMP walk requests against a list of devices:

```powershell
python .\snmp_walk_list.py
```

Example with custom options:

```powershell
python .\snmp_walk_list.py --device-count 2 --ip-address 127.0.0.1 --port 161 --oid 1.3.6.1.4.1 --version 2c
```

