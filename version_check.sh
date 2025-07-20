#!/bin/bash

csprojVer=$(grep -P "<Version>[0-9]+\.[0-9]+\.[0-9]+<\/Version>" TootTallyDiffCalcLibs.csproj | sed -E --expression="s/(<[\/]*Version>)| |\t//g");
manifestVer=$(grep -P "\"version_number\":( )*\"[0-9]+\.[0-9]+\.[0-9]+\"" thunderstore/manifest.json | sed -E --expression="s/(version_number)| |,|:|\"//g");
readmeVer=$(grep -P "> Version: \d+.\d+.\d+" README.md | sed -E --expression="s/> Version: //g");

echo .csproj Version: "$csprojVer";
echo manifest.json Version: "$manifestVer";
echo README.md Version: "$readmeVer";

if [[ "$csprojVer" != "$manifestVer" ]]; then
  echo Version check unsuccessful: csproj is not the same as manifest;
  exit 1;
fi

if [[ "$manifestVer" != "$readmeVer" ]]; then
    echo Version check unsuccessful: manifest is not the same as readme;
  exit 1;
fi

echo Version check successful!;
echo version="$csprojVer" >> $GITHUB_OUTPUT
exit 0;
