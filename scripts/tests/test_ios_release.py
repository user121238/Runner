import importlib.util
import json
import os
from pathlib import Path
import plistlib
import stat
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile


HELPER = Path(__file__).resolve().parents[1] / "ios_release.py"
SPEC = importlib.util.spec_from_file_location("ios_release", HELPER)
release = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = release
SPEC.loader.exec_module(release)


def macho(*, signed=False, cpu=0x0100000C):
    command = struct.pack("<4I", 0x1D, 16, 48, 4) if signed else b""
    header = struct.pack("<8I", 0xFEEDFACF, cpu, 0, 2, int(signed), len(command), 0, 0)
    return header + command + (b"SIGN" if signed else b"")


class VersionTests(unittest.TestCase):
    def test_tag_produces_numeric_versions_and_a_stable_asset_name(self):
        version = release.release_version("v1.2.3", "42", is_tag=True)
        self.assertEqual("1.2.3", version.display_version)
        self.assertEqual("42", version.build_number)
        self.assertEqual("v1.2.3", version.tag)
        self.assertEqual("Runner-1.2.3-ios-arm64-unsigned.ipa", version.asset_name)
        self.assertEqual("false", version.metadata()["prerelease"])

    def test_numeric_tags_and_semver_prereleases_preserve_the_original_tag(self):
        for tag, core, package in (("0.0.1-alpha", "0.0.1", "0.0.1-alpha"),
                                   ("0.0.1-alpha.1", "0.0.1", "0.0.1-alpha.1"),
                                   ("v1.2.3-rc.1", "1.2.3", "1.2.3-rc.1")):
            with self.subTest(tag=tag):
                version = release.release_version(tag, "42", is_tag=True)
                self.assertEqual(tag, version.tag)
                self.assertEqual(core, version.display_version)
                self.assertEqual(package, version.package_version)
                self.assertEqual("true", version.metadata()["prerelease"])
                self.assertEqual(f"Runner-{package}-ios-arm64-unsigned.ipa", version.asset_name)
        stable = release.release_version("1.2.3", "42", is_tag=True)
        self.assertEqual("1.2.3", stable.tag)
        self.assertEqual("false", stable.metadata()["prerelease"])

    def test_manual_version_does_not_produce_a_release_tag(self):
        self.assertEqual("", release.release_version("0.0.0", "9999").tag)
        self.assertEqual("", release.release_version("0.0.1-alpha", "42").tag)

    def test_bad_versions_are_rejected_instead_of_being_normalized(self):
        for version in ("1.2", "01.2.3", "1.02.3", "1.2.03", "1.2.3+build", "1.2.3\n",
                        "-1.2.3", "1.2.3;touch file", "$(id)", "1.2.10000", "", " 1.2.3",
                        "0.0.1-", "0.0.1-alpha..1", "0.0.1-alpha.01", "0.0.1-01", "0.0.1-alpha_1",
                        "0.0.1-alpha+build", "0.0.1-alpha\n", "v1.2.3"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                release.release_version(version, "1")

    def test_tag_prefix_and_build_boundaries_are_enforced(self):
        for tag in ("V1.2.3", "release-1.2.3", "vv1.2.3", "v0.0.1-alpha.01"):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                release.release_version(tag, "1", is_tag=True)
        for build in ("0", "01", "10000", "-1", "1.2", "1\n", "", "1;id"):
            with self.subTest(build=build), self.assertRaises(ValueError):
                release.release_version("1.2.3", build)

    def test_cli_github_outputs_are_literal_validated_values(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "github-output"
            result = subprocess.run([sys.executable, str(HELPER), "metadata", "--tag", "v1.2.3", "--build-number", "42",
                                     "--github-output", str(output)], capture_output=True, text=True, check=True)
            self.assertEqual("1.2.3", json.loads(result.stdout)["display_version"])
            values = dict(line.split("=", 1) for line in output.read_text(encoding="utf-8").splitlines())
            self.assertEqual("v1.2.3", values["tag"])
            self.assertEqual("42", values["build_number"])
            self.assertEqual(release.TOOLCHAIN["sdk_version"], values["sdk_version"])

    def test_invalid_cli_input_leaves_github_outputs_untouched(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "github-output"
            result = subprocess.run([sys.executable, str(HELPER), "metadata", "--tag", "v1.2.3\ninjected=value",
                                     "--build-number", "42", "--github-output", str(output)], capture_output=True, text=True)
            self.assertEqual(1, result.returncode)
            self.assertFalse(output.exists())


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.app = self.root / "Runner.app"
        self.app.mkdir()
        self.version = release.release_version("1.2.3", "42")
        self.ipa = self.root / self.version.asset_name
        self.info = {"CFBundleIdentifier": "com.eric.runner", "CFBundleExecutable": "Runner.Client",
                     "CFBundleShortVersionString": "1.2.3", "CFBundleVersion": "42", "CFBundlePackageType": "APPL",
                     "CFBundleSupportedPlatforms": ["iPhoneOS"]}
        self.write_plist()
        self.executable = self.app / "Runner.Client"
        self.executable.write_bytes(macho())
        self.executable.chmod(0o755)
        (self.app / "Resources").mkdir()
        (self.app / "Resources" / "tick.wav").write_bytes(b"sample audio resource")

    def write_plist(self):
        (self.app / "Info.plist").write_bytes(plistlib.dumps(self.info, fmt=plistlib.FMT_BINARY))

    def test_real_payload_structure_resources_and_executable_permissions(self):
        release.package_ipa(self.app, self.ipa, self.version)
        release.validate_ipa(self.ipa, self.version)
        with zipfile.ZipFile(self.ipa) as archive:
            self.assertIn("Payload/Runner.app/Info.plist", archive.namelist())
            self.assertEqual(b"sample audio resource", archive.read("Payload/Runner.app/Resources/tick.wav"))
            self.assertEqual(macho(), archive.read("Payload/Runner.app/Runner.Client"))
            mode = archive.getinfo("Payload/Runner.app/Runner.Client").external_attr >> 16
            self.assertTrue(stat.S_ISREG(mode))
            self.assertEqual(0o111, mode & 0o111)
            self.assertFalse(any(name.startswith("Runner.app/") for name in archive.namelist()))

    def test_mismatched_versions_or_simulator_bundle_fail_before_writing(self):
        for key, wrong in (("CFBundleVersion", "41"), ("CFBundleShortVersionString", "1.2.2"),
                           ("CFBundleIdentifier", "com.other.app"), ("CFBundlePackageType", "FMWK"),
                           ("CFBundleSupportedPlatforms", ["iPhoneSimulator"])):
            with self.subTest(key=key):
                original = self.info[key]
                self.info[key] = wrong
                self.write_plist()
                with self.assertRaises(ValueError):
                    release.package_ipa(self.app, self.ipa, self.version)
                self.assertFalse(self.ipa.exists())
                self.info[key] = original

    def test_missing_or_non_device_executable_is_rejected(self):
        for binary in (b"PK\x03\x04renamed-zip", macho(cpu=0x01000007), b""):
            with self.subTest(binary=binary):
                self.executable.write_bytes(binary)
                with self.assertRaises(ValueError):
                    release.package_ipa(self.app, self.ipa, self.version)
        self.executable.unlink()
        with self.assertRaises(ValueError):
            release.package_ipa(self.app, self.ipa, self.version)

    def test_executable_path_traversal_is_rejected(self):
        for executable in ("../outside", "folder/app", "folder\\app", "", ".."):
            with self.subTest(executable=executable), self.assertRaises(ValueError):
                release.validate_plist({**self.info, "CFBundleExecutable": executable}, self.version)

    def test_signature_envelope_and_provisioning_profile_are_rejected(self):
        for name in ("_CodeSignature", "embedded.mobileprovision"):
            with self.subTest(name=name):
                entry = self.app / name
                entry.write_text("signing material", encoding="utf-8")
                with self.assertRaises(ValueError):
                    release.package_ipa(self.app, self.ipa, self.version)
                entry.unlink()

    def test_embedded_signature_is_rejected_without_a_codesign_envelope(self):
        self.executable.write_bytes(macho(signed=True))
        with self.assertRaisesRegex(ValueError, "Embedded Mach-O signature"):
            release.package_ipa(self.app, self.ipa, self.version)

    def test_nested_framework_signature_is_also_rejected(self):
        framework = self.app / "Frameworks" / "Example.framework"
        framework.mkdir(parents=True)
        (framework / "Example").write_bytes(macho(signed=True))
        with self.assertRaisesRegex(ValueError, "Embedded Mach-O signature"):
            release.package_ipa(self.app, self.ipa, self.version)

    def test_output_inside_app_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "outside the .app"):
            release.package_ipa(self.app, self.app / "accidental.ipa", self.version)

    def test_cli_does_not_write_an_ipa_inside_its_original_source_bundle(self):
        output = self.app / "accidental.ipa"
        result = subprocess.run([sys.executable, str(HELPER), "package", "--app", str(self.app), "--output", str(output),
                                 "--version", "1.2.3", "--build-number", "42"], capture_output=True, text=True)
        self.assertEqual(1, result.returncode)
        self.assertIn("outside the .app", result.stderr)
        self.assertFalse(output.exists())

    def test_relative_symlink_is_stored_as_a_link_not_copied_as_a_resource(self):
        # Model a Unix symlink without requiring Windows Developer Mode or administrator privileges.
        link = self.app / "tick-link"
        link.write_text("placeholder", encoding="utf-8")
        original_lstat = Path.lstat
        original_readlink = os.readlink
        link_stat = list(link.lstat())
        link_stat[0] = stat.S_IFLNK | 0o777
        with patch.object(Path, "is_symlink", lambda path: path == link), \
                patch.object(Path, "lstat", lambda path: os.stat_result(link_stat) if path == link else original_lstat(path)), \
                patch.object(release.os, "readlink", side_effect=lambda path, *args, **kwargs: "Resources/tick.wav" if Path(path) == link else original_readlink(path, *args, **kwargs)):
            release.package_ipa(self.app, self.ipa, self.version)
        with zipfile.ZipFile(self.ipa) as archive:
            entry = archive.getinfo("Payload/Runner.app/tick-link")
            self.assertTrue(stat.S_ISLNK(entry.external_attr >> 16))
            self.assertEqual(b"Resources/tick.wav", archive.read(entry))

    def test_symlink_outside_the_app_is_rejected(self):
        link = self.app / "outside-link"
        link.write_text("placeholder", encoding="utf-8")
        original_readlink = os.readlink
        with patch.object(Path, "is_symlink", lambda path: path == link), \
                patch.object(release.os, "readlink", side_effect=lambda path, *args, **kwargs: "../private-file" if Path(path) == link else original_readlink(path, *args, **kwargs)):
            with self.assertRaisesRegex(ValueError, "outside the app"):
                release.package_ipa(self.app, self.ipa, self.version)

    def test_failed_validation_does_not_replace_an_existing_ipa(self):
        self.ipa.write_bytes(b"previous artifact")
        self.info["CFBundleVersion"] = "41"
        self.write_plist()
        with self.assertRaises(ValueError):
            release.package_ipa(self.app, self.ipa, self.version)
        self.assertEqual(b"previous artifact", self.ipa.read_bytes())

    def test_repeat_packaging_replaces_the_same_asset_without_adding_an_outer_directory(self):
        release.package_ipa(self.app, self.ipa, self.version)
        (self.app / "Resources" / "tick.wav").write_bytes(b"new resource")
        release.package_ipa(self.app, self.ipa, self.version)
        with zipfile.ZipFile(self.ipa) as archive:
            self.assertEqual(b"new resource", archive.read("Payload/Runner.app/Resources/tick.wav"))
        self.assertEqual([self.ipa], list(self.root.glob("*.ipa")))

    def test_cli_packages_fixture_and_writes_matching_checksum_without_modifying_source(self):
        result = subprocess.run([sys.executable, str(HELPER), "package", "--app", str(self.app), "--output", str(self.ipa),
                                 "--version", "1.2.3", "--build-number", "42"], capture_output=True, text=True, check=True)
        self.assertIn("Validated unsigned IPA:", result.stdout)
        digest, name = self.ipa.with_suffix(".ipa.sha256").read_text().strip().split("  ")
        self.assertEqual(release.hashlib.sha256(self.ipa.read_bytes()).hexdigest(), digest)
        self.assertEqual(self.ipa.name, name)
        self.assertEqual(macho(), self.executable.read_bytes())

    def test_alpha_tag_metadata_flows_through_packaging_without_losing_the_suffix(self):
        output = self.root / "github-output"
        result = subprocess.run([sys.executable, str(HELPER), "metadata", "--tag", "0.0.1-alpha", "--build-number", "42",
                                 "--github-output", str(output)], capture_output=True, text=True, check=True)
        metadata = json.loads(result.stdout)
        values = dict(line.split("=", 1) for line in output.read_text(encoding="utf-8").splitlines())
        self.assertEqual("0.0.1-alpha", values["tag"])
        self.assertEqual("0.0.1-alpha", values["package_version"])
        self.assertEqual("0.0.1", values["display_version"])
        self.assertEqual("true", values["prerelease"])
        self.assertEqual("Runner-0.0.1-alpha-ios-arm64-unsigned.ipa", metadata["asset_name"])
        self.info["CFBundleShortVersionString"] = metadata["display_version"]
        self.write_plist()
        ipa = self.root / metadata["asset_name"]
        subprocess.run([sys.executable, str(HELPER), "package", "--app", str(self.app), "--output", str(ipa),
                        "--version", metadata["package_version"], "--build-number", metadata["build_number"]],
                       capture_output=True, text=True, check=True)
        release.validate_ipa(ipa, release.release_version("0.0.1-alpha", "42", is_tag=True))
        with zipfile.ZipFile(ipa) as archive:
            info = plistlib.loads(archive.read("Payload/Runner.app/Info.plist"))
            self.assertEqual("0.0.1", info["CFBundleShortVersionString"])
            self.assertEqual("42", info["CFBundleVersion"])
        digest, name = ipa.with_suffix(".ipa.sha256").read_text().strip().split("  ")
        self.assertEqual(release.hashlib.sha256(ipa.read_bytes()).hexdigest(), digest)
        self.assertEqual("Runner-0.0.1-alpha-ios-arm64-unsigned.ipa", name)

    def test_zip_with_wrong_top_level_structure_is_rejected(self):
        with zipfile.ZipFile(self.ipa, "w") as archive:
            archive.writestr("Runner.app/Info.plist", plistlib.dumps(self.info))
        with self.assertRaisesRegex(ValueError, "Payload"):
            release.validate_ipa(self.ipa, self.version)

    def test_signature_cleanup_checks_codesign_result_instead_of_swallowing_failures(self):
        unsigned = subprocess.CompletedProcess([], 1, "", "code object is not signed at all")
        signed = subprocess.CompletedProcess([], 0, "", "signed object")
        with patch.object(release.sys, "platform", "darwin"), patch.object(release.subprocess, "run", side_effect=[signed, unsigned, unsigned]) as run:
            release.strip_signatures(self.app)
        self.assertEqual("--remove-signature", run.call_args_list[1].args[0][1])
        invalid = subprocess.CompletedProcess([], 1, "", "invalid object format")
        with patch.object(release.sys, "platform", "darwin"), patch.object(release.subprocess, "run", return_value=invalid):
            with self.assertRaisesRegex(ValueError, "Could not verify unsigned"):
                release.strip_signatures(self.app)


class MachOTests(unittest.TestCase):
    def test_unsigned_and_signed_load_commands(self):
        self.assertFalse(release.has_macho_signature(macho()))
        self.assertTrue(release.has_macho_signature(macho(signed=True)))
        self.assertFalse(release.has_macho_signature(b"not a binary"))

    def test_universal_binary_checks_the_inner_slice(self):
        for signed in (False, True):
            with self.subTest(signed=signed):
                inner = macho(signed=signed)
                fat = struct.pack(">7I", 0xCAFEBABE, 1, 0x0100000C, 0, 28, len(inner), 0) + inner
                self.assertEqual(signed, release.has_macho_signature(fat))

    def test_invalid_load_command_size_is_rejected(self):
        data = struct.pack("<8I", 0xFEEDFACF, 0x0100000C, 0, 2, 1, 8, 0, 0) + struct.pack("<2I", 1, 0)
        with self.assertRaisesRegex(ValueError, "load command"):
            release.has_macho_signature(data)


if __name__ == "__main__":
    unittest.main()
