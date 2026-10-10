#!/usr/bin/env python3
"""Validate release versions and package an unsigned iPhoneOS .app, using only stdlib."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import zipfile
from dataclasses import dataclass


# Verified against the NuGet workload set/manifest and actions/runner-images; see IOS_RELEASE.md.
TOOLCHAIN = {
    "sdk_version": "10.0.103",
    "workload_version": "10.0.103",
    "xcode_version": "26.2",
    "ios_sdk_version": "26.2",
}
VERSION_PATTERN = re.compile(
    r"(?P<core>(?:0|[1-9][0-9]{0,3})(?:\.(?:0|[1-9][0-9]{0,3})){2})"
    r"(?:-(?P<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\Z"
)
BUILD_PATTERN = re.compile(r"[1-9][0-9]{0,3}\Z")
MACHO_MAGICS = {
    b"\xce\xfa\xed\xfe", b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xce", b"\xfe\xed\xfa\xcf",
    b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca", b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca",
}


@dataclass(frozen=True)
class ReleaseVersion:
    display_version: str
    build_number: str
    tag: str = ""
    prerelease: str = ""

    @property
    def package_version(self) -> str:
        return self.display_version + (f"-{self.prerelease}" if self.prerelease else "")

    @property
    def asset_name(self) -> str:
        return f"Runner-{self.package_version}-ios-arm64-unsigned.ipa"

    def metadata(self) -> dict[str, str]:
        return {**TOOLCHAIN, "display_version": self.display_version, "build_number": self.build_number,
                "tag": self.tag, "asset_name": self.asset_name, "package_version": self.package_version,
                "prerelease": "true" if self.prerelease else "false"}


def release_version(version: str, build_number: str, *, is_tag: bool = False) -> ReleaseVersion:
    tag = version if is_tag else ""
    if is_tag and version.startswith("v"):
        version = version[1:]
    match = VERSION_PATTERN.fullmatch(version)
    if not match:
        raise ValueError("Version must be MAJOR.MINOR.PATCH (0..9999 each), optionally followed by a SemVer prerelease")
    prerelease = match.group("prerelease") or ""
    if any(part.isdigit() and len(part) > 1 and part.startswith("0") for part in prerelease.split(".")):
        raise ValueError("Numeric prerelease identifiers must not have leading zeroes")
    # A single-component CFBundleVersion has at most four digits in Apple's documented format.
    if not BUILD_PATTERN.fullmatch(build_number):
        raise ValueError("Build number must be a decimal integer from 1 to 9999")
    return ReleaseVersion(match.group("core"), build_number, tag, prerelease)


def verify_runtime_properties(document: dict) -> None:
    """Check evaluated SDK properties, not just XML text or Debug defaults."""
    properties = document.get("Properties") if isinstance(document, dict) else None
    if not isinstance(properties, dict):
        raise ValueError("Expected the JSON Properties object from dotnet msbuild -getProperty")
    expected = {
        "Configuration": "Release",
        "TargetFramework": "net10.0-ios",
        "RuntimeIdentifier": "ios-arm64",
        "UseMonoRuntime": "true",
        "MtouchInterpreter": "all",
    }
    for key, value in expected.items():
        if str(properties.get(key, "")).lower() != value.lower():
            raise ValueError(f"iOS runtime {key}: expected {value!r}, got {properties.get(key)!r}")
    if str(properties.get("PublishAot", "")).lower() == "true":
        raise ValueError("This LiteDB build requires the Mono interpreter, not NativeAOT PublishAot")
    if str(properties.get("DynamicCodeSupport", "")).lower() == "false":
        raise ValueError("DynamicCodeSupport=false disables the dynamic expression/thunk path required by LiteDB")


def validate_plist(info: dict, version: ReleaseVersion) -> str:
    expected = {"CFBundleShortVersionString": version.display_version, "CFBundleVersion": version.build_number,
                "CFBundleIdentifier": "com.eric.runner", "CFBundlePackageType": "APPL"}
    for key, value in expected.items():
        if info.get(key) != value:
            raise ValueError(f"{key}: expected {value!r}, got {info.get(key)!r}")
    if info.get("CFBundleSupportedPlatforms") != ["iPhoneOS"]:
        raise ValueError("The bundle must target iPhoneOS, not an iOS simulator")
    executable = info.get("CFBundleExecutable", "")
    if not isinstance(executable, str) or not executable or executable in (".", "..") or "/" in executable or "\\" in executable:
        raise ValueError("CFBundleExecutable must name a file directly inside the .app")
    return executable


def has_macho_signature(data: bytes | memoryview) -> bool:
    """Inspect LC_CODE_SIGNATURE, including every architecture of a universal Mach-O."""
    magic = bytes(data[:4])
    if magic not in MACHO_MAGICS:
        return False
    if magic in (b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca", b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca"):
        endian = ">" if magic[0] == 0xCA else "<"
        wide = magic in (b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca")
        count = struct.unpack_from(endian + "I", data, 4)[0]
        entry_size = 32 if wide else 20
        if 8 + count * entry_size > len(data):
            raise ValueError("Truncated universal Mach-O header")
        for index in range(count):
            offset, size = struct.unpack_from(endian + ("QQ" if wide else "II"), data, 16 + index * entry_size)
            if size < 28 or offset + size > len(data):
                raise ValueError("Invalid universal Mach-O slice")
            if has_macho_signature(memoryview(data)[offset:offset + size]):
                return True
        return False
    endian = "<" if magic[0] in (0xCE, 0xCF) else ">"
    header_size = 32 if magic in (b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xcf") else 28
    count, size = struct.unpack_from(endian + "II", data, 16)
    cursor, limit = header_size, header_size + size
    if limit > len(data) or count > size // 8:
        raise ValueError("Invalid Mach-O load-command header")
    for _ in range(count):
        command, command_size = struct.unpack_from(endian + "II", data, cursor)
        if command_size < 8 or cursor + command_size > limit:
            raise ValueError("Invalid Mach-O load command")
        if command == 0x1D:
            if command_size < 16:
                raise ValueError("Truncated Mach-O signature command")
            if struct.unpack_from(endian + "I", data, cursor + 12)[0] != 0:
                return True
        cursor += command_size
    return False


def validate_bundle(app: Path, version: ReleaseVersion) -> Path:
    if app.suffix != ".app" or not app.is_dir() or app.is_symlink():
        raise ValueError("Expected a real .app directory")
    with (app / "Info.plist").open("rb") as stream:
        executable = app / validate_plist(plistlib.load(stream), version)
    if not executable.is_file() or executable.is_symlink():
        raise ValueError("The app executable is missing or is a symlink")
    with executable.open("rb") as stream:
        header = stream.read(32)
    # ios-arm64 is a thin, little-endian 64-bit ARM executable (not a simulator or a renamed ZIP).
    if len(header) != 32 or struct.unpack_from("<I", header)[0] != 0xFEEDFACF or struct.unpack_from("<I", header, 4)[0] != 0x0100000C or struct.unpack_from("<I", header, 12)[0] != 2:
        raise ValueError("Expected a 64-bit arm64 Mach-O executable")
    if os.name != "nt" and not os.access(executable, os.X_OK):
        raise ValueError("The app executable has no executable permission")
    for entry in app.rglob("*"):
        if entry.name in ("_CodeSignature", "embedded.mobileprovision"):
            raise ValueError(f"Signing material remains in the app: {entry}")
        if entry.is_symlink():
            target = os.readlink(entry)
            if os.path.isabs(target) or not (entry.parent / target).resolve().is_relative_to(app.resolve()):
                raise ValueError(f"Bundle symlink points outside the app: {entry}")
        elif entry.is_file():
            with entry.open("rb") as stream:
                magic = stream.read(4)
                if magic in MACHO_MAGICS and has_macho_signature(magic + stream.read()):
                    raise ValueError(f"Embedded Mach-O signature remains in the app: {entry}")
    return executable


def strip_signatures(app: Path) -> None:
    """Only call this on the temporary packaging copy, never on the build output."""
    if sys.platform != "darwin":
        raise ValueError("Removing/verifying Apple signatures requires macOS codesign")
    env = {**os.environ, "LC_ALL": "C", "LANG": "C"}
    for entry in app.rglob("*"):
        if entry.is_symlink() or not entry.is_file():
            continue
        with entry.open("rb") as stream:
            magic = stream.read(4)
        if magic not in MACHO_MAGICS:
            continue
        result = subprocess.run(["codesign", "--display", str(entry)], capture_output=True, text=True, env=env, check=False)
        if result.returncode == 0:
            subprocess.run(["codesign", "--remove-signature", str(entry)], check=True, env=env)
            result = subprocess.run(["codesign", "--display", str(entry)], capture_output=True, text=True, env=env, check=False)
        if result.returncode == 0 or "code object is not signed at all" not in result.stderr:
            raise ValueError(f"Could not verify unsigned Mach-O {entry}: {result.stderr}")
    # Remove stale bundle signature envelopes and profiles after checking every Mach-O.
    for entry in sorted(app.rglob("*"), key=lambda path: len(path.parts), reverse=True):
        if entry.name not in ("_CodeSignature", "embedded.mobileprovision") or not entry.exists():
            continue
        if entry.is_dir() and not entry.is_symlink():
            shutil.rmtree(entry)
        else:
            entry.unlink()


def validate_ipa(ipa: Path, version: ReleaseVersion) -> None:
    with zipfile.ZipFile(ipa) as archive:
        if archive.testzip() is not None:
            raise ValueError("IPA failed the ZIP integrity check")
        names = archive.namelist()
        if any(not name.startswith("Payload/") or ".." in Path(name).parts for name in names):
            raise ValueError("IPA entries must be contained in Payload/")
        apps = {name.split("/")[1] for name in names if len(name.split("/")) > 2}
        if len(apps) != 1 or not next(iter(apps)).endswith(".app"):
            raise ValueError("IPA must contain exactly one Payload/*.app")
        prefix = f"Payload/{next(iter(apps))}/"
        info = plistlib.loads(archive.read(prefix + "Info.plist"))
        executable = validate_plist(info, version)
        mode = archive.getinfo(prefix + executable).external_attr >> 16
        if not stat.S_ISREG(mode) or not mode & 0o111:
            raise ValueError("IPA lost the main executable's Unix permissions")
        if any(part in ("_CodeSignature", "embedded.mobileprovision") for name in names for part in name.split("/")):
            raise ValueError("IPA contains signing material")


def package_ipa(app: Path, ipa: Path, version: ReleaseVersion) -> None:
    executable = validate_bundle(app, version)
    if ipa.resolve().is_relative_to(app.resolve()):
        raise ValueError("The output IPA must be outside the .app")
    ipa.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=ipa.parent, suffix=".ipa", delete=False) as stream:
        temporary = Path(stream.name)
    try:
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            directory = zipfile.ZipInfo("Payload/")
            directory.create_system = 3
            directory.external_attr = (stat.S_IFDIR | 0o755) << 16
            archive.writestr(directory, b"")
            for entry in [app, *sorted(app.rglob("*"))]:
                name = (Path("Payload") / app.name / entry.relative_to(app)).as_posix()
                mode = entry.lstat().st_mode
                if entry == executable:
                    mode |= 0o111
                if entry.is_dir() and not entry.is_symlink():
                    name += "/"
                item = zipfile.ZipInfo(name)
                item.create_system = 3
                item.external_attr = mode << 16
                item.compress_type = zipfile.ZIP_DEFLATED
                if entry.is_symlink():
                    archive.writestr(item, os.readlink(entry).encode("utf-8"))
                elif entry.is_file():
                    with entry.open("rb") as source, archive.open(item, "w") as destination:
                        shutil.copyfileobj(source, destination)
                else:
                    archive.writestr(item, b"")
        validate_ipa(temporary, version)
        os.replace(temporary, ipa)
    finally:
        temporary.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    metadata = commands.add_parser("metadata")
    source = metadata.add_mutually_exclusive_group(required=True)
    source.add_argument("--tag")
    source.add_argument("--version")
    metadata.add_argument("--build-number", required=True)
    metadata.add_argument("--github-output", type=Path)
    toolchain = commands.add_parser("toolchain")
    toolchain.add_argument("key", choices=TOOLCHAIN)
    runtime = commands.add_parser("verify-runtime")
    runtime.add_argument("properties", type=Path)
    package = commands.add_parser("package")
    package.add_argument("--app", type=Path, required=True)
    package.add_argument("--output", type=Path, required=True)
    package.add_argument("--version", required=True)
    package.add_argument("--build-number", required=True)
    package.add_argument("--strip-signatures", action="store_true")
    args = parser.parse_args()
    try:
        if args.command == "toolchain":
            print(TOOLCHAIN[args.key])
            return 0
        if args.command == "verify-runtime":
            verify_runtime_properties(json.loads(args.properties.read_text(encoding="utf-8-sig")))
            print("Verified iOS Release/ios-arm64 runtime: Mono with MtouchInterpreter=all")
            return 0
        version = release_version(args.tag if args.command == "metadata" and args.tag is not None else args.version,
                                  args.build_number, is_tag=args.command == "metadata" and args.tag is not None)
        if args.command == "metadata":
            values = version.metadata()
            if args.github_output:
                with args.github_output.open("a", encoding="utf-8") as stream:
                    for key, value in values.items():
                        stream.write(f"{key}={value}\n")
            print(json.dumps(values, indent=2))
            return 0
        if args.app.suffix != ".app" or not args.app.is_dir() or args.app.is_symlink():
            raise ValueError("Expected a real .app directory")
        if args.output.resolve().is_relative_to(args.app.resolve()):
            raise ValueError("The output IPA must be outside the .app")
        with tempfile.TemporaryDirectory(prefix="runner-unsigned-") as directory:
            app = Path(directory) / args.app.name
            shutil.copytree(args.app, app, symlinks=True)
            if args.strip_signatures:
                strip_signatures(app)
            package_ipa(app, args.output, version)
        digest = hashlib.sha256(args.output.read_bytes()).hexdigest()
        args.output.with_suffix(".ipa.sha256").write_text(f"{digest}  {args.output.name}\n", encoding="utf-8")
        print(f"Validated unsigned IPA: {args.output}\nSHA256: {digest}")
        return 0
    except (ValueError, OSError, KeyError, struct.error, plistlib.InvalidFileException, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
