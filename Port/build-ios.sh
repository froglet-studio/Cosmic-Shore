#!/bin/sh
# Builds the iOS app the way Unity's Build Settings > iOS does. Run on a Mac with Xcode and
# the .NET 10 SDK (the first run installs the .NET iOS workload). On Windows/Linux this only
# exports Builds/iOS (player data + build-ios.sh) for a Mac to finish.
# Signing: export CS_IOS_CODESIGN_KEY="Apple Development: Name (TEAMID)" and
#          CS_IOS_PROVISIONING_PROFILE=<profile name or UUID> first (or let Xcode choose).
set -e
cd "$(dirname "$0")/.."
dotnet run --project Port/src/CosmicShore.Build -c Release -- ios "$@"
