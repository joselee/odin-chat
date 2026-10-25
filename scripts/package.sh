#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo_root/src/OdinChat/OdinChat.csproj" | head -n1)"
dll="$repo_root/dist/OdinChat.dll"

if [[ ! -f "$dll" ]]; then
  echo "Build first: ./scripts/build.sh" >&2
  exit 1
fi

stage="$repo_root/release/OdinChat-$version"
out="$repo_root/release/OdinChat-$version.zip"
rm -rf "$stage"
mkdir -p "$stage"

cp "$dll" "$stage/OdinChat.dll"
cp "$repo_root/config/OdinChat.cfg.example" "$stage/OdinChat.cfg.example"
cp "$repo_root/README.md" "$stage/README.md"
cp "$repo_root/TESTING.md" "$stage/TESTING.md"
cp "$repo_root/LICENSE" "$stage/LICENSE"
cp "$repo_root/THIRD_PARTY_NOTICES.md" "$stage/THIRD_PARTY_NOTICES.md"

rm -f "$out"
python3 - "$stage" "$out" <<'PY'
import os
import sys
import zipfile

stage, out = sys.argv[1], sys.argv[2]
base = os.path.dirname(stage)
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as archive:
    for root, _, files in os.walk(stage):
        for name in files:
            path = os.path.join(root, name)
            archive.write(path, os.path.relpath(path, base))
PY

echo "Created: $out"
