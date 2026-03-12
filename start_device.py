from __future__ import annotations

from pathlib import Path
import subprocess
import sys

import typer

from utils import generate_module_oids, generate_system_oids, parse_module_ids


OUTPUT_FILE_NAME = "oid.snmprec"
SYSTEM_OID_FILE_NAME = "system_oid.json"
MODULES_CATALOG_NAME = "modules_catalog.json"

def main(
    ip_address: str = typer.Argument(..., help="IPv4 address to bind the SNMP simulator to."),
    port: int = typer.Argument(..., min=1, max=65535, help="UDP port to listen on."),
    module_ids: str | None = typer.Option(
        None,
        "--module-ids",
        help="Module IDs to merge into the output .snmprec file. Accepts 1,2,3 or [1, 2, 3].",
    ),
    output_file: str = typer.Option(
        OUTPUT_FILE_NAME,
        "--output-file",
        help="Output .snmprec file path relative to the data directory. Can include subdirectories (e.g., device_1/device_1).",
    ),
) -> None:
    """Start the SNMP simulator.

    This command always uses the local `data` folder.

    It creates `data/oid.snmprec` from `data/system_oid.json`
    and `data/modules_catalog.json`, then starts the simulator.

    If `--module-ids` is provided, it first creates `data/oid.snmprec`
    from `data/modules_catalog.json`, then starts the simulator.
    """
    # Resolve all paths relative to this script so the command works
    # no matter where it is launched from.
    project_dir = Path(__file__).resolve().parent
    data_dir = project_dir / "data"
    system_path = data_dir / SYSTEM_OID_FILE_NAME
    catalog_path = data_dir / MODULES_CATALOG_NAME

    output_file_path = output_file.strip()
    if not output_file_path:
        raise typer.BadParameter("Output file path must not be empty", param_hint="--output-file")
    if not output_file_path.endswith(".snmprec"):
        output_file_path = f"{output_file_path}.snmprec"

    output_path = data_dir / output_file_path
    output_path.parent.mkdir(parents=True, exist_ok=True)

    community_name = Path(output_file_path).stem
    simulator_data_dir = output_path.parent
    endpoint = f"{ip_address}:{port}"

    if not data_dir.exists():
        raise typer.BadParameter(f"Data directory not found: {data_dir}")
    
    # Step 1: Generate system OIDs from `data/system_oid.json`.
    system_lines_to_add, seen_oids = generate_system_oids(system_path)
    all_lines_to_write = list(system_lines_to_add)

    # Step 2: Generate module OIDs from `data/modules_catalog.json`.
    parsed_module_ids = parse_module_ids(module_ids)
    if parsed_module_ids:
        module_lines_to_add = generate_module_oids(catalog_path, seen_oids, parsed_module_ids)
        all_lines_to_write.extend(module_lines_to_add)

    rendered_content = "\n".join(all_lines_to_write) + "\n"
    output_path.write_text(rendered_content, encoding="utf-8")

    # Delegate the actual SNMP server process to the installed snmpsim package.
    command = [
        sys.executable,
        "-m",
        "snmpsim.commands.responder",
        f"--data-dir={simulator_data_dir}",
        f"--agent-udpv4-endpoint={endpoint}",
        "--logging-method=null",
    ]

    typer.echo(f"IP={ip_address} PORT={port} COMMUNITY={community_name}")

    # Exit with the same status code as the simulator process.
    raise typer.Exit(
        subprocess.call(
            command,
            cwd=project_dir,
        )
    )


if __name__ == "__main__":
    typer.run(main)