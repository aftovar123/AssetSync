#!/usr/bin/env bash
# Previews (default) or applies the AssetSync infrastructure.
#
#   infra/deploy.sh           # what-if only: shows what would change, touches nothing
#   infra/deploy.sh --apply   # previews, then deploys after confirmation
#
# Secrets are never stored in the repo. Each one is taken from its
# environment variable when set (e.g. to rotate it or to build a new
# environment); otherwise the value currently configured on the web app is
# reused, so a redeploy keeps them as they are. Requires `az login`.
set -euo pipefail

RESOURCE_GROUP="assetsync-rg"
WEB_APP="assetsync-api-andres"
HERE="$(cd "$(dirname "$0")" && pwd)"

current_settings="$(az webapp config appsettings list -g "$RESOURCE_GROUP" -n "$WEB_APP" -o json)"
current_connection_strings="$(az webapp config connection-string list -g "$RESOURCE_GROUP" -n "$WEB_APP" -o json)"

# Builds "export VAR=value" lines for the variables not already set, filled
# from the live configuration; the values only ever go to this shell. Kept in
# a variable first so a failure stops the script instead of being swallowed.
exports="$(CURRENT_SETTINGS="$current_settings" CURRENT_CONNECTION_STRINGS="$current_connection_strings" python3 - <<'PY'
import json, os, shlex

settings = {s["name"]: s["value"] for s in json.loads(os.environ["CURRENT_SETTINGS"])}
connection_strings = {c["name"]: c["value"] for c in json.loads(os.environ["CURRENT_CONNECTION_STRINGS"])}
clients = {settings.get(f"Auth__Clients__{i}__ClientId"): settings.get(f"Auth__Clients__{i}__ClientSecret") for i in range(10)}

wanted = {
    "ASSETSYNC_SQL_CONNECTION_STRING": connection_strings.get("AssetSyncDb"),
    "ASSETSYNC_JWT_SIGNING_KEY": settings.get("Jwt__SigningKey"),
    "ASSETSYNC_RABBITMQ_CONNECTION_STRING": settings.get("RabbitMq__ConnectionString"),
    "ASSETSYNC_CLIENT_ERP_INTEGRATION_SECRET": clients.get("erp-integration"),
    "ASSETSYNC_CLIENT_ASSET_ADMIN_SECRET": clients.get("asset-admin"),
}
for name, value in wanted.items():
    if os.environ.get(name):
        continue
    if not value:
        raise SystemExit(f"{name} is not set and the web app has no current value for it.")
    print(f"export {name}={shlex.quote(value)}")
PY
)"
eval "$exports"

args=(--resource-group "$RESOURCE_GROUP" --template-file "$HERE/main.bicep" --parameters "$HERE/main.bicepparam")

az deployment group what-if "${args[@]}"

if [[ "${1:-}" == "--apply" ]]; then
  read -r -p "Apply these changes? [y/N] " answer
  [[ "$answer" == "y" ]] || { echo "Cancelled."; exit 0; }
  az deployment group create "${args[@]}" --name "assetsync-$(date +%Y%m%d-%H%M%S)" --query "properties.outputs" -o json
fi
