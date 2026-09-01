#!/usr/bin/env python3
"""Author the reward system's asset set: script/folder .meta files plus the two
Resources assets the runtime loads.

The reward system is reached from code with no inspector wiring - `RewardService`
resolves `Resources/Channels/RewardGrantedChannel` and `RewardTableSO` resolves
`Resources/RewardTable` - so those two assets ARE the wiring, and they have to exist
before anything can display or pay a reward.

GUIDs are md5 of a stable key rather than uuid4, so a re-run reproduces the same
files byte for byte and `--check` compares CONTENT instead of identity.

    python3 Tools/Build/author_reward_assets.py            # write
    python3 Tools/Build/author_reward_assets.py --check    # verify, non-zero on drift
"""
import hashlib
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
NS = "cosmic-shore/reward-system"


def guid(key: str) -> str:
    return hashlib.md5(f"{NS}/{key}".encode()).hexdigest()


SCRIPTS = [
    "_Scripts/Data/Enums/RewardKind.cs",
    "_Scripts/Data/Structs/RewardGrant.cs",
    "_Scripts/Data/Structs/RewardGranted.cs",
    "_Scripts/ScriptableObjects/RewardTableSO.cs",
    "_Scripts/ScriptableObjects/SOAP/ScriptableRewardGrant/ScriptableEventRewardGranted.cs",
    "_Scripts/ScriptableObjects/SOAP/ScriptableRewardGrant/EventListenerRewardGranted.cs",
    "_Scripts/System/Rewards/RewardService.cs",
    "_Scripts/UI/Elements/RewardPayoutPanel.cs",
    "_Scripts/UI/Elements/RewardToastDriver.cs",
    "_Scripts/Editor/Rewards/RewardDisplayWirer.cs",
]

# A directory under Assets/ is itself an asset. Without a committed .meta Unity mints
# one per machine and the folder shows as an untracked change forever.
FOLDERS = [
    "_Scripts/ScriptableObjects/SOAP/ScriptableRewardGrant",
    "_Scripts/System/Rewards",
    "_Scripts/Editor/Rewards",
]

SCRIPT_META = "fileFormatVersion: 2\nguid: {guid}\n"

FOLDER_META = (
    "fileFormatVersion: 2\n"
    "guid: {guid}\n"
    "folderAsset: yes\n"
    "DefaultImporter:\n"
    "  externalObjects: {{}}\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n"
)

ASSET_META = (
    "fileFormatVersion: 2\n"
    "guid: {guid}\n"
    "NativeFormatImporter:\n"
    "  externalObjects: {{}}\n"
    "  mainObjectFileID: 11400000\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n"
)

ASSET_HEAD = (
    "%YAML 1.1\n"
    "%TAG !u! tag:unity3d.com,2011:\n"
    "--- !u!114 &11400000\n"
    "MonoBehaviour:\n"
    "  m_ObjectHideFlags: 0\n"
    "  m_CorrespondingSourceObject: {{fileID: 0}}\n"
    "  m_PrefabInstance: {{fileID: 0}}\n"
    "  m_PrefabAsset: {{fileID: 0}}\n"
    "  m_GameObject: {{fileID: 0}}\n"
    "  m_Enabled: 1\n"
    "  m_EditorHideFlags: 0\n"
    "  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
    "  m_Name: {name}\n"
    "  m_EditorClassIdentifier: \n"
)

# Payout numbers: Docs/ECONOMY_TABLES.md Table 2. Unchanged by this pass - the table
# only MOVED, out of Scoreboard's serialized field and nine scene copies.
REWARD_TABLE_BODY = (
    # Unity serializes a List<int> as a hex blob of little-endian 4-byte ints with no
    # count prefix: c8000000=200, 32000000=50, 00000000=0.
    "  placementCrystals: c80000003200000000000000\n"
    "  lastPlaceAlwaysEarnsNothing: 1\n"
)

# Donor-cloned from Resources/Channels/GameToastChannel.asset: the three
# ScriptableEventBase keys, then _debugValue laid out as Unity serializes the
# RewardGranted struct (nested RewardGrant first, then the two balances).
CHANNEL_BODY = (
    "  CategoryIndex: 0\n"
    "  Description: \n"
    "  _debugLogEnabled: 0\n"
    "  _debugValue:\n"
    "    Grant:\n"
    "      Kind: 0\n"
    "      Amount: 0\n"
    "      EntitlementId: \n"
    "      Source: \n"
    "      Dedupe: 0\n"
    "      DedupeKey: \n"
    "    PreviousCrystalBalance: 0\n"
    "    NewCrystalBalance: 0\n"
)

ASSETS_TO_WRITE = [
    ("Resources/RewardTable.asset", "RewardTableSO",
     "_Scripts/ScriptableObjects/RewardTableSO.cs", "RewardTable", REWARD_TABLE_BODY),
    ("Resources/Channels/RewardGrantedChannel.asset", "RewardGrantedChannel",
     "_Scripts/ScriptableObjects/SOAP/ScriptableRewardGrant/ScriptableEventRewardGranted.cs",
     "RewardGrantedChannel", CHANNEL_BODY),
]


def planned():
    """Every (path, content) pair this generator owns."""
    out = []
    for rel in SCRIPTS:
        out.append((ASSETS / (rel + ".meta"), SCRIPT_META.format(guid=guid(rel))))
    for rel in FOLDERS:
        out.append((ASSETS / (rel + ".meta"), FOLDER_META.format(guid=guid(rel))))
    for rel, key, script_rel, name, body in ASSETS_TO_WRITE:
        out.append((ASSETS / rel,
                    ASSET_HEAD.format(script_guid=guid(script_rel), name=name) + body))
        out.append((ASSETS / (rel + ".meta"), ASSET_META.format(guid=guid(key))))
    return out


def assert_guids_unique(plan):
    """Exactly one .meta may OWN a guid. Every other hit is a reference and is
    evidence the wiring worked, so only .meta files are swept."""
    mine = {}
    for path, content in plan:
        if path.suffix != ".meta":
            continue
        m = re.search(r"^guid: ([0-9a-f]{32})$", content, re.M)
        mine[m.group(1)] = path

    dupes = []
    for meta in ASSETS.rglob("*.meta"):
        if meta in {p for p, _ in plan}:
            continue
        try:
            head = meta.read_text(encoding="utf-8", errors="ignore")[:200]
        except OSError:
            continue
        m = re.search(r"^guid: ([0-9a-f]{32})$", head, re.M)
        if m and m.group(1) in mine:
            dupes.append(f"{m.group(1)} already owned by {meta.relative_to(ROOT)}")
    if dupes:
        sys.exit("GUID COLLISION:\n  " + "\n  ".join(dupes))


def main():
    check = "--check" in sys.argv
    plan = planned()
    assert_guids_unique(plan)

    drift = []
    for path, content in plan:
        current = path.read_text(encoding="utf-8") if path.exists() else None
        if current == content:
            continue
        drift.append(path.relative_to(ROOT))
        if not check:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8")

    if check:
        if drift:
            print("DRIFT (re-run without --check):")
            for d in drift:
                print(f"  {d}")
            sys.exit(1)
        print(f"OK - {len(plan)} reward assets match.")
        return

    print(f"Wrote {len(drift)} of {len(plan)} reward assets.")
    for d in drift:
        print(f"  {d}")


if __name__ == "__main__":
    main()
