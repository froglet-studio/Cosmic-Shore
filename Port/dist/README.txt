Prisma - distributables

Once you have any Prisma from 2026-10-07 or later, its UPDATE button downloads new versions by
itself (from the "Prisma launcher release" workflow on GitHub) and restarts into them: no zip.
A first copy: the newest run of that workflow (GitHub > Actions) has Prisma.exe as its artifact,
or a release "Prisma <commit>" when the repository allows Actions to publish releases.

Prisma-Windows.zip  - the same launcher, for a first copy without the Releases page. Unzip, run
    Prisma.exe, pick a branch, press START GAME. It builds the game from that branch's source,
    so it is never stale. Rebuild it with ..\build-launcher.bat. Guide: ..\docs\LAUNCHER.md
