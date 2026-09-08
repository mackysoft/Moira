#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repository_root="$(cd -- "$script_directory/.." && pwd -P)"
report_path="$repository_root/.dotmet/local/analysis.json"

if [[ $# -ne 0 ]]; then
  printf 'Usage: %s\n' "$(basename -- "$0")" >&2
  exit 2
fi

cd "$repository_root"
rm -f -- "$report_path"

# Restore belongs to the caller so analysis uses the same package inputs as build and tests.
analysis_exit_code=0
dotnet tool run dotmet -- analyze \
  --repositoryRoot "$repository_root" \
  --solutionPath "$repository_root/MackySoft.Moira.slnx" \
  --targetKind dotnet \
  --rulesPath "$repository_root/.dotmet/rules.json" \
  --comparisonMode none \
  --no-restore \
  --cacheMode off \
  --outputPath "$report_path" \
  --pretty || analysis_exit_code=$?

if [[ $analysis_exit_code -ne 0 ]] || ! jq -e '
  .contractVersion == 1
  and .reportKind == "analysis"
  and .status == "ok"
  and .verdict == "pass"
  and .analysisCompleteness == "full"
  and .execution.rulesCoverage.state == "full"
  and .comparison.mode == "none"
  and .errors == []
  and .partialFailures == []
' "$report_path" >/dev/null; then
  if [[ -f "$report_path" ]]; then
    jq '{
      status,
      verdict,
      analysisCompleteness,
      rulesCoverage: .execution.rulesCoverage,
      summary,
      findings,
      errors,
      partialFailures
    }' "$report_path" >&2
  fi

  printf 'analyze: failed; report: %s\n' "$report_path" >&2
  exit 1
fi

printf 'analyze: passed; report: %s\n' "$report_path"
