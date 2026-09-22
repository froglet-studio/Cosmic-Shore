#!/usr/bin/env python3
"""The credits manifest carries FMOD's required in-game credit — offline gate.

`Assets/Plugins/FMOD/LICENSE.txt` clause 3, verbatim:

    3. CREDITS

    All Products require an in game credit line which must include the words "FMOD"
    and "Firelight Technologies Pty Ltd." Refer to www.fmod.com/attribution for
    examples.

That applies on EVERY tier — free, Indie and Basic alike — so it is not contingent on
the separate licence-tier question. This checks the one thing a text tool can check
without Unity: that `Assets/Resources/CreditsManifest.asset` exists and its authored
text still contains both required strings.

It is the offline twin of `Assets/_Scripts/Editor/Build/CreditsReleaseGuard.cs`, which
fails a non-development build on the same condition. Two gates because they answer in
different places: the guard catches it at build time on a machine with the editor, this
catches it in CI and in a session that cannot open one.

WHAT THIS DOES NOT PROVE. It reads the asset as TEXT, so it proves the words are in the
file — not that the screen is reachable, not that the modal is wired into a scene, and
not that anything renders. Reachability is verified in the editor; see
`Docs/THIRD_PARTY_REGISTER.md` §7.

Usage:
  python3 Tools/Build/check_credits_manifest.py            # report
  python3 Tools/Build/check_credits_manifest.py --check    # exit 1 on failure (CI)
  python3 Tools/Build/check_credits_manifest.py --self-test
"""
import argparse
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MANIFEST = os.path.join(ROOT, "Assets", "Resources", "CreditsManifest.asset")

# Quoted verbatim from FMOD's EULA clause 3. The trailing period on the company name is
# INSIDE the quoted requirement — do not "tidy" it away.
REQUIRED = ('FMOD', 'Firelight Technologies Pty Ltd.')


def audit(text):
    """Returns the list of required strings that are missing from `text`."""
    if text is None:
        return list(REQUIRED)
    return [needle for needle in REQUIRED if needle not in text]


def read_manifest(path=MANIFEST):
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def run(check):
    rel = os.path.relpath(MANIFEST, ROOT)
    text = read_manifest()

    if text is None:
        print(f"credits-manifest check: FAILED — {rel} does not exist.")
        print("  FMOD's EULA clause 3 requires an in-game credit containing the words")
        print('  "FMOD" and "Firelight Technologies Pty Ltd." on every licence tier.')
        print("  Fix: restore the manifest asset (see Docs/THIRD_PARTY_REGISTER.md §7).")
        return 1 if check else 0

    missing = audit(text)
    if missing:
        print(f"credits-manifest check: FAILED — {rel} is missing required text.")
        for needle in missing:
            print(f'  • the words "{needle}" appear nowhere in the manifest')
        print("  FMOD's EULA clause 3 requires BOTH, in game, on every licence tier.")
        print("  Fix: restore the credit line in the MIDDLEWARE section. The conventional")
        print('  accepted form is "Made with FMOD Studio by Firelight Technologies Pty Ltd."')
        return 1 if check else 0

    print(f"credits-manifest check: OK — {rel} carries FMOD's required credit "
          f"({len(text)} bytes scanned).")
    return 0


def self_test():
    """Negative control. A gate nobody has watched fail is a gate nobody should trust."""
    failures = []

    def expect(label, text, want_missing):
        got = audit(text)
        if bool(got) != want_missing:
            failures.append(
                f"{label}: expected missing={want_missing}, got {got or 'nothing missing'}")
        else:
            print(f"  ok  {label} → {'reported missing: ' + ', '.join(got) if got else 'passes'}")

    real = read_manifest()
    if real is None:
        failures.append("the shipped manifest is absent, so the positive case cannot be proved")
    else:
        expect("the shipped manifest", real, want_missing=False)

        # THE case this gate exists for: somebody edits the FMOD line out.
        stripped = real.replace("Firelight Technologies Pty Ltd.", "")
        expect("manifest with the company name removed", stripped, want_missing=True)

        no_fmod = real.replace("FMOD", "")
        expect("manifest with every 'FMOD' removed", no_fmod, want_missing=True)

        # A near miss: the company without its trailing period is NOT what the EULA quotes.
        near = real.replace("Firelight Technologies Pty Ltd.", "Firelight Technologies Pty Ltd")
        expect("manifest with the trailing period dropped", near, want_missing=True)

    expect("a missing manifest", None, want_missing=True)

    # And the shapes that must NOT fire.
    expect("minimal text carrying both required strings",
           'Made with FMOD Studio by Firelight Technologies Pty Ltd.', want_missing=False)

    if failures:
        print("\nself-test FAILED:")
        for line in failures:
            print("   " + line)
        return 1
    print("\nself-test OK — the gate fires on a removed credit line and passes on a real one.")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true",
                        help="exit 1 when the required credit is missing (for CI)")
    parser.add_argument("--self-test", action="store_true",
                        help="prove the gate fails on a manifest with the FMOD line removed")
    args = parser.parse_args()
    return self_test() if args.self_test else run(args.check)


if __name__ == "__main__":
    sys.exit(main())
