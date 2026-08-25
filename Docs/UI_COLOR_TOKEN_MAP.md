# UI colour-literal → `UIThemeSO` token map

**Status:** audit, no call sites changed · **Scope:** `Assets/_Scripts/UI/**/*.cs`
**Derives from:** `Docs/STYLE_FOUNDATION.md` §10 (field map), §2 (colour), §3 (team-colour contract), §7 (interactive states)
**Companion to:** `Docs/PALETTE.md`, `Docs/UI_ARCHITECTURE_AUDIT.md` §1.4 / §5.4

---

## 1. What shipped alongside this report

| File | What |
|---|---|
| `Assets/_Scripts/UI/UIThemeSO.cs` | The SO. **25 serialized fields and nothing else** — no constants, no helpers, no accessors — in §10's document order, with §10's values as hardcoded defaults. |
| `Assets/_Scripts/UI/UIThemeHelper.cs` | Read access and the fallbacks. Everything that is not a token lives here. |
| `Assets/_SO_Assets/UI/UITheme.asset` | An authored instance, sibling to `HUDAnimationSettings.asset`. |
| `Assets/_Scripts/UI/Scoreboard.cs` | One `[SerializeField] UIThemeSO uiTheme` — the reference, nothing restyled. |

Pattern follows `HUDAnimationSettingsSO`: a plain `ScriptableObject` of public fields with
`[Header]`/`[Tooltip]`, referenced per-consumer, degrading gracefully when unassigned.

**The type carries no members but its 25 fields**, so "authored to §10 verbatim" is a claim a
script can check against the document's table rather than a judgement call. Read access lives in
`UIThemeHelper` as extension methods — safe on an unassigned reference, since an extension method
may be invoked on a null `this`:

- **`theme.Resolve()`** — the asset, or a hidden defaults instance. Lets a call site read
  `theme.Resolve().textBody` instead of restating a hex, which is how `HUDAnimationSettingsSO`'s
  fallbacks ended up duplicated inline in `ScoreNumberAnimator` (lines 131–132 below — the same
  two colours written twice).
- **`theme.Spacing(step)`** — 1-based, so `Spacing(4)` is `s4`. Falls back to the shipped scale,
  with a one-shot warning per asset, if the serialized array is resized in the inspector. A silent
  fallback there would be indistinguishable from a theme that never applied.
- **`theme.StaggerFor(index)`** — `min(index, staggerCap) * staggerStep`. §8's cap is only
  meaningful applied together with the step; split across two raw fields it is the exact mistake
  the hangar grid already makes (80 ms across an unbounded list).

The one consumer is `Scoreboard`, which sits on `GameCanvas.prefab` — so the eventual inspector
assignment is a single prefab edit reaching every game mode, and adds no scene override. Nothing
is restyled: the field is declared and unread.

**Team colours are not in the asset.** Jade/Ruby/Gold stay in `SO_ColorSet`, reached through
`GetDomainUIColor` / `GetDomainSignalColor` / `GetDomainUIAccentColor`.

Verified: compiles clean (0 errors, 0 warnings) against Unity-shaped stubs; field names, types and
declaration order are an exact match against `Docs/STYLE_FOUNDATION.md` §10's table; all 14 colour
fields, the 9 spacing steps, the clamp behaviour and the stagger cap assert against §10's literal
values (25/25 pass, including the null-reference path); the `.asset` YAML round-trips to the same
14 hex values. **Not** verified in the Editor — no Unity instance is reachable from this session,
so `/verify-unity` did not run, and the inspector assignment on `GameCanvas.prefab` is outstanding.

---

## 2. Census and how it reconciles with 165

The audit's figure is **165**; a reproducible sweep for
`new Color(` · `new Color32(` · `Color.<named>` · `<color=` over `Assets/_Scripts/UI/**/*.cs`
returns **184 occurrences** across 53 files (181 lines — four lines hold more than one).

Both numbers describe the same population. The audit's own worst-offender counts are approximate
in the same direction (it cites `PrivacyConsentOverlay` at 14 against an actual 15, and
`SquirrelVesselHUDView` at "7–8" against 16 — the latter counting only the authored
`[SerializeField]` tints, not the `Color.white` field initialisers). The `new Color(` +
named-`Color.X` subset alone is **170**, which brackets 165 from the other side.

Rather than reverse-engineer the original grep, **every one of the 184 is classified below and the
buckets sum to 184.** Nothing is unaccounted for either way.

| Destination | Count | |
|---|---:|---|
| **`UIThemeSO`** | **58** | Chrome. The subject of this report. |
| `SO_ColorSet` | 22 | Team identity — correct by policy, mostly already routed |
| `ElementalBarsConfigSO` | 34 | Vessel-HUD gauge states — see §5 |
| `FrogletEditorPalette` | 24 | Four `FrogletTools` editor windows, not game UI |
| Not a colour | 19 | Alpha re-packs, `Color.clear`, captured-rest initialisers |
| Console rich text | 14 | `<color=#FFD700>` log traces — see §6 |
| **No home (gaps)** | **13** | **§4 — for discussion, not invention** |

---

## 3. Mapping — literals that land on a `UIThemeSO` field

58 occurrences. Grouped by the field they resolve to.


#### `surfaceVoid` · `#07090F` — scrims and modal backdrop (2)

