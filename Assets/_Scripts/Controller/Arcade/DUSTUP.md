# Dustup (`GameModes.Dustup = 59`)

The Butterfly's **CHARGE** game — a dust duel. The Butterfly carries no gun; its one weapon is the
**Scale Dust**, a capsule that hangs **below** the hull while it is in Dust mode. So a rival is hit
by flying **over** them. Every opposing pilot the dust passes through takes the Charge-scaled
all-element bite and pays one **dusting**; the first **DOMAIN** to the point target wins.

It is one of the Butterfly's four element games, one per element and one per genre petal
(`ModeGenre`): **Waystation** is Time (a race), **Tapestry** Mass (making mass),
**Sirocco** Space (destroying mass), and this is Charge (working other pilots over).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameDustup.unity` |
| Controller | `DustupController : MultiplayerDomainGamesController` |
| Metric | `ScoringMetric.CombatPoints` (8) — **reused** |
| Rule | `DustupScoringRuleSO` — a dusting (`CombatHitClass.Strike`) is 1 point, every other class 0 |
| Turn monitor | `DustupPointTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.dustupPointTarget` (**10**) |
| Comeback | rate **0.6** — a quarter of the race behind (2.5 dustings) buys 1.5 element levels |
| Arena | Dog Fight's **Boneyard**, referenced read-only (four intensity configs) |
| Objective arrow | `BendsObjectiveProvider` — the nearest pilot you may dust |
| Generator | `Tools/Build/author_dustup_assets.py` (`--check`) |

## The loop, in the Butterfly's own terms

1. **Flip to Dust mode** (right trigger). The wings fold, the wake narrows to a line, and a fall of
   motes in your domain's shielded colour appears **under** the hull — the capsule, 24 across and
   60 long at rest.
2. **Get above a rival.** Both of you turn at 45°/s. The duel is won by whoever finds the other
   first and gets on top — the Boneyard's wrecks are somewhere to lose a pursuer and somewhere to
   wait above a canyon.
3. **Pass through them.** One pass is one dusting. The dust drains their elements (−0.33 per element
   at rest, scaled by your **Charge** from 0.5× to 2×, doubled again at Charge 5 *Monarch*), so a
   dusted rival also flies worse for four seconds.
4. **Break off and come round again.** A skimmer runs its vessel effects on **entry**, so hovering
   over a rival scores the pass that caught them and nothing after it.

The other two elements matter too: **Space** lengthens the capsule (60 → 150), which is reach, and
**Time**'s Fold is the best way to get above someone who has not seen you yet. **Mass** mode still
exists — a wide wake laid across a canyon is a wall — but it switches the dust off.

## What the mode reuses, and the one thing it adds

**Nothing outside the mode is edited.** The dust container has carried a Strike-class
`VesselCombatHitBySkimmerEffectSO` (`ButterflyCombatHitBySkimmerEffect`, 1 s same-victim window,
`requireOwningMachine`) since the hull shipped, so every dusting was already **counted** in every
mode; `DustupScoringRuleSO` is the first rule that **pays** for one. The generator asserts both
halves of that assumption — the reporter is still Strike-class, and it is still in the dust
container — because if either moved, the scoreboard would sit at zero with nothing to say why.

**The one addition is `ButterflyAutopilotModeDriver`**, shared by all three new Butterfly games.
`AIPilot` writes a stick and a throttle and nothing else, and a Butterfly spawns in **Mass** mode,
so without it every AI would fly the whole match with its only weapon switched off — an all-AI
domain that cannot play (the Tollway rule). It presses the mode switch through
`PerformShipControllerActionsReplicated` (an AI is simulated server-only, and the mode is
simulated on every peer), finds the control by capability (`TryGetBoundAction<SpreadWingsActionSO>`),
and reads the mode **back** off `SpreadWingsActionExecutor.IsDustMode` rather than counting its own
presses — a toggle a driver counts drifts the first time a press is lost or doubled.

## AI

Every AI Butterfly **hunts**: it holds Dust mode for the whole turn and steers to a point **above**
an intercept on the nearest opposing pilot (humans preferred by `aiHumanFocus`), above along its
**own** up axis because the capsule hangs along its own down (`aiDustHover` 30 = half the resting
capsule). A hull flown *at* a rival meets them with its body and misses with its dust. With no rival
it flies to the arena centre. Steering is `SetExternalTargetProvider` and nothing else, cleared on
despawn, on the whistle and on replay.

## Numbers

| | value | why |
|---|---|---|
| target | 10 | ten clean passes — slow hulls, a far camera, and each pass needs a break-off and a turn |
| points per dusting | 1 | the only scoring event, so it needs no weighting |
| min players / domains | 2 / 2 | a **rule**: the dust spares teammates, so a solo lobby has nobody to score on |
| spawn ring | 40 outside, floor 700, Symmetric | Dog Fight's own — the Boneyard has no nucleus |
| `noNucleusSpawnRadius` | 420 | Dog Fight's — without it every omni crystal spawns at the exact centre |

## Verification status

**Authored headless; nothing has been opened in the editor.** What is proved: the generator's
`--check`, the standing out-of-editor gates, and the clone's diff against its donor (the identity,
the four AI hulls, the spawn ring, the crystal volume and the four cell configs, and nothing else).
Not proved: that it plays. The controller, the rule and the driver have had a syntax pass and an
API-surface read and no type check.

## Known limitations

- **The target is reasoned, not measured.** How long a pass takes between two 45°/s hulls in the
  Boneyard is a play-test number.
- **The AI's hover is a guess at geometry it does not control.** `AIPilot` owns the vessel's roll,
  so "above along its own up" is only right while the AI flies roughly level. If bots overfly
  rivals without dusting them, `aiDustHover` is the dial.
- **Wildlife is not scored.** The dust also withers opposing-domain fauna and nourishes your own
  (`sparesOwnDomain` on the Butterfly's wither effect), which in a one-colour swarm is asymmetric by
  domain — so creature kills are deliberately left unpaid rather than handed to whoever does not
  share the swarm's colour.
- **The preview is open-ended**: it scores on rival pilots and a solo preview has none.
- **Card art** is rendered by `/cardart` (MODEL tier over the Boneyard at intensity 2: a Butterfly's dust
  capsule falling across a rival below it). It is staging, not a screenshot.
