#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "${script_dir}/.." && pwd)"
helper="${script_dir}/ios_release.py"
version="${1:?Usage: build-unsigned-ipa.sh VERSION BUILD_NUMBER [OUTPUT_DIRECTORY]}"
build_number="${2:?A positive build number is required}"
output="${3:-${root}/artifacts/ios}"

# Reject invalid input before invoking any expensive build or touching its output.
python3 "$helper" metadata --version "$version" --build-number "$build_number" >/dev/null
display_version="${version%%-*}"
metadata_source=(--version "$version")
if [[ -n "${RELEASE_TAG:-}" ]]; then
    if [[ "${RELEASE_TAG#v}" != "$version" ]]; then
        printf '%s\n' 'RELEASE_TAG and the package version must match.' >&2
        exit 1
    fi
    metadata_source=(--tag "$RELEASE_TAG")
fi
if [[ "$(uname -s)" != Darwin ]]; then
    printf '%s\n' 'An iPhoneOS build requires macOS and Xcode.' >&2
    exit 1
fi
expected_sdk="$(python3 "$helper" toolchain sdk_version)"
expected_xcode="$(python3 "$helper" toolchain xcode_version)"
expected_ios_sdk="$(python3 "$helper" toolchain ios_sdk_version)"
if [[ "$(dotnet --version)" != "$expected_sdk" ]]; then
    printf 'Select .NET SDK %s before running this script.\n' "$expected_sdk" >&2
    exit 1
fi
if [[ "$(xcodebuild -version | sed -n '1p')" != "Xcode ${expected_xcode}" ]] ||
   [[ "$(xcrun --sdk iphoneos --show-sdk-version)" != "$expected_ios_sdk" ]]; then
    printf 'Select Xcode %s with the iPhoneOS %s SDK.\n' "$expected_xcode" "$expected_ios_sdk" >&2
    exit 1
fi

mkdir -p "${output}/logs"
output="$(cd "$output" && pwd)"
# Each invocation has fresh outputs; no old .app can accidentally become this release.
work="$(mktemp -d "${output}/build.XXXXXX")"
app="${work}/Runner.Client.app"
python3 "$helper" metadata "${metadata_source[@]}" --build-number "$build_number" >"${output}/metadata.json"
dotnet --info >"${output}/logs/dotnet-info.txt"
dotnet workload list >"${output}/logs/workloads.txt"
xcodebuild -version >"${output}/logs/xcode.txt"

dotnet publish "${root}/src/Runner.Client/Runner.Client.csproj" \
    --configuration Release \
    --framework net10.0-ios \
    --runtime ios-arm64 \
    --artifacts-path "${work}/artifacts" \
    -p:RunnerTargetFrameworks=net10.0-ios \
    -p:ApplicationDisplayVersion="$display_version" \
    -p:ApplicationVersion="$build_number" \
    -p:AppBundleDir="$app" \
    -p:EnableCodeSigning=false \
    -p:CodesignRequireProvisioningProfile=false \
    -p:ArchiveOnBuild=false \
    -p:BuildIpa=false \
    "-bl:${output}/logs/publish-{}.binlog" \
    2>&1 | tee "${output}/logs/publish.log"

python3 "$helper" package \
    --app "$app" \
    --output "${output}/Runner-${version}-ios-arm64-unsigned.ipa" \
    --version "$version" \
    --build-number "$build_number" \
    --strip-signatures
