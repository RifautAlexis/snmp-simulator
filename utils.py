import json
from pathlib import Path
import re
from sys import modules

import typer

OUTPUT_FILE_NAME = "oid.snmprec"
SYSTEM_OID_FILE_NAME = "system_oid.json"
MODULES_CATALOG_NAME = "modules_catalog.json"

OID_PATTERN = re.compile(r"^(?:0|[1-9]\d*)(?:\.(?:0|[1-9]\d*))*$")
ALLOWED_SNMPREC_TAGS = {
    "2",   # Integer
    "4",   # OctetString
    "5",   # Null
    "6",   # ObjectIdentifier
    "64",  # IpAddress
    "65",  # Counter32
    "66",  # Gauge32/Unsigned32
    "67",  # TimeTicks
    "68",  # Opaque
    "69",  # NsapAddress
    "70",  # Counter64
}

def parse_module_ids(raw_value: str | None) -> list[int]:
    """Parse module IDs from CLI input.

    Accepted forms:
    - `1,2,3`
    - `[1, 2, 3]`
    """
    if raw_value is None:
        return []

    text = raw_value.strip()
    if not text:
        return []

    try:
        if text.startswith("["):
            parsed = json.loads(text)
            if not isinstance(parsed, list):
                raise ValueError("module_ids must be a JSON array")
            values = parsed
        else:
            values = [item.strip() for item in text.split(",") if item.strip()]

        module_ids = [int(value) for value in values]

    except (TypeError, ValueError, json.JSONDecodeError) as exc:
        raise typer.BadParameter(
            "module IDs must be provided as 1,2,3 or [1, 2, 3]"
        ) from exc

    if not module_ids:
        raise typer.BadParameter("At least one module ID must be provided")

    return module_ids

def normalize_record_to_snmprec_line(record: object, context: str) -> str:
    if not isinstance(record, dict):
        raise typer.BadParameter(f"Invalid record in {context}: expected object")

    oid = str(record.get("oid", "")).strip()
    tag = str(record.get("tag", "")).strip()
    value = str(record.get("value", "")).strip()

    if not oid:
        raise typer.BadParameter(f"Invalid record in {context}: oid is required")

    if not OID_PATTERN.fullmatch(oid):
        raise typer.BadParameter(f"Invalid record in {context}: oid '{oid}' is not a valid OID")

    oid_parts = [int(part) for part in oid.split(".")]
    if oid_parts[0] > 2:
        raise typer.BadParameter(
            f"Invalid record in {context}: oid '{oid}' must start with 0, 1, or 2"
        )
    if len(oid_parts) < 2:
        raise typer.BadParameter(
            f"Invalid record in {context}: oid '{oid}' must contain at least two arcs"
        )
    if oid_parts[0] < 2 and oid_parts[1] > 39:
        raise typer.BadParameter(
            f"Invalid record in {context}: oid '{oid}' has invalid second arc for root {oid_parts[0]}"
        )

    if tag not in ALLOWED_SNMPREC_TAGS:
        raise typer.BadParameter(
            f"Invalid record in {context}: tag '{tag}' is not an authorized snmpsim tag"
        )

    if value == "":
        raise typer.BadParameter(f"Invalid record in {context}: value is required")

    return f"{oid}|{tag}|{value}"


def generate_system_oids(system_path: Path) -> tuple[list[str], set[str]]:
    if not system_path.exists():
        raise typer.BadParameter(f"System OID file not found: {system_path}")

    try:
        system_payload = json.loads(system_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise typer.BadParameter(f"Invalid JSON in {system_path}: {exc}") from exc

    # Expected format:
    # {"sysDescr": {...}, "sysObjectID": {...}, ...}
    if not isinstance(system_payload, dict):
        raise typer.BadParameter(
            f"Invalid format in {system_path}: expected top-level object of system records"
        )

    lines: list[str] = []
    seen_oids: set[str] = set()

    for name, record in system_payload.items():
        line = normalize_record_to_snmprec_line(record, f"system.{name}")
        oid, _, _ = line.partition("|")

        if oid in seen_oids:
            raise typer.BadParameter(f"Duplicate OID {oid} found in system records")

        seen_oids.add(oid)
        lines.append(line)

    if not lines:
        raise typer.BadParameter("No system OIDs found in system_oid.json")

    return lines, seen_oids


def generate_module_oids(catalog_path: Path, seen_oids: set[str], module_ids: list[int]) -> list[str]:
    if not catalog_path.exists():
        raise typer.BadParameter(f"Modules catalog not found: {catalog_path}")

    try:
        catalog_payload = json.loads(catalog_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise typer.BadParameter(f"Invalid JSON in {catalog_path}: {exc}") from exc
    
    if not isinstance(catalog_payload, dict):
        raise typer.BadParameter(
            f"Invalid catalog format in {catalog_path}: expected object key 'modules'"
        )

    lines: list[str] = []

    for module_index, module_id in enumerate(module_ids):
        module_key = str(module_id)
        module_data = catalog_payload.get(module_key)

        if not isinstance(module_data, dict):
            raise typer.BadParameter(f"Module ID {module_id} not found in {catalog_path}")

        for section in ("alarms", "metrics"):
            records = module_data.get(section, [])
            if records is None:
                records = []
            if not isinstance(records, list):
                raise typer.BadParameter(
                    f"Invalid section '{section}' for module {module_key}: expected list"
                )

            for index, record in enumerate(records):
                if isinstance(record, dict):
                    record = dict(record)
                    record["oid"] = f"{str(record.get('oid', '')).strip()}.{module_index + 1}"
                else:
                    raise typer.BadParameter(
                        f"Invalid record in module {module_key} section {section}[{index}]: expected object"
                    )
                
                line = normalize_record_to_snmprec_line(record, f"modules.{module_key}.{section}[{index}]")
                oid, _, _ = line.partition("|")

                if oid in seen_oids:
                    raise typer.BadParameter(
                        f"Duplicate OID {oid} found while processing module {module_key}"
                    )

                seen_oids.add(oid)
                lines.append(line)

    if not lines:
        raise typer.BadParameter("No OIDs found for selected modules")

    return lines