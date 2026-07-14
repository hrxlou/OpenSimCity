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
UNITY_LOCK_TIMEOUT_SECONDS=60

FATAL_ERROR_SIGNATURES=(
  'Aborting batchmode due to fatal error'
  'Multiple Unity instances cannot open the same project'
  'It looks like another Unity instance is running'
  'No valid Unity Editor license found'
  'Failed to activate/update license'
  'Scripts have compiler errors'
)

if [[ ! -x "${UNITY_EDITOR}" ]]; then
  printf 'Unity Editor not found at %s\nSet UNITY_EDITOR to the executable path.\n' "${UNITY_EDITOR}" >&2
  exit 1
fi

if [[ ! "${UNITY_TEST_TIMEOUT_SECONDS}" =~ ^[1-9][0-9]*$ ]]; then
  printf 'UNITY_TEST_TIMEOUT_SECONDS must be a positive integer: %s\n' "${UNITY_TEST_TIMEOUT_SECONDS}" >&2
  exit 2
fi

mkdir -p "${RESULTS_DIR}"

find_unity_fatal_error() {
  local log_path="$1"
  local signature

  [[ -f "${log_path}" ]] || return 1
  for signature in "${FATAL_ERROR_SIGNATURES[@]}"; do
    if grep -Fq -- "${signature}" "${log_path}"; then
      printf '%s' "${signature}"
      return 0
    fi
  done

  return 1
}

wait_for_unity_lock_release() {
  local lock_path="${REPO_ROOT}/Temp/UnityLockfile"
  local elapsed=0

  while [[ -e "${lock_path}" && "${elapsed}" -lt "${UNITY_LOCK_TIMEOUT_SECONDS}" ]]; do
    sleep 1
    elapsed=$((elapsed + 1))
  done

  if [[ -e "${lock_path}" ]]; then
    printf 'Unity project lock was not released after %ss: %s\n' \
      "${UNITY_LOCK_TIMEOUT_SECONDS}" "${lock_path}" >&2
    return 1
  fi
}

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
  local fatal_error

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
    if fatal_error="$(find_unity_fatal_error "${log_path}")"; then
      printf "%s tests failed: Unity stopped with fatal error '%s'. Log: %s\n" \
        "${platform}" "${fatal_error}" "${log_path}" >&2
      return 1
    fi

    sleep 2
    elapsed=$((elapsed + 2))
  done

  if [[ ! -s "${results_path}" ]]; then
    if fatal_error="$(find_unity_fatal_error "${log_path}")"; then
      printf "%s tests failed: Unity stopped with fatal error '%s'. Log: %s\n" \
        "${platform}" "${fatal_error}" "${log_path}" >&2
    else
      printf '%s tests failed: no result XML after %ss (Unity exit code=%s). Log: %s\n' \
        "${platform}" "${UNITY_TEST_TIMEOUT_SECONDS}" "${unity_exit_code}" "${log_path}" >&2
    fi
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
  if [[ ! "${total}" =~ ^[1-9][0-9]*$ ]]; then
    printf '%s tests failed: discovered test count is zero or invalid (total=%s). Log: %s, Results: %s\n' \
      "${platform}" "${total:-<empty>}" "${log_path}" "${results_path}" >&2
    return 1
  fi

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
    # Wait for the lock only before the next run. After the final run the wait
    # is pointless, and a lingering lock could flip a passing run into a failure.
    wait_for_unity_lock_release
    run_tests PlayMode playmode
    ;;
  *)
    printf 'Usage: %s [edit|play|all]\n' "$0" >&2
    exit 2
    ;;
esac

printf 'Unity tests passed. Results: %s\n' "${RESULTS_DIR}"