| Site | Current | Reading |
|---|---|---|
| `MiniGameHUD.cs:119` | `skipImage.color = new Color(0f, 0f, 0f, 0.5f);` | skip-button scrim (alpha stays at the call site) |
| `Privacy/PrivacyConsentOverlay.cs:109` | `scrimImage.color = new Color(0.02f, 0.02f, 0.04f, 0.88f);` | modal scrim; #050A0A vs #07090F |


#### `surfaceHull` · `#0E131C` — default panel surface (2)

| Site | Current | Reading |
|---|---|---|
| `Views/HangarCaptainsView.cs:113` | `SelectedCaptainImage.color = Color.black;` | empty captain slot |
| `Views/PortSquadCaptainSelectionView.cs:14` | `[SerializeField] Color32 UnselectedRowColor = Color.black;` | unselected row |


#### `surfacePlate` · `#171E2A` — raised surface, card, button rest (3)

| Site | Current | Reading |
|---|---|---|
| `Elements/LoadoutCard.cs:20` | `[SerializeField] Color DeselectedColor = Color.white;` | card rest surface (§7 Rest) |
| `Privacy/PrivacyConsentOverlay.cs:256` | `bg.color = new Color(0.09f, 0.10f, 0.13f, 0.99f);` | panel body |
| `ToastNotification/ToastNotificationManager.cs:152` | `bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);` | toast body |


#### `surfaceRaise` · `#212B3A` — hover surface, active row (3)

| Site | Current | Reading |
|---|---|---|
| `Privacy/PrivacyConsentOverlay.cs:156` | `new Color(0.28f, 0.30f, 0.35f), Decline);` | secondary action fill |
| `Privacy/PrivacyConsentOverlay.cs:336` | `bg.color = new Color(0.16f, 0.17f, 0.21f);` | input field surface |
| `Views/PortSquadCaptainSelectionView.cs:13` | `[SerializeField] Color32 SelectedRowColor = Color.gray;` | selected row (§7 active row) |


#### `borderRule` · `#2A3444` — hairline border, divider (0)

Nothing in C# draws a divider today — borders are baked into per-prefab sprites. This token exists for §7's stroke work.

_No existing literal maps here — greenfield token._


#### `borderRuleHigh` · `#3D4A5E` — emphasised border (4)

All four are `ArcadeLoadoutView` resetting option borders to untinted white before re-tinting the selected one — the sprite-swap idiom §7 replaces with tint + stroke.

| Site | Current | Reading |
|---|---|---|
| `Views/ArcadeLoadoutView.cs:201` | `IntensityBorders[i].color = Color.white;` | state reset |
| `Views/ArcadeLoadoutView.cs:240` | `PlayerCountBorders[i].color = Color.white;` | state reset |
| `Views/ArcadeLoadoutView.cs:89` | `PlayerCountBorders[i].color = Color.white;` | option border state reset (§7) |
| `Views/ArcadeLoadoutView.cs:91` | `IntensityBorders[i].color = Color.white;` | option border state reset (§7) |


#### `textSignal` · `#E8EDF5` — headings, primary values (17)

The largest single group, and the one with the most behavioural change in it: §2 says **never pure white**, so every one of these gets perceptibly dimmer. Worth a look pass rather than a blind sweep.

| Site | Current | Reading |
|---|---|---|
| `Elements/DomainInfoData.cs:29` | `[SerializeField] private Color selectedTextColor = Color.white;` | selected label |
| `Elements/IconRotator.cs:25` | `Color.white,` | cycle stop 1 |
| `Elements/InputDeviceIconSetSwitcher.cs:73` | `public Color activeColor = Color.white;` | active device icon |
| `Elements/RequestInfoEntry.cs:29` | `[SerializeField] private Color friendRequestColor = Color.white;` | friend-request row label |
| `GameToastSystem/GameToastController.cs:147` | `? Color.white` | non-domain toast text |
| `MiniGameHUD.cs:137` | `tmpText.color = Color.white;` | skip label |
| `Privacy/PrivacyConsentOverlay.cs:295` | `t.color = new Color(0.94f, 0.95f, 0.97f);` | heading |
| `Privacy/PrivacyConsentOverlay.cs:348` | `text.color = new Color(0.94f, 0.95f, 0.97f);` | input text |
| `Privacy/PrivacyConsentOverlay.cs:383` | `tmp.color = Color.white;` | button label |
| `ToastNotification/ToastNotificationManager.cs:163` | `tmp.color = Color.white;` | toast text |
| `Views/ArcadeLoadoutView.cs:200` | `IntensityOptions[i].color = Color.white;` | state reset |
| `Views/ArcadeLoadoutView.cs:239` | `PlayerCountOptions[i].color = Color.white;` | state reset |
| `Views/ArcadeLoadoutView.cs:88` | `PlayerCountOptions[i].color = Color.white;` | option label state reset (§7) |
| `Views/ArcadeLoadoutView.cs:90` | `IntensityOptions[i].color = Color.white;` | option label state reset (§7) |
| `Views/HangarCaptainsView.cs:99` | `SelectedCaptainImage.color = Color.white;` | captain portrait untinted |
| `Views/HangarVesselDetailView.cs:197` | `unlockButtonText.color = Color.white;` | button label |
| `Views/HangarVesselDetailView.cs:202` | `unlockButtonText.color = Color.white;` | button label |


#### `textBody` · `#B9C4D2` — body copy, table rows (7)

