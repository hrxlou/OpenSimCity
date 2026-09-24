#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
EXPECTED_QUALITY_VALUES="0 0 0 0 2 4"

fail() {
  printf 'ProjectSettings baseline guard failed: %s\n' "$1" >&2
  exit 1
}

get_indexed_file() {
  local relative_path="$1"
  local content

  if ! content="$(git -c "safe.directory=${REPO_ROOT}" -C "${REPO_ROOT}" show ":${relative_path}" 2>/dev/null)"; then
    fail "Git index does not contain ${relative_path}."
  fi

  printf '%s' "${content}"
}

graphics_settings="$(get_indexed_file 'ProjectSettings/GraphicsSettings.asset')"
quality_settings="$(get_indexed_file 'ProjectSettings/QualitySettings.asset')"

graphics_value_count="$(printf '%s\n' "${graphics_settings}" | awk '$1 == "m_LightsUseColorTemperature:" { count++ } END { print count + 0 }')"
if [[ "${graphics_value_count}" -ne 1 ]]; then
  fail "expected exactly one m_LightsUseColorTemperature entry in the Git index, found ${graphics_value_count}."
fi

graphics_value="$(printf '%s\n' "${graphics_settings}" | awk '$1 == "m_LightsUseColorTemperature:" { print $2 }')"
if [[ "${graphics_value}" != "1" ]]; then
  fail "GraphicsSettings.asset in the Git index has m_LightsUseColorTemperature=${graphics_value}; expected 1."
fi

quality_value_count="$(printf '%s\n' "${quality_settings}" | awk '$1 == "antiAliasing:" { count++ } END { print count + 0 }')"
quality_values="$(printf '%s\n' "${quality_settings}" | awk '$1 == "antiAliasing:" { if (count > 0) printf " "; printf "%s", $2; count++ }')"
if [[ "${quality_value_count}" -ne 6 || "${quality_values}" != "${EXPECTED_QUALITY_VALUES}" ]]; then
  fail "QualitySettings.asset in the Git index has antiAliasing values [${quality_values}]; expected [${EXPECTED_QUALITY_VALUES}]."
fi

printf 'Committed ProjectSettings baseline passed (Git index; unstaged Unity drift ignored).\n'
