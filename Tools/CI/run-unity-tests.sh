#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/{print $2; exit}' "${REPO_ROOT}/ProjectSettings/ProjectVersion.txt")"
DEFAULT_UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY}}"
MODE="${1:-all}"
RESULTS_DIR="${REPO_ROOT}/TestResults"

if [[ ! -x "${UNITY_EDITOR}" ]]; then
  printf 'Unity Editor not found at %s\nSet UNITY_EDITOR to the executable path.\n' "${UNITY_EDITOR}" >&2
  exit 1
fi

mkdir -p "${RESULTS_DIR}"

run_tests() {
  local platform="$1"
  local slug="$2"

  "${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${REPO_ROOT}" \
    -runTests \
    -testPlatform "${platform}" \
    -testResults "${RESULTS_DIR}/${slug}.xml" \
    -logFile "${RESULTS_DIR}/${slug}.log"
}

case "${MODE}" in
  edit)
    run_tests EditMode editmode
    ;;
  play)
    run_tests PlayMode playmode
    ;;
  all)
    run_tests EditMode editmode
    run_tests PlayMode playmode
    ;;
  *)
    printf 'Usage: %s [edit|play|all]\n' "$0" >&2
    exit 2
    ;;
esac

printf 'Unity tests passed. Results: %s\n' "${RESULTS_DIR}"