| Site | Current | Reading |
|---|---|---|
| `Privacy/PrivacyConsentOverlay.cs:302` | `t.color = new Color(0.76f, 0.78f, 0.83f);` | body copy |
| `Screens/LeaderboardsMenu.cs:220` | `HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = C…` | other rows |
| `Screens/LeaderboardsMenu.cs:221` | `HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().color = C…` | other rows |
| `Screens/LeaderboardsMenu.cs:222` | `HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = C…` | other rows |
| `Views/DailyChallengeLeaderboardView.cs:100` | `HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = C…` | other rows |
| `Views/DailyChallengeLeaderboardView.cs:101` | `HighScoresContainer.transform.GetChild(i).GetChild(3).GetComponent<TMP_Text>().color = C…` | other rows |
| `Views/DailyChallengeLeaderboardView.cs:99` | `HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = C…` | other rows |


#### `textMuted` · `#7C8899` — labels, secondary, captions (3)

| Site | Current | Reading |
|---|---|---|
| `Elements/DomainInfoData.cs:30` | `[SerializeField] private Color unselectedTextColor = Color.gray;` | unselected label |
| `Elements/InputDeviceIconSetSwitcher.cs:74` | `public Color inactiveColor = new Color(0.65f, 0.65f, 0.7f, 1f);` | inactive device icon |
| `Privacy/PrivacyConsentOverlay.cs:309` | `t.color = new Color(0.60f, 0.63f, 0.70f);` | caption |


#### `textFaint` · `#4E5A6B` — disabled, placeholder (1)

| Site | Current | Reading |
|---|---|---|
| `Privacy/PrivacyConsentOverlay.cs:356` | `ph.color = new Color(0.45f, 0.47f, 0.53f);` | placeholder |


#### `systemAccent` · `#4FD5E8` — focus, selection, links, all pre-team UI (14)

Two independent teals already converged on roughly this hue — the leaderboard own-row `(.1,.7,.7)` in two views, and the consent overlay's `(0.16,0.62,0.52)` primary action. §2's cyan is the generalisation of what the codebase was already reaching for.

