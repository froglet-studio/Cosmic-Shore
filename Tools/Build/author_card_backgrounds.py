#!/usr/bin/env python3
"""Wire each live arcade/arena card's BACKGROUND to that mode's own screenshot.

THE GAP THIS CLOSES
-------------------
`GameCard.UpdateCardView` does `BackgroundImage.sprite = game.CardBackground`, so a card's
backdrop is authored per mode on its own `SO_ArcadeGame` - and measured across the live
roster, 22 of the 25 cards share FOUR legacy images from the retired single-player era
(sixteen of them are all `GameCardBackground_Rampage.jpg`) and three have none at all. So
the grid tells the player almost nothing about which world a card leads to.

WHAT THIS TOOL DOES, AND WHAT IT DOES NOT
-----------------------------------------
It does NOT take the screenshots - that needs the running editor, and it is the one half of
this job that is genuinely play-testing rather than asset work. Capture them however suits
(the Screenshot Director, `Docs/SCREENSHOT_DIRECTOR.md`, is the shortest route: fly the mode
at intensity 2 and press 0), drop them in

    Assets/_Graphics/ARCADE/CardBackgrounds/<CardName>.png

and this tool does everything after that: writes each PNG's `.meta` as a Sprite with the
same import settings the legacy backgrounds carry, and rewires every matching card's
`CardBackground` to it. `<CardName>` is the card asset's own name with `ArcadeGame` stripped
(`ArcadeGameWreckingBall` -> `WreckingBall.png`); the report prints the exact filename each
card is waiting for, so there is nothing to guess. `.jpg` works too.

The ROSTER IS READ, never typed: the live cards are whatever `ArcadeGames.asset` and
`ArenaGames.asset` list, so a mode added to either is covered the day it is added and a mode
removed stops being asked for.

Usage:
  python3 Tools/Build/author_card_backgrounds.py           # import + rewire what is present
  python3 Tools/Build/author_card_backgrounds.py --check    # fail on an INCONSISTENCY
  python3 Tools/Build/author_card_backgrounds.py --strict    # ...and on incomplete coverage

`--check` deliberately passes while captures are merely MISSING - it reports that as coverage
- and fails only on something actually wrong: a card pointing at a capture that is not on
disk, a capture nothing points at, or a reference whose guid disagrees with the file's meta.
A gate that cannot be passed on the day it lands is noise, so the coverage half is behind
`--strict`, which is the flag to put in CI once the 25 captures exist.
"""

import re
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
assert (ROOT / "Assets").is_dir(), f"ROOT is wrong: {ROOT}"

ROSTERS = ["Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset",
           "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset"]
CAPTURES = "Assets/_Graphics/ARCADE/CardBackgrounds"
LEGACY_DIR = "Assets/_Graphics/Video/Minigames"
SPRITE_FILEID = "21300000"


def guid_index() -> dict:
    """guid -> asset path, for every .meta in Assets."""
    out = {}
    for meta in (ROOT / "Assets").rglob("*.meta"):
        try:
            head = meta.read_text(encoding="utf-8", errors="ignore")[:200]
        except OSError:
            continue
        g = re.search(r"^guid: (\w+)", head, re.M)
        if g:
            out[g.group(1)] = meta.with_suffix("")
    return out


def guid_of(asset: Path) -> str:
    meta = Path(str(asset) + ".meta")
    if not meta.exists():
        return ""
    g = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8", errors="ignore"), re.M)
    return g.group(1) if g else ""


def texture_meta(guid: str) -> str:
    """A Sprite importer meta matching what the shipped card backgrounds carry."""
    platforms = "".join(f"""  - serializedVersion: 3
    buildTarget: {target}
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: {fmt}
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: {over}
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
""" for target, fmt, over in [("DefaultTexturePlatform", -1, 0), ("Standalone", -1, 0),
                              ("Server", -1, 0), ("Android", 45, 1), ("iPhone", -1, 0)])
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  cookieLightType: 0
  platformSettings:
{platforms}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def roster_cards(index: dict) -> list:
    """[(card asset path, display name, mode)] over the LIVE rosters, in listed order."""
    seen, out = set(), []
    for rel in ROSTERS:
        text = (ROOT / rel).read_text(encoding="utf-8")
        for g in re.findall(r"guid: (\w+), type: 2\}", text):
            asset = index.get(g)
            if asset is None or asset in seen:
                continue
            body = asset.read_text(encoding="utf-8", errors="ignore")
            if "CardBackground:" not in body:
                continue
            seen.add(asset)
            dn = re.search(r"^  DisplayName: (.*)$", body, re.M)
            mode = re.search(r"^  Mode: (-?\d+)$", body, re.M)
            out.append((asset, (dn.group(1).strip() if dn else asset.stem),
                        mode.group(1) if mode else "?"))
    return out


