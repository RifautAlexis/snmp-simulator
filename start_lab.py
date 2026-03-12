from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys

import typer

CONFIG_FILE_NAME = "devices_config.json"

def main(
	config_file: str = typer.Option(CONFIG_FILE_NAME, "--config", help="Device configuration JSON file."),
) -> None:
	"""Launch multiple SNMP device simulators by calling start_device.py X times."""

	project_dir = Path(__file__).resolve().parent
	start_device_path = project_dir / "start_device.py"
	config_path = project_dir / config_file

	if not start_device_path.exists():
		raise typer.BadParameter(f"Launcher not found: {start_device_path}")
	if not config_path.exists():
		raise typer.BadParameter(f"Config file not found: {config_path}")

	try:
		config_payload = json.loads(config_path.read_text(encoding="utf-8"))
	except json.JSONDecodeError as exc:
		raise typer.BadParameter(f"Invalid JSON in {config_path}: {exc}") from exc

	if not isinstance(config_payload, dict):
		raise typer.BadParameter(f"Invalid config format in {config_path}: expected object")

	device_count = config_payload.get("device_count")
	ip_address = config_payload.get("ip_address")
	port = config_payload.get("port")
	devices_config = config_payload.get("devices_config")

	if not isinstance(device_count, int) or not (1 <= device_count <= 255):
		raise typer.BadParameter("Config field 'device_count' must be an integer between 1 and 255")
	if not isinstance(ip_address, str) or not ip_address.strip():
		raise typer.BadParameter("Config field 'ip_address' must be a non-empty string")
	if not isinstance(port, int) or not (1 <= port <= 65535):
		raise typer.BadParameter("Config field 'port' must be an integer between 1 and 65535")
	if not isinstance(devices_config, list):
		raise typer.BadParameter("Config field 'devices_config' must be a list")
	if len(devices_config) < device_count:
		raise typer.BadParameter(
			f"Config field 'devices_config' must contain at least {device_count} entries"
		)

	ip_parts = ip_address.split(".")
	if len(ip_parts) != 4:
		raise typer.BadParameter(f"Invalid IPv4 address: {ip_address}")

	try:
		octets = [int(part) for part in ip_parts]
	except ValueError as exc:
		raise typer.BadParameter(f"Invalid IPv4 address: {ip_address}") from exc

	if any(part < 0 or part > 255 for part in octets):
		raise typer.BadParameter(f"Invalid IPv4 address: {ip_address}")

	base_last_octet = octets[3]
	last_needed_octet = base_last_octet + device_count - 1
	if last_needed_octet > 255:
		raise typer.BadParameter(
			f"IP range overflow: base IP {ip_address} with device_count={device_count} exceeds .255"
		)

	processes: list[subprocess.Popen[bytes]] = []

	for index in range(device_count):
		device_ip = f"{octets[0]}.{octets[1]}.{octets[2]}.{base_last_octet + index}"
		device_name = device_ip.replace('.', '_')
		output_file = f"{device_name}/{device_name}.snmprec"
		module_ids = devices_config[index]
		if not isinstance(module_ids, list) or not all(isinstance(module_id, int) for module_id in module_ids):
			raise typer.BadParameter(
				f"devices_config[{index}] must be a list of integers"
			)

		command = [
			sys.executable,
			str(start_device_path),
			device_ip,
			str(port),
			"--output-file", output_file,
            "--module-ids", ",".join(str(module_id) for module_id in module_ids),
		]

		typer.echo("=========================")
		process = subprocess.Popen(
			command,
			cwd=project_dir,
		)
		processes.append(process)
		typer.echo(f"Started device #{index + 1} on {device_ip}:{port} (pid={process.pid})")
		typer.echo("IP=%s PORT=%s COMMUNITY=%s" % (device_ip, port, device_name))
		typer.echo("Config used: %s" % (module_ids,))
		typer.echo("=========================")

	typer.echo(f"Launched {len(processes)} simulator process(es). Press Ctrl+C to stop all.")

	try:
		for process in processes:
			process.wait()
	except KeyboardInterrupt:
		typer.echo("Stopping all simulator processes...")
		for process in processes:
			if process.poll() is None:
				process.terminate()
		for process in processes:
			if process.poll() is None:
				process.wait(timeout=5)


if __name__ == "__main__":
	typer.run(main)
