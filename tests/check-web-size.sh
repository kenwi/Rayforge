#!/usr/bin/env bash
# Check a published web host against the recorded payload baseline.
#
#   ./tests/check-web-size.sh artifacts/hello-world/wwwroot [baseline.env]
#
# Raw bytes exclude the precompressed .br/.gz siblings, so this measures the bytes a host without
# compression would send. Brotli bytes are the .br siblings Blazor publishes; static hosts should
# serve those (or compress on the fly) with Content-Encoding: br.
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
root="${1:-$repo_root/artifacts/hello-world/wwwroot}"
baseline="${2:-$repo_root/tests/web-size-baseline.env}"

if [[ ! -d "$root" ]]; then
    echo "No published site at $root" >&2
    exit 1
fi

# shellcheck disable=SC1090
source "$baseline"

sum_bytes() {
    find "$root" "$@" -type f -printf '%s\n' 2>/dev/null | awk '{ total += $1 } END { print total + 0 }'
}

count_files() {
    find "$root" "$@" -type f -printf '.\n' 2>/dev/null | wc -l | tr -d ' '
}

raw_bytes="$(sum_bytes ! -name '*.br' ! -name '*.gz')"
brotli_bytes="$(sum_bytes -name '*.br')"
file_count="$(count_files ! -name '*.br' ! -name '*.gz')"
native_wasm_bytes="$(find "$root/_framework" -maxdepth 1 -type f -name 'dotnet.native.*.wasm' -printf '%s\n' 2>/dev/null | sort -nr | head -1)"
native_wasm_bytes="${native_wasm_bytes:-0}"

mib() {
    awk -v bytes="$1" 'BEGIN { printf "%.2f MiB", bytes / 1048576 }'
}

report=""
failed=0

check() {
    local label="$1" current="$2" limit="$3" formatter="$4"
    local status="ok"
    if (( current > limit )); then
        status="FAIL"
        failed=1
    fi

    local size
    if [[ "$formatter" == "bytes" ]]; then
        size="$(mib "$current")"
    else
        size="-"
    fi

    printf '%-18s %14s  (%9s)  limit %9s  %s\n' "$label" "$current" "$size" "$limit" "$status"
    report+="| $label | $current | $size | $limit | $status |"$'\n'
}

echo "Web payload for $root"
check "raw bytes" "$raw_bytes" "$MAX_RAW_BYTES" bytes
check "brotli bytes" "$brotli_bytes" "$MAX_BROTLI_BYTES" bytes
check "files" "$file_count" "$MAX_FILES" count
check "dotnet.native" "$native_wasm_bytes" "$MAX_NATIVE_WASM_BYTES" bytes

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    {
        echo "### Published web payload"
        echo
        echo "| Metric | Bytes | Size | Limit | Result |"
        echo "|---|---:|---:|---:|---|"
        printf '%s' "$report"
    } >>"$GITHUB_STEP_SUMMARY"
fi

if (( failed )); then
    echo "Payload regression: update tests/web-size-baseline.env with a reason if the growth is expected." >&2
    exit 1
fi

echo "Web payload within budget."