| Site | Current | Reading |
|---|---|---|
| `Elements/IconRotator.cs:26` | `new Color(0.25f, 0.95f, 1f),  // cyan` | cycle stop 2 (#40F2FF ~ sys) |
| `Elements/OnlineInfoEntry.cs:39` | `[SerializeField] private Color onlineColor = Color.white;` | presence: online |
| `Modals/GameSettingsPanelController.cs:53` | `[SerializeField] Color selectedColor = Color.white;` | selected setting (§7 Selected) |
| `Modals/HangarTrainingModal.cs:76` | `IntensityButtons[i].GetComponent<Image>().color = Color.green;` | selected intensity |
| `Privacy/PrivacyConsentOverlay.cs:138` | `new Color(0.16f, 0.62f, 0.52f), SubmitBirthYear);` | primary action fill |
| `Privacy/PrivacyConsentOverlay.cs:158` | `new Color(0.16f, 0.62f, 0.52f), Accept);` | primary action fill |
| `Privacy/PrivacyConsentOverlay.cs:407` | `tmp.color = new Color(0.35f, 0.72f, 0.95f);` | policy link |
| `ResourceDisplay.cs:39` | `[SerializeField] private Color sliderNormalColor = Color.white;` | filled resource track |
| `Screens/LeaderboardsMenu.cs:214` | `HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = n…` | own row highlight (teal) |
| `Screens/LeaderboardsMenu.cs:215` | `HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().color = n…` | own row highlight |
| `Screens/LeaderboardsMenu.cs:216` | `HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = n…` | own row highlight |
| `Views/DailyChallengeLeaderboardView.cs:93` | `HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().color = n…` | own row highlight (teal) |
| `Views/DailyChallengeLeaderboardView.cs:94` | `HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().color = n…` | own row highlight |
| `Views/DailyChallengeLeaderboardView.cs:95` | `HighScoresContainer.transform.GetChild(i).GetChild(3).GetComponent<TMP_Text>().color = n…` | own row highlight |


#### `systemDim` · `#2A8A99` — inactive tab, unfilled track (1)

| Site | Current | Reading |
|---|---|---|
| `Modals/GameSettingsPanelController.cs:54` | `[SerializeField] Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);` | unselected setting |


#### `attention` · `#A67CFF` — new / unclaimed / CTA badge (1)

One accidental match: `IconRotator`'s violet cycle stop is `#A673FF`, four units off the token. Nothing else in the UI is violet, so `attention` is effectively a new capability rather than a consolidation.

| Site | Current | Reading |
|---|---|---|
| `Elements/IconRotator.cs:28` | `new Color(0.65f, 0.45f, 1f),  // violet` | cycle stop 4 (#A673FF ~ attn) |


#### `danger` · `#FF5C3A` — destructive fill only (0)

**Zero call sites, and that is the finding.** §3 scopes `danger` to destructive *fills* — kick, leave, delete data. Every red currently in the UI is feedback or a gauge state, not a destructive confirm, so none of them may legally take this token. See §4.

_No existing literal maps here — greenfield token._


---

## 4. Gaps — literals with no home

13 occurrences that the §10 vocabulary cannot express. Listed for discussion; **no fields were
invented for them.**

Bucketed against T4's three categories:

| Bucket | Count | Which |
|---|---:|---|
| **(a) missing token** — the foundation should name it | 10 | negative/urgent ×4, positive ×2, affordability ×2, locked tint ×1, disabled alpha ×1 |
| **(b) belongs to a feature-level SO** — not chrome | 2 | gauge-at-full ×1, input-denied ×1 (both `ElementalBarsConfigSO`, alongside §5.2's 34) |
| **(c) never designed** — no intent behind it | 1 | the decorative magenta cycle stop |


**Negative / urgent feedback — 4 occurrences.** The biggest gap. §3 pins `danger` to destructive fill *only*, never a tint or a border, so score-loss text, the countdown-urgent tint, and the consent overlay's validation error have nowhere to go. Three of the four are already the *same colour* (`1, 0.3, 0.2` ≈ `#FF4D33`), a hair off `danger`'s `#FF5C3A` — the vocabulary is missing a word the codebase already has a value for. Options: a `warn` token distinct from `danger`; or rule that this is gameplay feedback and belongs in `ElementalBarsConfigSO` beside the existing fire/negative-debuff colour.


| Site | Current |
|---|---|
| `Elements/ScoreNumberAnimator.cs:132` | `: (_settings ? _settings.scoreLossColor : new Color(1f, 0.3f, 0.2f, 1f));` |
| `HUDAnimationSettingsSO.cs:36` | `public Color scoreLossColor = new Color(1f, 0.3f, 0.2f, 1f);` |
| `HUDAnimationSettingsSO.cs:46` | `public Color countdownUrgentColor = new Color(1f, 0.3f, 0.2f, 1f);` |
| `Privacy/PrivacyConsentOverlay.cs:133` | `_ageError.color = new Color(0.90f, 0.36f, 0.42f);` |


**Positive feedback — 2 occurrences.** Score-gain green, and its inline duplicate. §2 has no success hue, deliberately: green is Jade. A success token would collide with team identity exactly the way §1.2 says Ruby collides with danger — and §1.2's own answer is *form disambiguates before hue does*, which suggests score gain should be signalled by motion or a glyph, not a colour. Worth deciding rather than defaulting.


| Site | Current |
|---|---|
| `Elements/ScoreNumberAnimator.cs:131` | `? (_settings ? _settings.scoreGainColor : new Color(0.2f, 1f, 0.4f, 1f))` |
| `HUDAnimationSettingsSO.cs:34` | `public Color scoreGainColor = new Color(0.2f, 1f, 0.4f, 1f);` |


**Requirement met / unmet — 2 occurrences.** `HangarCaptainsView` injects a bare `"FFF"` / `"888"` hex into a rich-text template. Note these two are *additional* to the 184 — a bare 3-digit hex string matches no colour-literal pattern, which is a reminder the census floor is a floor. Maps cleanly onto `textSignal` / `textMuted` if that reading is accepted; flagged because "can I afford this" is arguably a status, not a text weight.


| Site | Current |
|---|---|
| `Views/HangarCaptainsView.cs:54` | `const string CrystalRequirementTemplate = "<color=#{2}>{0}</color> / {1}";` |
| `Views/HangarCaptainsView.cs:55` | `const string XPRequirementTemplate = "<color=#{2}>{0}</color> / {1} XP";` |


**Locked-card multiply tint — 1 occurrence.** `GameCard.lockedTintColor` is `(0.3,0.3,0.3)` applied multiplicatively over card art. §7 defines disabled as *transparent surface + `rule` border + `faint` text*, which describes a chrome control, not a dimmed image. Either add a `disabledTint` scalar, or rule that card art dims via `CanvasGroup` alpha.


| Site | Current |
|---|---|
| `Elements/GameCard.cs:31` | `[SerializeField] private Color lockedTintColor = new Color(0.3f, 0.3f, 0.3f, 1f);` |


**Disabled alpha — 1 occurrence.** `DomainInfoData` swaps `1.0`/`0.4` on `interactable`. §7 names no alpha for disabled. Related to the above; probably one decision covering both.


| Site | Current |
|---|---|
| `Elements/DomainInfoData.cs:56` | `backgroundImage.color = new Color(c.r, c.g, c.b, interactable ? 1f : 0.4f);` |


**Gauge-at-full — 1 occurrence.** `ResourceDisplay.sliderFullColor`. A track reaching full is information, not a destruction warning. Likely `ElementalBarsConfigSO` rather than a new chrome token.


| Site | Current |
|---|---|
| `ResourceDisplay.cs:40` | `[SerializeField] private Color sliderFullColor = Color.red;` |


**Input denied — 1 occurrence.** `SparrowHUDView.blockedInputColor`. Same family as the above: a HUD affordance saying "not now".


| Site | Current |
|---|---|
| `View/SparrowHUDView.cs:49` | `[SerializeField] private Color blockedInputColor = Color.red;` |


**Decorative cycle stop — 1 occurrence.** `IconRotator`'s magenta. The rotator blends white → cyan → magenta → violet as pure ornament; three of four stops land on or near tokens and magenta does not. §1.1 says a coloured pixel tells the player something, so this may simply be an element to retire rather than a token to add.


| Site | Current |
|---|---|
| `Elements/IconRotator.cs:27` | `new Color(1f, 0.30f, 0.85f),  // magenta` |

---

## 5. Not `UIThemeSO` — correct destinations

### 5.1 `SO_ColorSet` — team identity (22)

Policy-correct by §3, and most are already routed through the accessors; the literal is only the
no-theme-wired fallback. **Two need work:**

- `DomainVolumeHexGraphic.cs:84` hardcodes `{ Color.green, Color.red, Color.yellow }` as the three
  domain colours and never consults the palette at all. That is a live defect independent of any
  theming work — a domain re-colour silently misses this gauge.
- The `Color.white` / `Color.gray` fallbacks duplicate what the accessors already do internally
  (`GetDomainUIColor` returns gray, `GetDomainSignalColor` returns white for an unauthored domain).
  These should call the accessor and drop the local fallback rather than acquire a token.

`ObjectiveArrowGraphic`'s three lime constants are the §3 "objective arrow, owned crystals: YES,
full-saturation fill" row. Today they are fixed lime regardless of domain, so "existing behaviour —
keep" in §3 is describing an intent the code does not yet implement.


| Site | Current | Reading |
|---|---|---|
| `ConnectingPanelController.cs:142` | `sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(DomainColor(d))}>{d.ToString().ToUpper…` | domain hex in rich text |
| `ConnectingPanelController.cs:160` | `: Color.white;` | DomainColor no-theme fallback |
| `DomainVolumeHexGraphic.cs:84` ×3 | `readonly Color[] _domainColor = { Color.green, Color.red, Color.yellow };` | DEFECT: green/red/yellow hardcoded per domain, bypasses the palette |
| `DomainVolumeIndicator.cs:340` | `_jadeColor = _rubyColor = _goldColor = Color.white;` | reset to unresolved |
| `DomainVolumeIndicator.cs:352` | `return Color.white;` | ResolveDomainColor fallback |
| `DomainVolumeIndicator.cs:82` ×3 | `Color _jadeColor = Color.white, _rubyColor = Color.white, _goldColor = Color.white;` | per-domain fields, white until resolved |
| `GameToastSystem/GameToastAPI.cs:59` | `ColorSet != null ? ColorSet.GetDomainUIColor(domain) : Color.white;` | no-theme fallback |
| `GameToastSystem/GameToastController.cs:198` | `return $"<color=#{hex}><b>{playerName}</b></color>";` | domain hex in rich text |
| `GameToastSystem/GameToastController.cs:205` | `: Color.white;` | no-theme fallback |
| `MiniGameHUD.cs:572` | `: Color.gray;` | ResolveDomainColor no-theme fallback |
| `ObjectiveArrowGraphic.cs:26` | `[SerializeField] Color glowColor = new Color(0.31f, 0.50f, 0.18f, 0.32f);` | SF §3: objective arrow IS team-coloured; currently hardcoded lime |
| `ObjectiveArrowGraphic.cs:29` | `[SerializeField] Color outerColor = new Color(0.55f, 0.92f, 0.10f, 0.95f);` | same |
| `ObjectiveArrowGraphic.cs:32` | `[SerializeField] Color innerColor = new Color(0.86f, 1.00f, 0.62f, 1.00f);` | same |
| `Scoreboard.cs:592` | `: Color.gray;` | GetDomainColor no-theme fallback |
| `TournamentRoundCard.cs:67` | `Color DomainColor(Domains domain) => _colorOf != null ? _colorOf(domain) : Color.gray;` | unknown-domain fallback; GetDomainUIColor already returns gray |
| `TournamentRoundCard.cs:80` | `? $"WINNING DOMAIN : <color=#{ColorUtility.ToHtmlStringRGB(wc)}>{winner.ToString().ToUpp…` | domain hex injected into rich text |
| `View/DolphinVesselHUDView.cs:92` | `[SerializeField] private Color crystalTeamFallbackColor = Color.white;` | team-crystal fallback |
| `View/SquirrelVesselHUDView.cs:91` | `private Color _playerDomainColor = Color.white;` | player domain colour |


### 5.2 `ElementalBarsConfigSO` — vessel-HUD gauge states (34)

The largest non-chrome group: six vessel HUD views plus `MantaVesselHUDController`. These are
gauge states — cooling/ready, rest/armed, ammo normal/full, overheat, drift warm-vs-cool — not
chrome, so `UIThemeSO` is the wrong home by §10's own "chrome only" line.

`ElementalBarsConfigSO` is the better one, and the reasoning is already in the codebase: its
grey → white → blue → lime → fire ladder is *the HUD's existing words* for not-in-use → in-use →
overcharge → debuff. `MantaVesselHUDController._overchargeTextColor` is literally the overcharge
state hand-rolled in red beside a config that already defines overcharge as lime. `VesselHUDView.cs:288`
shows the destination shape — it already reads `config.whiteColor` with only a local fallback.

This is a recommendation, not a mapping: it needs the same sign-off the chrome tokens got.


| Site | Current | Reading |
|---|---|---|
| `Controller/MantaVesselHUDController.cs:22` | `readonly Color _overchargeTextColor = Color.red;` | overcharge is the petal ladder's own language |
| `View/DolphinVesselHUDView.cs:122` | `[SerializeField] private Color blastRestColor = Color.white;` | blast gauge rest |
| `View/DolphinVesselHUDView.cs:139` | `[SerializeField] private Color jawRestColor = Color.white;` | jaw gauge rest |
| `View/DolphinVesselHUDView.cs:169` | `Color _jawArmedColor = Color.white;` | jaw armed |
| `View/DolphinVesselHUDView.cs:94` | `[SerializeField] private Color crystalArmedFlashColor = Color.white;` | armed flash |
| `View/MantaVesselHUDView.cs:17` | `[SerializeField] private Color            normalColor   = Color.white;` | gauge rest |
| `View/MantaVesselHUDView.cs:18` | `[SerializeField] private Color            highlightColor = Color.yellow;` | gauge highlight |
| `View/RhinoVesselHUDView.cs:27` | `[SerializeField] private Color crystalDefaultColor = Color.white;` | gauge rest |
| `View/RhinoVesselHUDView.cs:28` | `[SerializeField] private Color crystalActivatedColor = Color.green;` | gauge active |
| `View/RhinoVesselHUDView.cs:29` | `[SerializeField] private Color lineDefaultColor = Color.white;` | gauge rest |
| `View/RhinoVesselHUDView.cs:30` | `[SerializeField] private Color lineActivatedColor = Color.red;` | gauge active |
| `View/RhinoVesselHUDView.cs:31` | `[SerializeField] private Color debuffDefaultColor = Color.white;` | gauge rest |
| `View/RhinoVesselHUDView.cs:32` | `[SerializeField] private Color debuffActiveColor = Color.cyan;` | debuff active |
| `View/SerpentVesselHUDView.cs:21` | `[SerializeField] private Color pipFullColor      = new Color(1f, 1f, 1f, 1f);` | pip full |
| `View/SerpentVesselHUDView.cs:22` | `[SerializeField] private Color pipConsumingColor = new Color(0.3f, 1f, 0.3f, 1f);` | pip consuming |
| `View/SerpentVesselHUDView.cs:23` | `[SerializeField] private Color pipEmptyColor     = new Color(1f, 1f, 1f, 0.25f);` | pip empty |
| `View/SquirrelVesselHUDView.cs:230` | `_targetBoostColor = Color.Lerp(_targetBoostColor, Color.white, fullBoostWhiteMix);` | full-boost white mix |
| `View/SquirrelVesselHUDView.cs:247` | `_currentBoostColor = Color.Lerp(_targetBoostColor, Color.white, flashT * 0.6f);` | boost flash mix |
| `View/SquirrelVesselHUDView.cs:283` | `? new Color(1f, 0.6f, 0.2f, 1f) // warm orange for double drift` | double-drift warm |
| `View/SquirrelVesselHUDView.cs:284` | `: new Color(0.7f, 0.9f, 1f, 1f); // cool blue for single drift` | single-drift cool |
| `View/SquirrelVesselHUDView.cs:42` | `[SerializeField] private Color impactRestColor = Color.white;` | impact gauge rest |
| `View/SquirrelVesselHUDView.cs:44` | `[SerializeField] private Color joustFlashColor = Color.red;` | joust flash |
| `View/SquirrelVesselHUDView.cs:46` | `[SerializeField] private Color crystalFlashColor = new Color(0.4f, 0.9f, 1f, 1f);` | crystal flash |
| `View/SquirrelVesselHUDView.cs:52` | `[SerializeField] private Color tubeCoolingColor = new Color(0.5f, 0.5f, 0.55f, 0.9f);` | tube cooling |
| `View/SquirrelVesselHUDView.cs:54` | `[SerializeField] private Color tubeReadyColor = new Color(1f, 0.2f, 0.2f, 1f);` | tube ready |
| `View/SquirrelVesselHUDView.cs:63` | `[SerializeField] private Color tubeSlamFlashColor = Color.white;` | tube slam flash |
| `View/SquirrelVesselHUDView.cs:71` | `[SerializeField] private Color overheatHotColor = new Color(1f, 0.45f, 0.15f, 1f);` | overheat hot |
| `View/SquirrelVesselHUDView.cs:73` | `[SerializeField] private Color overheatFlashColor = new Color(1f, 0.9f, 0.6f, 1f);` | overheat flash |
| `View/SquirrelVesselHUDView.cs:92` | `private Color _currentBoostColor = Color.white;` | boost colour state |
| `View/SquirrelVesselHUDView.cs:93` | `private Color _targetBoostColor = Color.white;` | boost colour state |
| `View/UrchinVesselHUDView.cs:26` | `[SerializeField] Color ammoNormalColor = Color.white;` | ammo gauge rest |
| `View/UrchinVesselHUDView.cs:27` | `[SerializeField] Color ammoFullColor = Color.cyan;` | ammo gauge full |
| `View/UrchinVesselHUDView.cs:33` | `[SerializeField] Color ridingOnColor = Color.green;` | riding-on state |
| `View/VesselHUDView.cs:288` | `badge.color = config ? config.whiteColor : Color.white;` | already reads config.whiteColor; local fallback only |


### 5.3 Not a colour at all (19)

These match the census pattern but carry no authored colour. They re-pack an existing colour with
a new alpha, initialise a field that is immediately overwritten by a captured rest value, or set a
fully transparent raycast target. **They need no token and should not be swept.** They are also
the likeliest source of the 184-vs-165 delta.


| Site | Current | Reading |
|---|---|---|
| `DomainVolumeIndicator.cs:178` | `hostImage.color = new Color(0f, 0f, 0f, 0f);` | invisible raycast host |
| `Elements/GameCard.cs:34` | `private Color _originalBgColor = Color.white;` | initialiser for a captured rest colour |
| `Elements/IconRotator.cs:49` | `private Color _restColor = Color.white;` | initialiser for a captured rest colour |
| `Elements/NavLink.cs:128` | `activeImage.color = new Color(initialActiveColor.r, initialActiveColor.g, initialActiveC…` | alpha crossfade re-pack |
| `Elements/NavLink.cs:129` | `inactiveImage.color = new Color(initialInactiveColor.r, initialInactiveColor.g, initialI…` | alpha crossfade re-pack |
| `Elements/OnlineInfoEntry.cs:48` | `[SerializeField] private Color defaultTint = Color.white;` | untinted sprite default |
| `Elements/QuestItemCard.cs:42` | `private Color _originalBgColor = Color.white;` | initialiser for a captured rest colour |
| `FX/Pulse.cs:22` | `Color newColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);` | alpha re-pack |
| `Modals/ModalWindowManager.cs:99` | `image.color = Color.clear;` | Color.clear raycast blocker |
| `Privacy/PrivacyConsentOverlay.cs:400` | `image.color = new Color(0f, 0f, 0f, 0f); // click target only` | invisible click target |
| `ThumbPerimeter.cs:23` | `Color color = Color.white;` | tint holder driven by alpha only |
| `VesselButtonPanel.cs:58` | `buttonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);` | re-packs an existing colour with a new alpha |
| `VesselButtonPanel.cs:61` | `buttonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetA…` | same |
| `View/ControllerButtonIconReferences.cs:41` | `_img.color = new Color(1, 1, 1, 1 - t / half);` | alpha fade |
| `View/ControllerButtonIconReferences.cs:48` | `_img.color = new Color(1, 1, 1, t / half);` | alpha fade |
| `View/ControllerButtonIconReferences.cs:51` | `_img.color = Color.white;` | untint reset |
| `View/SparrowHUDView.cs:202` | `img.color = Color.white;` | untint reset |
| `View/SparrowHUDView.cs:75` | `h.image.color = Color.white;` | untint reset |
| `View/SquirrelVesselHUDView.cs:115` | `private Color _overheatIconOriginalColor = Color.white;` | captured rest initialiser |


### 5.4 Console rich text (14) and editor chrome (24)

`<color=#FFD700>` FLOW traces, `DebugExtensions.LogColored` calls, and four `FrogletTools` editor
windows. Neither is game UI; neither belongs in `UIThemeSO`. Editor windows have their own house
palette (`FrogletEditorPalette`, per `Docs/TOOLING.md`).

Separately worth noting against the project's own logging rule: the five `[FLOW-2]` traces in
`ArcadeGameConfigureModal` and the `PartyInviteNotificationPanel` `[INVITE-UI]` traces are
bring-up telemetry of exactly the kind CLAUDE.md says should sit on a `CSLogChannel`. Three of them
already do; `Debug.LogError` at line 1320 and the three `LogColored` calls do not.

**Console rich text (14)**

| Site | Current | Reading |
|---|---|---|
| `Modals/ArcadeGameConfigureModal.cs:1232` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal…` | FLOW-2 trace |
| `Modals/ArcadeGameConfigureModal.cs:1288` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal…` | FLOW-2 trace |
| `Modals/ArcadeGameConfigureModal.cs:1305` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFD700>[FLOW-2] [ArcadeConfigModa…` | FLOW-2 trace |
| `Modals/ArcadeGameConfigureModal.cs:1320` | `Debug.LogError("<color=#FF0000>[FLOW-2] [ArcadeConfigModal] SyncAllGameDataForLaunch - g…` | FLOW-2 error trace |
| `Modals/ArcadeGameConfigureModal.cs:1335` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFD700>[FLOW-2] [ArcadeConfigModa…` | FLOW-2 trace |
| `Screens/PartyInviteNotificationPanel.cs:146` | `Color.green);` | LogColored |
| `Screens/PartyInviteNotificationPanel.cs:73` | `Color.magenta);` | LogColored |
| `Screens/PartyInviteNotificationPanel.cs:79` | `Color.red);` | LogErrorColored |
| `TestMiniGameEvents.cs:25` | `DebugExtensions.LogColored("OnMiniGameRoundStarted", Color.cyan);` | DebugExtensions.LogColored |
| `TestMiniGameEvents.cs:30` | `DebugExtensions.LogColored("OnMiniGameRoundEnd", Color.cyan);` | DebugExtensions.LogColored |
| `UniversalStatsProviderEditor.cs:413` | `CSDebug.Log($"<color=cyan><b>  STATS PREVIEW ({stats.Count} total)</b></color>");` | console rich text |
| `UniversalStatsProviderEditor.cs:419` | `CSDebug.Log($"<color=cyan>[{icon}] <b>{stat.Label}</b>: {stat.Value}</color>");` | console rich text |
| `WildlifeBlitzHUD.cs:57` | `CSDebug.Log($"<color=cyan>[WildlifeBlitzHUD] Target set to {targetScoreToWin}</color>");` | console rich text |
| `WildlifeBlitzHUD.cs:93` | `CSDebug.Log("<color=yellow>[WildlifeBlitzHUD] Round ended - clearing displays</color>");` | console rich text |

**Editor-window chrome (24)**

| Site | Current | Reading |
|---|---|---|
| `ActiveGameModesWindow.cs:160` | `GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);` | editor window |
| `ActiveGameModesWindow.cs:168` | `GUI.backgroundColor = new Color(0.8f, 0.5f, 0.5f);` | editor window |
| `LeaderboardConfigSOEditor.cs:145` | `GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);` | — |
| `LeaderboardConfigSOEditor.cs:151` | `GUI.backgroundColor = new Color(0.8f, 0.7f, 0.5f);` | — |
| `LeaderboardConfigSOEditor.cs:199` | `EditorGUI.DrawRect(dotRect, new Color(0.3f, 0.8f, 0.3f));` | — |
| `LeaderboardConfigSOEditor.cs:213` | `GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);` | — |
| `LeaderboardConfigSOEditor.cs:22` | `private static readonly Color headerColor = new Color(0.3f, 0.5f, 0.7f, 0.3f);` | — |
| `LeaderboardConfigSOEditor.cs:23` | `private static readonly Color activeGameModeColor = new Color(0.4f, 0.7f, 0.4f, 0.15f);` | — |
| `LeaderboardConfigSOEditor.cs:24` | `private static readonly Color inactiveGameModeColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);` | — |
| `LeaderboardConfigSOEditor.cs:25` | `private static readonly Color missingMappingColor = new Color(0.8f, 0.6f, 0.2f, 0.2f);` | — |
| `LeaderboardConfigSOEditor.cs:290` | `GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);` | — |
| `Model/MinigameHUDInspector.cs:10` | `Color headerColor = new Color(0.09f, 0.24f, 0.48f);` | — |
| `Model/MinigameHUDInspector.cs:11` | `Color sectionBlue = new Color(0.14f, 0.22f, 0.36f);` | — |
| `Model/MinigameHUDInspector.cs:12` | `Color sectionGreen = new Color(0.14f, 0.32f, 0.21f);` | — |
| `Model/MinigameHUDInspector.cs:23` | `MiniGameType.Freestyle => new Color(0.2f, 0.6f, 0.2f),// greenish` | — |
| `Model/MinigameHUDInspector.cs:24` | `MiniGameType.CellularDuel => new Color(0.6f, 0.2f, 0.6f),// purple` | — |
| `Model/MinigameHUDInspector.cs:77` | `normal = { textColor = Color.white },` | — |
| `Model/MinigameHUDInspector.cs:93` | `normal = { textColor = Color.white },` | — |
| `UniversalStatsProviderEditor.cs:17` | `private static readonly Color TrackerColor = new Color(0.4f, 0.6f, 0.9f);` | — |
| `UniversalStatsProviderEditor.cs:18` | `private static readonly Color SuccessColor = new Color(0.3f, 0.8f, 0.4f);` | — |
| `UniversalStatsProviderEditor.cs:19` | `private static readonly Color ErrorColor = new Color(0.9f, 0.35f, 0.35f);` | — |
| `UniversalStatsProviderEditor.cs:20` | `private static readonly Color ListHeaderColor = new Color(0.5f, 0.5f, 0.5f);` | — |
| `UniversalStatsProviderEditor.cs:303` | `miniLabelStyle.normal.textColor = Color.gray;` | — |
| `UniversalStatsProviderEditor.cs:317` | `previewStyle.normal.textColor = new Color(0.6f, 0.8f, 0.6f);` | — |

