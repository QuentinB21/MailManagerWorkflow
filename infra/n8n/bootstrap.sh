#!/bin/sh
set -eu

force_import="${N8N_BOOTSTRAP_FORCE_IMPORT:-false}"
marker_directory="/home/node/.n8n/mail-manager-bootstrap"
mkdir -p "${marker_directory}"

import_workflow() {
  workflow_id="$1"
  workflow_file="$2"
  marker_file="${marker_directory}/${workflow_id}.imported"
  workflow_hash="$(sha256sum "${workflow_file}" | cut -d ' ' -f 1)"
  imported_hash="$(cat "${marker_file}" 2>/dev/null || true)"

  if [ "${imported_hash}" = "${workflow_hash}" ] && [ "${force_import}" != "true" ]; then
    echo "Bootstrap n8n: workflow ${workflow_id} déjà initialisé, import ignoré."
  else
    if [ "${force_import}" = "true" ]; then
      echo "Bootstrap n8n: synchronisation forcée de ${workflow_id} depuis le JSON versionné."
    fi
    echo "Bootstrap n8n: import de ${workflow_file}."
    n8n import:workflow --input="${workflow_file}"
    n8n publish:workflow --id="${workflow_id}"
    printf '%s\n' "${workflow_hash}" > "${marker_file}"
    echo "Bootstrap n8n: workflow ${workflow_id} importé et publié."
  fi
}

import_workflow "6bca4f35-87e1-4d3b-b628-d6aee1252fe7" "/opt/mail-manager/workflows/fake-email-classification.json"
import_workflow "9a9cfe88-4a5c-4d7f-9d25-6f0c44b2f1e2" "/opt/mail-manager/workflows/mailbox-sync.json"

exec n8n start
