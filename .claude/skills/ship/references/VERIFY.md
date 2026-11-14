# /ship §0.05 in full: reading gate output, what refcompile buys, Amoebius

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

Read when a gate exits non-zero and you are about to report it, when you need to state what `unity_refcompile` proved, or when Amoebius comes up.

- **These gates do not share a command line, and a wrong flag exits 2 — which reads as a
  FAILURE.** `check_enum_member_references.py`, `check_switch_label_collisions.py`,
  `check_using_directives.py` and `check_elemental_floats.py` take `--check`;
  `check_conditional_compilation.py` takes none (it just runs) and
  `check_self_referential_locals.py` wants `--all` or a path list. Passing `--check` to either of
  the last two gets `error: unrecognized arguments: --check` and exit 2, which in a sweep is
  indistinguishable from the gate finding something — the sibling of this file's own "a gate that
  ABORTS looks exactly like a gate that passes". **Read the first line of any non-zero exit before
  reporting it**; `usage:` means you called it wrong, not that the tree is dirty.

**And read the exit code of the SCRIPT, not of the pager you piped it into.** `python3
check.py --check | tail -5; echo $?` reports **`tail`'s** status, which is 0 whatever the
script did — so a gate printing `FAIL` in the very output you are reading comes back "exit
0". That is this file's own *"a gate that ABORTS looks exactly like a gate that passes"*,
produced by the measurement rather than by the gate, and it is worse than the original
because you have the failure text on screen and a number telling you to ignore it. Run the
script bare (`script >/dev/null 2>&1; echo $?`), or read `${PIPESTATUS[0]}`. **A command substitution in the SAME
`echo` resets it too:** `check; echo "$gen @ $(git rev-parse --short HEAD): exit $?"` prints `git`'s status, so an A/B
of a red generator against its merge's parent read "exit 0" on BOTH sides, twice in one session
(`claude/ci-runs-failing-i6u77f`, 2026-11-01). Capture first: `check; rc=$?; echo "... exit $rc"`. Do this on the
negative control too: a `--check` you have only ever watched SUCCEED is a `--check` you have
not tested — mutate one authored value, confirm it exits non-zero AND names the file, then
restore and confirm the tree is clean (`git status --short` on the asset path).

**What `Tools/Build/unity_refcompile` buys (2026-10).** It binds the whole of `Assembly-CSharp`,
method bodies included, against real Unity 6 reference assemblies and every package at its locked
source — not the syntax-only Roslyn pass warned about above — and `check_generated_assets.py` then
audits changed YAML assets against the schema it wrote. A green run of it (negative-controlled) IS
evidence: the verification line may say "compiled against Unity reference assemblies
(`unity_refcompile`), not opened in the editor" instead of "not compiled". How to run it, and its
false positives: `/asset-surgery` §4.

**Amoebius is not part of a ship.** A ship never builds, tests or patches Amoebius (Shombith, 2026-10-31). The
engine is brought up to date with the game every three weeks by the scheduled **Amoebius update** task (a fresh cloud
session: `dotnet build Port/src/CosmicShore.Player`, `dotnet test Port/tests/CosmicShore.Tests`, stand-ins fixed to
Unity's real shape). A branch that breaks the engine build is fixed there, not here.

**§2.5 is NOT one of these.** The tool-output gate is a git and filesystem question — did the
WRITER tool's assets land in a commit — and it needs no compiler, no editor and no CI. It
runs in full, in every mode.
