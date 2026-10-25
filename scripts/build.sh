#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

: "${VALHEIM_MANAGED:?Set VALHEIM_MANAGED to the Valheim server Managed directory, e.g. /path/to/valheim_server_Data/Managed}"
: "${BEPINEX_CORE:?Set BEPINEX_CORE to the BepInEx core directory, e.g. /path/to/BepInExPack_Valheim/BepInEx/core}"

dotnet build "$repo_root/src/OdinChat/OdinChat.csproj" \
  -c Release \
  -p:ValheimManaged="$VALHEIM_MANAGED" \
  -p:BepInExCore="$BEPINEX_CORE"

echo
echo "Built: $repo_root/dist/OdinChat.dll"
echo "Install it in the server's BepInEx/plugins directory."
