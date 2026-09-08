#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repository_root="$(cd -- "$script_directory/.." && pwd -P)"
solution_path="$repository_root/MackySoft.Moira.slnx"

usage() {
  printf 'Usage: %s\n' "$(basename -- "$0")" >&2
}

if [[ $# -ne 0 ]]; then
  usage
  exit 2
fi

temporary_root="$(mktemp -d /tmp/moira-verify.XXXXXX)"
cleanup() {
  rm -rf -- "$temporary_root"
}
trap cleanup EXIT

export DOTNET_CLI_HOME="$temporary_root/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_HTTP_CACHE_PATH="$temporary_root/nuget-http-cache"
export NUGET_PACKAGES="$temporary_root/nuget-packages"
export NUGET_PLUGINS_CACHE_PATH="$temporary_root/nuget-plugins-cache"

mkdir -p "$DOTNET_CLI_HOME" "$NUGET_HTTP_CACHE_PATH" "$NUGET_PACKAGES" "$NUGET_PLUGINS_CACHE_PATH"
cd "$repository_root"

dotnet tool restore --tool-manifest "$repository_root/dotnet-tools.json"
dotnet restore "$solution_path"
"$script_directory/code-quality.sh" verify
dotnet build "$solution_path" --configuration Release --no-restore
dotnet test "$solution_path" --configuration Release --no-build --no-restore
bash "$script_directory/analyze-dotnet.sh"

package_directory="$temporary_root/packages"
mkdir -p "$package_directory"
dotnet pack src/MackySoft.Moira/MackySoft.Moira.csproj --configuration Release --no-build --no-restore --output "$package_directory"

shopt -s nullglob
package_files=("$package_directory"/MackySoft.Moira.0.0.0.nupkg)
shopt -u nullglob
if [[ ${#package_files[@]} -ne 1 ]]; then
  printf 'verify: expected exactly one MackySoft.Moira.0.0.0.nupkg, found %s\n' "${#package_files[@]}" >&2
  exit 1
fi

package_file="${package_files[0]}"
for package_entry in \
  'lib/netstandard2.1/MackySoft.Moira.dll' \
  'lib/netstandard2.1/MackySoft.Moira.xml' \
  'README.md' \
  'LICENSE'; do
  if ! unzip -Z1 "$package_file" | grep -Fx "$package_entry" >/dev/null; then
    printf 'verify: package is missing %s\n' "$package_entry" >&2
    exit 1
  fi
done

nuspec_path="$(unzip -Z1 "$package_file" | awk '/\.nuspec$/')"
if [[ -z "$nuspec_path" || "$nuspec_path" == *$'\n'* ]]; then
  printf 'verify: package must contain exactly one nuspec\n' >&2
  exit 1
fi

package_metadata="$(unzip -p "$package_file" "$nuspec_path" | tr -d '\r\n')"
package_id="$(sed -n 's|.*<id>\([^<]*\)</id>.*|\1|p' <<< "$package_metadata")"
package_version="$(sed -n 's|.*<version>\([^<]*\)</version>.*|\1|p' <<< "$package_metadata")"
package_license="$(sed -n 's|.*<license[^>]*>\([^<]*\)</license>.*|\1|p' <<< "$package_metadata")"
package_repository_url="$(sed -n 's|.*<repository[^>]* url="\([^"]*\)".*|\1|p' <<< "$package_metadata")"
if [[ "$package_id" != 'MackySoft.Moira' || "$package_version" != '0.0.0' \
  || "$package_license" != 'MIT' || "$package_repository_url" != 'https://github.com/mackysoft/Moira' ]]; then
  printf 'verify: package metadata does not match MackySoft.Moira\n' >&2
  exit 1
fi

consumer_directory="$temporary_root/consumer"
mkdir -p "$consumer_directory"
cat > "$consumer_directory/Moira.PackageSmoke.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MackySoft.Moira" Version="0.0.0" />
  </ItemGroup>
</Project>
EOF
cat > "$consumer_directory/Program.cs" <<'EOF'
using MackySoft.Moira;

WeightedDistribution<string> distribution = new(
[
    new WeightedEntry<string>("selected", 1d),
],
WeightedSelectionMethod.Cumulative);

if (distribution.Select(0d) != "selected")
{
    return 1;
}

WeightedBag<string> bag = new WeightedBagDefinition<string>(
[
    new WeightedBagEntry<string>("item", 1d, 2),
]).CreateBag();

_ = bag.Draw(0d);
return bag.RemainingItemCount == 1 ? 0 : 1;
EOF

dotnet restore "$consumer_directory/Moira.PackageSmoke.csproj" --source "$package_directory" --ignore-failed-sources
dotnet build "$consumer_directory/Moira.PackageSmoke.csproj" --configuration Release --no-restore
dotnet run --project "$consumer_directory/Moira.PackageSmoke.csproj" --configuration Release --no-build --no-restore

printf 'verify: passed\n'
