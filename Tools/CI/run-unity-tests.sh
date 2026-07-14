#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/{print $2; exit}' "${REPO_ROOT}/ProjectSettings/ProjectVersion.txt")"
DEFAULT_UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY}}"
MODE="${1:-all}"
UNITY_TEST_TIMEOUT_SECONDS="${UNITY_TEST_TIMEOUT_SECONDS:-1800}"
RESULTS_DIR="${REPO_ROOT}/TestResults"

if [[ ! -x "${UNITY_EDITOR}" ]]; then
  printf 'Unity Editor not found at %s\nSet UNITY_EDITOR to the executable path.\n' "${UNITY_EDITOR}" >&2
  exit 1
fi

if [[ ! "${UNITY_TEST_TIMEOUT_SECONDS}" =~ ^[1-9][0-9]*$ ]]; then
  printf 'UNITY_TEST_TIMEOUT_SECONDS must be a positive integer: %s\n' "${UNITY_TEST_TIMEOUT_SECONDS}" >&2
  exit 2
fi

mkdir -p "${RESULTS_DIR}"

run_tests() {
  local platform="$1"
  local slug="$2"
  local results_path="${RESULTS_DIR}/${slug}.xml"
  local log_path="${RESULTS_DIR}/${slug}.log"
  local unity_exit_code
  local elapsed=0
  local test_result
  local passed
  local total

  # Never let a previous run satisfy the result check.
  rm -f "${results_path}" "${log_path}"

  printf 'Running %s tests...\n' "${platform}"

  # Unity may relaunch itself in batch mode. The first process can therefore
  # return a non-zero or otherwise unreliable code before the real result XML
  # is written. Keep the code for diagnostics and judge the test from XML.
  set +e

  "${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${REPO_ROOT}" \
    -runTests \
    -testPlatform "${platform}" \
    -testResults "${results_path}" \
    -logFile "${log_path}"
  unity_exit_code=$?
  set -e

  while [[ ! -s "${results_path}" && "${elapsed}" -lt "${UNITY_TEST_TIMEOUT_SECONDS}" ]]; do
    sleep 2
    elapsed=$((elapsed + 2))
  done

  if [[ ! -s "${results_path}" ]]; then
    printf '%s tests failed: no result XML after %ss (Unity exit code=%s). Log: %s\n' \
      "${platform}" "${UNITY_TEST_TIMEOUT_SECONDS}" "${unity_exit_code}" "${log_path}" >&2
    return 1
  fi

  if ! test_result="$(xmllint --xpath 'string(/*[local-name()="test-run"]/@result)' "${results_path}" 2>/dev/null)"; then
    printf '%s tests failed: result XML is not valid. Log: %s, Results: %s\n' \
      "${platform}" "${log_path}" "${results_path}" >&2
    return 1
  fi

  if [[ -z "${test_result}" ]]; then
    printf '%s tests failed: result XML has no test-run result. Log: %s, Results: %s\n' \
      "${platform}" "${log_path}" "${results_path}" >&2
    return 1
  fi

  if [[ "${test_result}" != "Passed" ]]; then
    printf '%s tests failed (result=%s, Unity exit code=%s). Log: %s, Results: %s\n' \
      "${platform}" "${test_result}" "${unity_exit_code}" "${log_path}" "${results_path}" >&2
    return 1
  fi

  passed="$(xmllint --xpath 'string(/*[local-name()="test-run"]/@passed)' "${results_path}" 2>/dev/null || true)"
  total="$(xmllint --xpath 'string(/*[local-name()="test-run"]/@total)' "${results_path}" 2>/dev/null || true)"
  printf '%s tests passed (%s/%s). Results: %s, Log: %s\n' \
    "${platform}" "${passed:-?}" "${total:-?}" "${results_path}" "${log_path}"

  if [[ "${unity_exit_code}" -ne 0 ]]; then
    printf 'Unity launcher exit code was %s, but the NUnit result was Passed; continuing.\n' \
      "${unity_exit_code}" >&2
  fi
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
