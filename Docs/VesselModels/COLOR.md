# Vessel colour: the language, the scheme, the cards

One page for how a vessel is coloured: in flight, in the hangar, on its card and in an advertisement. The numbers
live in `Tools/Build/vessel_model/vessel_colors.py` (the scheme) and `OriginalColorSetSO.asset` (the domains). The
game and the card renderer (`render_cards.py`) both read them, so a card is the ship the player flies.

## 1. The colour language (Garrett, 2026-10-18)

Each hue family has a name and one meaning. Saturation and value inside a family are free, to tell things apart
within the same use.

| family | meaning | on a vessel |
|---|---|---|
| **Blue** | the NEUTRAL family; grey (desaturated or valued down) belongs to it alone | the body and the glass |
| **Fire** (red, a little orange) | DANGER: bad, avoid, address | the jet exits, subtle at rest |
| **Lime** | CALL TO ACTION only: a categorically good thing to collect or click | never |
| **Ruby / Gold / Jade** | the three DOMAINS: teams, the cultures of freedom, commerce and order | the domain slot, in the pilot's team colour |

So a vessel wears four things: a domain accent, a neutral body, neutral glass (told from the body by saturation and
value), and a subtle Fire at its jet exits.

## 2. How the shader draws a slot

`VesselGraph.shadergraph` (URP, unlit) computes `colour = lerp(_Color1, _Color2, facing) × _ColorMultiplier`.
Here `facing` is the view-space normal's z: 1 where the surface faces the camera, 0 at the silhouette.

- `_Color1` is the **rim**: the outline the ship draws against the sky.
- `_Color2` is the **face**: the colour the camera sees head-on.

The result then passes through `VesselVisionShade`, the distance-gated cel band in `Docs/VESSEL_VISION.md`, which
is off at close range. Colours are stored as sRGB, and the project renders in linear space.

The engine slot's multiplier is 1 at rest. `VesselAnimation.FlareEngine` sets it to 5 under boost on
`materials[3]`, and `StopFlareEngine` sets it back to 1. `FlareBody` drives `materials[0]`, the domain slot, the
same way.

The body (`BlueBaseVesselMaterial`) is the one TRANSPARENT slot. Its alpha is a distance fade:
`clamp(distance / 2.5 − 2.5)`. It also writes no depth. Its surface mode was left as it was (see §6).

## 3. The scheme (sRGB, as the materials store it)

