#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/{print $2; exit}' "${REPO_ROOT}/ProjectSettings/ProjectVersion.txt")"
DEFAULT_UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY}}"
OUTPUT_PATH="${1:-${REPO_ROOT}/build/StandaloneOSX/OpenSimCity.app}"

if [[ ! -x "${UNITY_EDITOR}" ]]; then
  printf 'Unity Editor not found at %s\nSet UNITY_EDITOR to the executable path.\n' "${UNITY_EDITOR}" >&2
  exit 1
fi

SOURCE_COMMIT="$(git -C "${REPO_ROOT}" rev-parse HEAD)"
STATUS_ROWS="$(git -C "${REPO_ROOT}" status --porcelain --untracked-files=all)"
if [[ -z "${STATUS_ROWS}" ]]; then
  CLEAN_CHECKOUT=1
else
  CLEAN_CHECKOUT=0
fi

RESULTS_DIR="${REPO_ROOT}/TestResults"
mkdir -p "${RESULTS_DIR}"
LOG_FILE="${RESULTS_DIR}/build-macos.log"
RESULT_FILE="${RESULTS_DIR}/build-macos.result"
EVIDENCE_FILE="${RESULTS_DIR}/build-macos.release-evidence.json"
rm -f "${LOG_FILE}" "${RESULT_FILE}" "${EVIDENCE_FILE}"

printf 'Building StandaloneOSX -> %s\n' "${OUTPUT_PATH}"
printf 'Release source=%s cleanCheckout=%s\n' "${SOURCE_COMMIT}" "${CLEAN_CHECKOUT}"

set +e
"${UNITY_EDITOR}" \
  -batchmode \
  -quit \
  -nographics \
  -projectPath "${REPO_ROOT}" \
  -executeMethod OpenSimCity.Editor.BuildScript.PerformMacOSBuild \
  -osc-buildPath "${OUTPUT_PATH}" \
  -osc-buildResultPath "${RESULT_FILE}" \
  -osc-buildEvidencePath "${EVIDENCE_FILE}" \
  -osc-sourceCommit "${SOURCE_COMMIT}" \
  -osc-cleanCheckout "${CLEAN_CHECKOUT}" \
  -logFile "${LOG_FILE}"
UNITY_EXIT_CODE=$?
set -e

TIMEOUT_SECONDS="${UNITY_BUILD_TIMEOUT_SECONDS:-1800}"
elapsed=0
while [[ ! -s "${RESULT_FILE}" && "${elapsed}" -lt "${TIMEOUT_SECONDS}" ]]; do
  sleep 2
  elapsed=$((elapsed + 2))
done

if [[ ! -s "${RESULT_FILE}" ]]; then
  printf 'macOS build result was not produced after %ss (Unity exit code=%s). Log: %s\n' \
    "${TIMEOUT_SECONDS}" "${UNITY_EXIT_CODE}" "${LOG_FILE}" >&2
  exit 1
fi

OUTCOME="$(head -n 1 "${RESULT_FILE}" | tr -d '\r\n')"
if [[ "${OUTCOME}" != "Succeeded" || ! -d "${OUTPUT_PATH}" || ! -s "${EVIDENCE_FILE}" ]]; then
  printf 'macOS build failed (result=%s, Unity exit code=%s). Log: %s, evidence: %s\n' \
    "${OUTCOME}" "${UNITY_EXIT_CODE}" "${LOG_FILE}" "${EVIDENCE_FILE}" >&2
  exit 1
fi

printf 'macOS build succeeded: %s\nRelease evidence: %s\n' "${OUTPUT_PATH}" "${EVIDENCE_FILE}"
