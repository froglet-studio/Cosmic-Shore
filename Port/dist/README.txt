Temporary distribution channel: chat attachments proved non-downloadable for
large binaries, so progress builds are committed here. This folder is dropped
before any merge to a mainline branch (a squash-merge never carries the blob).

CosmicShore-Player-Windows.zip  - CURRENT. The real game (Assets/_Scripts compiled
    against the port engine), self-contained win-x64, FMOD runtime included.
    Run it with ..\play-player.bat from inside a clone of this branch: it reads
    content from the clone's Assets/ folder.
CosmicShore-Windows.zip, SkimRace-Windows.zip - SUPERSEDED. The earlier
    hand-ported SkimRace client; kept only until the next cleanup.

Build recipe:
  dotnet publish src/CosmicShore.Player -c Release -r win-x64 --self-contained true -o <out>
  python3 tools/fetch_native.py --platform win-x64 ; copy .native/win-x64/fmodstudio.dll <out>
  zip <out> (no .pdb) -> dist/CosmicShore-Player-Windows.zip