def capture_for(card: Path) -> Path:
    """The capture this card is waiting for, whichever extension is on disk."""
    stem = re.sub(r"^ArcadeGame", "", card.stem)
    folder = ROOT / CAPTURES
    for ext in (".png", ".jpg", ".jpeg"):
        candidate = folder / (stem + ext)
        if candidate.exists():
            return candidate
    return folder / (stem + ".png")


def read_reference(body: str):
    m = re.search(r"^  CardBackground: \{fileID: (-?\d+)(?:, guid: (\w+))?", body, re.M)
    return (m.group(1), m.group(2)) if m else (None, None)


def main() -> int:
    check = "--check" in sys.argv
    strict = "--strict" in sys.argv
    index = guid_index()
    cards = roster_cards(index)
    if not cards:
        print("card-backgrounds: FAIL - read no cards off the live rosters")
        return 1

    folder = ROOT / CAPTURES
    problems, rows, wired, imported = [], [], 0, 0
    claimed = set()

    for card, display, mode in cards:
        body = card.read_text(encoding="utf-8")
        fid, guid = read_reference(body)
        current = index.get(guid) if guid else None
        capture = capture_for(card)
        want_rel = capture.relative_to(ROOT).as_posix()

        # A reference pointing at nothing on disk is broken whatever the coverage is.
        if guid and current is None:
            problems.append(f"{card.stem}: CardBackground guid {guid} resolves to no asset")

        if capture.exists():
            claimed.add(capture.resolve())
            cguid = guid_of(capture)
            if not cguid:
                cguid = uuid.uuid4().hex
                if not check:
                    Path(str(capture) + ".meta").write_text(texture_meta(cguid), encoding="utf-8")
                    imported += 1
                else:
                    problems.append(f"{want_rel}: no .meta - run the tool without --check")
            if guid == cguid:
                wired += 1
                rows.append(("OK  ", display, mode, want_rel))
            else:
                rows.append(("WIRE", display, mode, want_rel))
                if check:
                    problems.append(f"{card.stem}: capture present but CardBackground points "
                                    f"elsewhere ({want_rel})")
                else:
                    ref = f"  CardBackground: {{fileID: {SPRITE_FILEID}, guid: {cguid}, type: 3}}"
                    body = re.sub(r"^  CardBackground: .*$", ref, body, count=1, flags=re.M)
                    card.write_text(body, encoding="utf-8")
                    wired += 1
        else:
            shared = (current is not None
                      and current.relative_to(ROOT).as_posix().startswith(LEGACY_DIR))
            rows.append(("GAP " if shared or current is None else "own ",
                         display, mode,
                         f"wants {want_rel}"
                         + (f"  (now: {current.name})" if current is not None else "  (now: none)")))

    # A capture nothing is waiting for is a typo in a filename, which otherwise fails by
    # the card silently keeping its old art.
    if folder.is_dir():
        for f in sorted(folder.iterdir()):
            if f.suffix.lower() not in (".png", ".jpg", ".jpeg"):
                continue
            if f.resolve() not in claimed:
                problems.append(f"{f.relative_to(ROOT).as_posix()}: no live card is named for this "
                                f"file - check the spelling against the report above")

    width = max(len(r[1]) for r in rows)
    for state, display, mode, note in rows:
        print(f"  [{state}] {display:{width}s} mode={mode:>3s}  {note}")
    print(f"\n{wired}/{len(cards)} live cards wear their own screenshot"
          f"{f'; imported {imported} capture(s)' if imported else ''}")

    if problems:
        print("\ncard-backgrounds: FAIL")
        for p in problems:
            print("  -", p)
        return 1
    if strict and wired < len(cards):
        print(f"\ncard-backgrounds: FAIL (--strict) - {len(cards) - wired} card(s) still share a "
              f"legacy background")
        return 1
    print("card-backgrounds: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