| slot | material | rim (`_Color1`) | face (`_Color2`) | mult | family, hue, why |
|---|---|---|---|---|---|
| Body | `BlueBaseVesselMaterial` and its opaque twin `BlueBaseOpaqueVesselMaterial` (the Squirrel's body; also the SkyBurst projectile) | (0.18, 0.21, 0.33) | (0.028, 0.033, 0.065) | 1 | Blue, 228–232°, low saturation, very low value. A dark gunmetal whose cool steel rim draws the silhouette against the sky (0, 0, 0.102), so the domain and the glass carry the colour. |
| Window | `ScreenVesselMaterial` | (0.70, 0.80, 1.00) | (0.06, 0.16, 0.62) | 1 | Blue, 220–229°, saturated and brighter. Sapphire glass with an ice glint at its edge, so it reads as glass and not as body. |
| Engine | `EngineVesselMaterial` | (0.42, 0.11, 0.04) | (0.19, 0.04, 0.02) | 1 (×5 boost) | Fire, 7–11°, low value. An ember at rest that you notice if you look, not one that asks for attention; FlareEngine's ×5 makes it a hot danger signal under thrust. |
| Domain | the team's ShipMaterial | the palette's `ShipColor1` | `ShipColor2` | 1 | Ruby, Gold or Jade, set by ThemeManager. The UI reads the same colours, so they are never edited for the vessel's sake. |

**The value hierarchy** at the chase camera, brightest first:

1. the domain plates;
2. the glass;
3. the body's silhouette rim;
4. the body's face and the resting ember.

Team first, then cockpit, then the shape, all against a dark ship that sits well in the dark sky.

**What it replaced** (bleeding-edge until this change):

- the body was a navy rim (0, 0, 0.16) on a black face, so it vanished against the sky;
- the glass was a grey-lavender (0.47, 0.47, 0.71) on (0, 0, 0.5), close to the body in hue and saturation;
- the engine was dark rust (0.094, 0.025, 0.003) / (0.35, 0.12, 0.09) at a RESTING multiplier of 5: an orange
  glow that asked for attention all the time.

`python3 Tools/Build/vessel_model/vessel_colors.py --apply` writes the scheme into the three materials, and
`--check` fails when any of them drifts from it.

### Slots that broke the language, fixed in the same change

| hull | was | now |
|---|---|---|
| Manta (and Termite, Falcon, which reuse its mesh) | the renderer's array `[BlueBase, Screen, GreenAccent, Lime]` was one slot out against the FBX order (`accent, BASE, windsheild, N`). The body drew as glass, the canopy as Jade, the boost stripes as **Lime**, and the domain reached nothing (`_shipGeometries` named only bones) | `[GreenAccent, BlueBase, Screen, Engine]` in FBX order, and the live `manta` renderer added to `_shipGeometries` and painted by identity (`_domainReplacesMaterials = [GreenAccent]`), so the eye-spots carry the team, the stripes are the ember, and `FlareBody`/`FlareEngine` light the right slots |
| `mantis_shapekey_with_animations.fbx.meta` (the inactive copy) | `N` → Lime | `N` → Engine |
| Rhino (`rhino_shapekey_with_animations.fbx.meta`) | `BASE.005` → domain (80 % of the hull was team colour), `accent.005` → body, `N.005` (the nozzles) → window glass | `BASE.005` → body, `accent.005` → domain (the artist's neon stripes), `N.005` → Engine |

Not changed:

- **Grizzly's jet renderers still wear `LimeVesselMaterial`.** The Grizzly is jtgjones's, in flight on
  `grizzly-v3`, so this is a team task, not an edit.
- **Serpent's Lime** sits on the inactive `serpent.fbx` vestige, which nothing draws.
- **Dolphin, Sparrow and Serpent** have no engine slot (three materials), so they show no ember until their
  models gain one.

## 4. Cards, hangar previews, advertisements

Every playable hull's card images are rendered from its model by `render_cards.py`, which emulates VesselGraph's
fresnel in Cycles: `lerp(rim, face, |N·V|)`, emission only, linear colours times the multiplier. They use the same
scheme. This covers 14 hulls:

- the seven artist hulls: Squirrel, Dolphin, Sparrow, Serpent, Manta, Rhino, Urchin;
- the seven new ones: Butterfly, Scarab, Thresher, Stoat, Angler, BrittleStar, Shrike.

The artist hulls are rendered IN PLACE over the files their class assets already name, at those files' sizes, so
every other UI reference keeps working. Examples: `Dolphin_Square.png` has five other users, `Hangar/Urchin.png` is
1620×920, and the Termite's silhouette is `Manta_Square_silhouette.png`.

**The domain slot on a card wears the CLASS livery:** `vessel_colors.CLASS_LIVERY`, rim (0.86, 0.90, 1.00) and
face (0.50, 0.56, 0.70). These are light, low-saturation Blue plates. A card sprite is static and shared by every
player, so Ruby, Gold or Jade on it would claim a team. The hand-drawn cards were gold on every hull, which is
commerce on everyone's card, with lime engines. The silver plates say "your team's colour goes here" and stay
inside the neutral family. The hangar preview's glow moved from gold (255, 175, 20) to cool silver
(150, 175, 230) for the same reason.

**Advertisements** that show a team should render in that team's colours:

```
render_cards.py <vessel> --compare out.jpg --domains Jade,Ruby,Gold
```

This draws each domain as the chase camera sees it against the arena sky, and as a card. Add `--boost` for the
engines under thrust.

## 5. The comparison sheets this was chosen on

`sheets/colour_before_after.jpg` shows Squirrel, Rhino and Thresher in each domain:

- each column is the scheme next to the bleeding-edge colours it replaced;
- the top tile is the chase view against the sky, the bottom tile is the card.

`sheets/colour_boost.jpg` shows the same pairing with the engines at boost.

The "before" columns use today's slot wiring with the old colours. So the Rhino's "before" shows its new
accent/body swap, not its old hull-wide domain.

Three candidates were tried before this one, all on the same sheets:

| candidate | body | outcome |
|---|---|---|
| v1 | slate (0.30, 0.34, 0.47) / (0.055, 0.065, 0.11) | read as flat grey and drowned Ruby's dark violet |
| B, chosen | dark gunmetal | keeps every domain the brightest thing on the hull |
| the old navy | navy rim on black | gave no silhouette at all |

Re-render the sheets with:

```
render_cards.py Squirrel Rhino Thresher --compare out.jpg --candidates before.json
```

`before.json` holds the old values in the candidates format, `{label: {role: {rim, face, mult}}}`.

## 5b. Lime's four shades: the Wrasse's lasso elements (Garrett, 2026-10-21)

> "make all four of the lasso elements different hues, saturations and values within the lime green color family
> (lime). make a 3d boundary that defines the space as distinct from jade, ruby, gold, blue (neutral), and fire (danger).
> form the largest tetrahedron within that space that makes four distinct values within that boundary and assign them
> to each element."

`Tools/Build/kite_mote_colors.py` derives them from the live palette. It writes them into `KiteNavigatorConfig.asset`
(`elementColors`) and the Wrasse Lasso Lab (`LIME_E`), and `--check` fails if either drifts.

- **The space** is OKLab: L is the value, and the angle and radius of (a, b) are the hue and the saturation. A distance
  there is a visible difference; about 0.02 is just noticeable.
- **The boundary** contains every sRGB colour at least 0.05 nearer a lime anchor than any anchor of another family. The
  anchors are what the game already draws:
  - Lime: the CTA signal, BrightCTA / the HUD's lime, and the kite's lime.
  - Jade, Ruby, Gold: each domain's UI and signal colours.
  - Blue: the Blue domain, the HUD's blue, the hull body and glass, and every grey (this page's rule 1: grey is Blue's).
  - Fire: the danger signal, the HUD's fire, and the engine ember.

  The region is small. Its value runs 0.73–0.97, its chroma 0.14–0.29 and its hue 104–154°. Jade's teal takes the dark
  greens (the "dark olive is not lime" finding, `Docs/PALETTE.md` §2.5), and the grey axis takes the pale ones.
- **Green-yellow, round 2** (Garrett, 2026-10-21, after the first cut: "pull time [and] space away from jade and charge
  away from gold" and "it has to have green in the yellow and yellow in the green. it can not have any blue or orange at
  all"). The boundary keeps only red + green:
  - blue is 0;
  - green leads red (no orange);
  - red is a quarter to three quarters of green, an HSV hue of 75–105°: a quarter of the way in from pure yellow and from
    pure green (`GREEN_YELLOW_HUES`).

  With blue at 0, lime is a curved sheet of hue × value in OKLab, not a volume. Saturation (chroma) still differs from
  corner to corner, but only as hue and value carry it.
- **The tetrahedron** has the largest OKLab volume among those whose four corners differ pairwise in value, in saturation
  and in hue by the largest of 0.03 / 0.025 / 0.02 that fits. On the sheet that is 0.02, one just-noticeable difference,
  because value, saturation and hue move together there. The tetrahedron is nearly flat (volume 5e-9): its corners are the
  sheet's four most spread colours. Without the distinctness rule, the first cut put two corners at one hue (#00ff00 and
  #a5ff95).
- **The assignment** reads each corner: Mass is the lowest value (weight), Charge the highest (the spark), and of the
  other two Space is the cooler hue (toward green) and Time the warmer (toward yellow).

| element | colour | sRGB | red / green | HSV hue | L (value) | chroma | nearest other family, beyond lime |
|---|---|---|---|---|---|---|---|
| Charge | `#a0ff00` | (0.626, 1, 0) | 0.63 | 82° | 0.905 | 0.250 | Jade +0.19 (Gold further) |
| Mass | `#5ec600` | (0.368, 0.776, 0) | 0.47 | 92° | 0.735 | 0.220 | Jade +0.05 |
| Space | `#40ff00` | (0.25, 1, 0) | 0.25 | 105° | 0.872 | 0.287 | Jade +0.16 |
| Time | `#9bcf00` | (0.609, 0.813, 0) | 0.75 | 75° | 0.789 | 0.200 | Gold +0.10 (Jade further) |

The first cut (`#ffff00`, `#a1c417`, `#00ff00`, `#c0fb93`) had pure yellow, pure green and a blue-tinted mint. The window
moves Charge off Gold (its gap to Gold went from 0.11 to over 0.19) and puts yellow into Space (red 0 → a quarter of
green).

The motes in the kite view and the pilot's faint mote dots wear them. The kite's tail, the saved-boost dots and every
other call to action keep the palette's one lime. Mass sits on the boundary's Jade edge (+0.05): the dark green-yellows
are the ones nearest Jade's teal. Narrowing the window further squeezes the four together (at 78–102°, under 0.02 apart).

## 6. Open: decisions and checks owed

- **Jade's and Ruby's hues: deferred** (Garrett, 2026-10-18: "we'll address the jade and ruby hues later").
  The observation stands. `JadeColors.ShipColor2` is (0, 0.388, 0.749), hue **209°**, an azure, about 20° from the
  glass (220–229°) and the body (228–232°), so Jade is the one domain that can be read as "neutral". The
  palette's other Jade entries sit at about 177–182° (teal), which is one option. The UI (`MultiplayerHUD`,
  `DomainScorePanel`) reads the same colours, so this is a palette change.
- **Gold's rim and face were swapped: fixed 2026-10-18.** Gold carried a bright `ShipColor1`
  (1.22, 0.61, 0.03) over a dark `ShipColor2` (0.47, 0.24, 0.04), the reverse of Ruby and Jade. Gold ships
  therefore glowed at the edge and darkened face-on. The HUD chip was inverted too: `DomainScorePanel` reads
  `ShipColor1` as "the muted hull tone" for its backdrop and `ShipColor2` as "the bright pop". The two values are
  swapped in `OriginalColorSetSO.GoldColors`, with Gold's own numbers kept. The unused Gold materials
  (`YellowAccentVesselMaterial`, `YellowVesselMaterial`) carried the same reversal and are swapped too.
- **An Editor look is owed** (`/verify-unity` was not available in the session that made this change). Fly each
  domain at the chase camera and in the hangar. Then:
  - Watch the body for self-overlap ghosting. It is transparent with no depth write, so its own parts draw in
    index order. A near-black body hid that, and the steel rim may show it. If it shows, put the hull on
    `BlueBaseOpaqueVesselMaterial` (the Squirrel's body already wears it; same colours), or give the shared body
    the opaque surface settings.
  - Check that the Manta, Termite, Falcon and Rhino carry their team colour on the accent and the ember on their
    engines.
