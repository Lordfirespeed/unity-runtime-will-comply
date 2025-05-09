#!/usr/bin/env python3.13

from pathlib import Path

other_net_standard_ref_dirpath = Path("~/.nuget/packages/netstandard.library.ref/2.1.0/ref/netstandard2.1").expanduser()
net_standard_ref_dirpath = Path("/usr/lib/dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1")
unity_runtime_dirpath = Path("~/.steam/debian-installation/steamapps/common/REPO/REPO_Data/Managed").expanduser()


def check_nuget_matches_sdk_pack():
    a = set(path.stem for path in other_net_standard_ref_dirpath.glob("*.dll"))
    b = set(path.stem for path in net_standard_ref_dirpath.glob("*.dll"))
    assert a == b


def main():
    net_standard_assembly_names = set(path.stem for path in net_standard_ref_dirpath.glob("*.dll"))
    standard_count = len(net_standard_assembly_names)
    unity_runtime_assembly_names = set(path.stem for path in unity_runtime_dirpath.glob("*.dll"))
    unity_runtime_implemented_standard_assembly_names = net_standard_assembly_names.intersection(unity_runtime_assembly_names)
    unity_implemented_count = len(unity_runtime_implemented_standard_assembly_names)
    unity_runtime_missing_standard_assembly_names = net_standard_assembly_names.difference(unity_runtime_assembly_names)
    unity_missing_count = len(unity_runtime_missing_standard_assembly_names)
    print(f"Unity has implemented {unity_implemented_count}/{standard_count} .NET Standard 2.1 assemblies:")
    for assembly_name in sorted(unity_runtime_implemented_standard_assembly_names):
        print(f"{' ':>4}{assembly_name}")
    print(f"Unity has not implemented {unity_missing_count}/{standard_count} .NET Standard 2.1 assemblies:")
    for assembly_name in sorted(unity_runtime_missing_standard_assembly_names):
        print(f"{' ':>4}{assembly_name}")


if __name__ == "__main__":
    main()