---

## 6. What section 10 does not touch

Eleven of the 25 fields are non-colour and therefore have **zero** overlap with the 184 literals:
`spacing[9]`, `chamferLarge`, `chamferSmall`, `hairline`, `stroke`, `durMicro`, `durStd`,
`durPanel`, `durCeremony`, `staggerStep`, `staggerCap`.

Their call sites are the *other* two piles `Docs/UI_ARCHITECTURE_AUDIT.md` §5.4 counts, which this report
did not enumerate:

- **~50 hardcoded `sizeDelta` / `anchoredPosition` writes** → `spacing`, `chamfer*`
- **Hardcoded durations outside any settings asset** — veil fades, connecting-dots interval, the
  replay 500 ms, toast durations, the 3 s invite auto-hide → `dur*`
- **The hangar grid's 80 ms unbounded stagger** → `staggerStep` / `staggerCap`, which is why
  `StaggerFor(index)` exists on the asset rather than two loose floats

Also outside the census by construction: colours authored on **prefabs**, not in C#. §7 replaces a
per-prefab `_pressed` / `_selected` / `_inactive` sprite-swap approach; those sprites carry colour
this sweep cannot see. The `borderRuleHigh` and several `textSignal` rows below are the C#-side
*shadow* of that idiom — code resetting a tint to white so a sprite shows through — so the real
interactive-state surface is larger than 58.

---

## 7. Suggested order of work

1. **Settle the gaps in §4** — particularly negative/positive feedback. Four literals are blocked
   on one decision, and it is a §3 contract question, not a colour-picking one.
2. **Fix the two `SO_ColorSet` defects** (`DomainVolumeHexGraphic`, `ObjectiveArrowGraphic`). They
   are wrong today, independent of theming.
3. **Sweep the unambiguous chrome** — `PrivacyConsentOverlay` (13 of its 15 map cleanly and it is
   entirely code-built, so it is the cleanest single-file proof of the asset), then the two
   leaderboard views, then the toast manager.
4. **Decide the `ElementalBarsConfigSO` question** before touching any vessel HUD view; 34
   occurrences hang on it.
5. **Then** the interactive-state work, which is where the non-colour fields earn their keep and
   where prefab sprites have to come along.

**Do not** sweep `Color.white` globally. 17 of the 64 are `textSignal`; the rest are untint resets,
domain fallbacks, gauge rests, or field initialisers. A blanket replace would dim raycast targets,
break domain fallbacks, and change gauge semantics.
