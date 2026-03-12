from __future__ import annotations

import asyncio
import sys

from pysnmp.hlapi.v1arch.asyncio import CommunityData
from pysnmp.hlapi.v1arch.asyncio import ObjectIdentity
from pysnmp.hlapi.v1arch.asyncio import ObjectType
from pysnmp.hlapi.v1arch.asyncio import SnmpDispatcher
from pysnmp.hlapi.v1arch.asyncio import UdpTransportTarget
from pysnmp.hlapi.v1arch.asyncio import next_cmd
from pysnmp.proto.rfc1905 import EndOfMibView
from pysnmp.proto.rfc1902 import ObjectName
import typer


async def walk_host(ip: str, community: str, port: int, oid: str, version: str) -> int:
    version_map = {"1": 0, "2c": 1}
    if version not in version_map:
        print(f"Error: unsupported SNMP version '{version}'. Use '1' or '2c'.", file=sys.stderr)
        return 1

    start_oid = ObjectName(oid)
    current_oid = start_oid
    dispatcher = SnmpDispatcher()
    transport_target = await UdpTransportTarget.create((ip, port))

    print("=========================")
    print(f"SNMP walk on {ip}:{port}")
    print(f"Community: {community}")
    print(f"OID: {oid}")

    try:
        while True:
            error_indication, error_status, error_index, var_binds = await next_cmd(
                dispatcher,
                CommunityData(community, mpModel=version_map[version]),
                transport_target,
                ObjectType(ObjectIdentity(str(current_oid))),
            )

            if error_indication:
                print(f"Error: {error_indication}", file=sys.stderr)
                return 1

            if error_status:
                print(
                    f"Error: {error_status.prettyPrint()} at index {error_index}",
                    file=sys.stderr,
                )
                return 1

            if not var_binds:
                break

            next_var_bind = var_binds[0]
            next_oid, value = next_var_bind
            next_oid = ObjectName(next_oid)

            if isinstance(value, EndOfMibView):
                break

            if tuple(next_oid)[: len(start_oid)] != tuple(start_oid):
                break

            print(f"{next_oid.prettyPrint()} = {value.prettyPrint()}")
            current_oid = next_oid

    finally:
        dispatcher.close()

    print("=========================")
    print()
    return 0


async def run_walks(ips: list[str], port: int, oid: str, version: str) -> int:
    exit_code = 0
    for index, ip in enumerate(ips):
        community = f"device_{index + 1}"
        host_code = await walk_host(ip, community, port, oid, version)
        if host_code != 0:
            exit_code = host_code
    return exit_code


def main(
    device_count: int = typer.Option(3, "--device-count", min=1, help="Number of devices to query."),
    ip_address: str = typer.Option("127.0.0.1", "--ip-address", help="Base IP address for the first device."),
    port: int = typer.Option(161, "--port", min=1, max=65535, help="SNMP UDP port."),
    oid: str = typer.Option("1.3.6.1.4.1", "--oid", help="OID to walk."),
    version: str = typer.Option("2c", "--version", help="SNMP version for snmpwalk."),
) -> None:
    ip_parts = ip_address.split(".")
    if len(ip_parts) != 4:
        raise typer.BadParameter(f"invalid IPv4 address '{ip_address}'", param_hint="--ip-address")

    try:
        octets = [int(part) for part in ip_parts]
    except ValueError:
        raise typer.BadParameter(f"invalid IPv4 address '{ip_address}'", param_hint="--ip-address")

    if any(octet < 0 or octet > 255 for octet in octets):
        raise typer.BadParameter(f"invalid IPv4 address '{ip_address}'", param_hint="--ip-address")

    last_octet = octets[3]
    if last_octet + device_count - 1 > 255:
        raise typer.BadParameter(
            f"IP range overflow from '{ip_address}' with device count {device_count}.",
            param_hint="--device-count",
        )

    ips = [
        f"{octets[0]}.{octets[1]}.{octets[2]}.{last_octet + index}"
        for index in range(device_count)
    ]

    raise typer.Exit(asyncio.run(run_walks(ips, port, oid, version)))


if __name__ == "__main__":
    typer.run(main)
