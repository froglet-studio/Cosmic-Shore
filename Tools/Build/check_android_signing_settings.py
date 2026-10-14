#!/usr/bin/env python3
"""Keep the Android upload key out of the repo: no keystore path or key alias in ProjectSettings.

Why: selecting a keystore under Player Settings > Android > Publishing Settings writes its PATH
into ProjectSettings.asset (`AndroidKeystoreName`, `AndroidKeyaliasName`,
`androidUseCustomKeystore`). That path is one machine's file, so a committed one breaks every
other machine's build. It happened: bleeding-edge carried a path to a file on one developer's PC
that belonged to another game (TailGlider) until 2026-10-08 (Docs/PLATFORM_UNIFICATION.md §3.9).

Each machine selects the Cosmic Shore upload key locally, or sets the CS_ANDROID_* environment
variables that `CosmicShoreBuildPipeline.BuildAndroidRelease` reads and then puts back
(Docs/ANDROID_BUILD.md §1). Unity never serialises the passwords, so this only checks the path,
the alias and the switch.

Fails when any of the three is set. The fix is to discard those lines from the commit, not to
clear them in the editor (that would also clear the local selection).

Usage:
    check_android_signing_settings.py [--self-test] [path/to/ProjectSettings.asset]
"""

import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_PATH = os.path.join(ROOT, "ProjectSettings", "ProjectSettings.asset")

# key -> the only value allowed in the repo
EXPECTED = {
    "AndroidKeystoreName": "",
    "AndroidKeyaliasName": "",
    "androidUseCustomKeystore": "0",
}


def problems(text):
    """Every signing field that is set, as human-readable lines. Missing keys are fine."""
    found = []
    for key, allowed in EXPECTED.items():
        m = re.search(r"^  " + re.escape(key) + r":[ ]?(.*)$", text, re.M)
        if not m:
            continue
        value = m.group(1).strip().strip("'\"")
        if value != allowed:
            found.append(f"{key} is '{value}' (the repo must carry '{allowed}')")
    return found


def self_test():
    clean = "PlayerSettings:\n  AndroidKeystoreName: \n  AndroidKeyaliasName: \n  androidUseCustomKeystore: 0\n"
    leaked = ("PlayerSettings:\n  AndroidKeystoreName: C:/Users/me/upload.keystore\n"
              "  AndroidKeyaliasName: cosmicshore-upload\n  androidUseCustomKeystore: 1\n")
    quoted = "PlayerSettings:\n  AndroidKeystoreName: ''\n  AndroidKeyaliasName: ''\n  androidUseCustomKeystore: 0\n"
    assert problems(clean) == [], problems(clean)
    assert problems(quoted) == [], problems(quoted)
    assert len(problems(leaked)) == 3, problems(leaked)
    assert problems("PlayerSettings:\n  companyName: x\n") == []
    # A key elsewhere in the file with the same suffix must not be read.
    assert problems("  someAndroidKeystoreName: x\n  AndroidKeystoreName: \n") == []
    print("self-test passed: clean, quoted-empty and absent pass; a leaked path, alias and switch fail.")


def main(argv):
    if "--self-test" in argv:
        self_test()
        return 0
    paths = [a for a in argv if not a.startswith("--")] or [DEFAULT_PATH]
    failed = False
    for path in paths:
        with open(path, encoding="utf-8") as f:
            found = problems(f.read())
        for p in found:
            print(f"{os.path.relpath(path, ROOT)}: {p}")
        failed |= bool(found)
    if failed:
        print("Android signing settings: FAIL. A keystore selection is per machine; discard these "
              "lines from the commit (Docs/ANDROID_BUILD.md section 1).")
        return 1
    print("Android signing settings: OK (no keystore path, alias or switch in the repo).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
