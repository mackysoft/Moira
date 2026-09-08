#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repository_root="$(cd -- "$script_directory/.." && pwd -P)"

usage() {
  printf 'Usage: %s [format|verify]\n' "$(basename -- "$0")" >&2
}

if [[ $# -ne 1 ]]; then
  usage
  exit 2
fi

cd "$repository_root"

case "$1" in
  format)
    dotnet format MackySoft.Moira.slnx
    ;;
  verify)
    dotnet format MackySoft.Moira.slnx --verify-no-changes --no-restore
    ;;
  *)
    usage
    exit 2
    ;;
esac
