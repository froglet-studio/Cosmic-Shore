# Prompt — remove Wwise and Parse, and take `SerializeInterface` first-party

> ## ✅ EXECUTED 12 Sep 2026 — do not run this again
>
> `Assets/Wwise/` and `Assets/Parse/` are **deleted**; `[RequireInterface]` is **first-party** at
> `_Scripts/Utility/RequireInterfaceAttribute.cs` + `_Scripts/Editor/RequireInterfaceDrawer.cs`, and
> `Assets/SerializeInterface/` is gone. Outcome and proofs:
> [`THIRD_PARTY_REGISTER.md` §0 row 8 / §2 / §6](../THIRD_PARTY_REGISTER.md),
> [`THIRD_PARTY_DECISIONS.md` §3 / §4](../THIRD_PARTY_DECISIONS.md),
> [`LAUNCH_BLOCKER_INDEX.md` §A4 / §A5](../LAUNCH_BLOCKER_INDEX.md).
>
> **Two of this prompt's own measurements were wrong**, and the corrections are the part worth
> reading before writing the next one of these:
> * **Six live `[RequireInterface]` consumers, not seven** — `AIGunner.cs`'s usage is inside a
>   `/* */` block. *A grep for an attribute's name counts the commented-out ones too.*
> * **Half the 290 lines were dead** — `InterfaceReference<>`, its drawer and `InterfaceArgs` had
>   **zero** consumers, so they were deliberately not reproduced. *When the plan is to rewrite a drop
>   rather than delete it, measure which half anything actually uses first.*
>
> **Still open, and it is not a code question:** whether a Wwise evaluation or project licence was
> ever signed — [`THIRD_PARTY_DECISIONS.md` §2.1](../THIRD_PARTY_DECISIONS.md).
>
> **Not verified in a Unity editor.** The session that executed it had no Unity, no reference
> assemblies and no `unity` binary, so nothing here was compiled and `/verify-unity` could not run.
> The checklist below is the whole of what a human still has to do; it lives here rather than in a
> pull request because this file is the execution record and a PR body is not durable. *(The
> `refactor(utility):` commit message points at "the PR body" for this — it means this list.)*
>
> ### What still needs the editor
>
> **1 · It compiles.** Open the project and let it finish importing. Two new scripts
> (`_Scripts/Utility/RequireInterfaceAttribute.cs`, `_Scripts/Editor/RequireInterfaceDrawer.cs`) plus
> one new test file. The six out-of-editor gates pass, but per CLAUDE.md they are **syntax-level for
> anything deriving from a Unity type**, so they say nothing about `PropertyAttribute` /
> `PropertyDrawer` member resolution — that is exactly what this step is for.
>
> **2 · Run the edit-mode suite**, at least `RequireInterfaceAttributeTests` (5 tests). It reflects
> over every `[RequireInterface]` field and asserts the four preconditions the drawer needs, plus
> that at least six fields still resolve the attribute.
>
> **3 · Each of the six live fields still FILTERS.** This is the part no test can reach — the drawer
> is the load-bearing half, and a field whose drawer is missing looks *perfectly normal* while
> accepting anything. Per field:
>
> | Component | Field | Interface |
> |---|---|---|
> | `PlayerSpawner` | `_playerPrefab` | `IPlayer` |
> | `ImpactCollider` | `impactorObject` | `IImpactor` |
> | `VesselStatus` | `_shipInstance` | `IVessel` |
> | `GunTransformer` | `shipInstance` | `IVesselStatus` |
> | `VesselCollider` | `shipObject` | `IVessel` |
> | `ObjectiveIndicator` | `provider` | `IObjectiveProvider` |
>
> For each, check all four — they fail independently:
> * **a.** Empty, the right-hand end of the field reads `(IVessel)` etc. *If no hint appears at all,
>   the drawer is not bound and nothing else below is meaningful.*
> * **b.** Assign something that DOES implement it → it sticks, and the hint collapses to `*`
>   (returning on hover).
> * **c.** Assign something that does NOT → the field is left **empty** and the console carries
>   exactly **one** `[RequireInterface] '<name>' … does not implement …` warning. *Silently accepting
>   it is the regression this whole rewrite exists to prevent.*
> * **d.** Drag a **GameObject** carrying a qualifying component onto it → the field stores the
>   **component**, not the GameObject.
>
> **4 · One existing serialized value survived.** Open a vessel prefab and confirm
> `VesselStatus._shipInstance` still points where it did. It *should* be untouched — the field's
> name and type did not change, only an attribute's namespace — so this is cheap insurance against
> the one way a move like this could silently clear references.
>
> **5 · Nothing else regressed from the two deletions.** Both folders were provably unreferenced, so
> the realistic risk is zero; a boot to the main menu plus one freestyle flight is enough.

Paste everything below into a fresh session.

---

Three third-party drops in `Assets/` are either **entirely dead** or **unattributable**. None needs a
purchase; all three need a decision and a commit.

Read `Docs/THIRD_PARTY_REGISTER.md` §2 and §6, and `Docs/THIRD_PARTY_DECISIONS.md` §4 (row 8).
Measured on `claude/zealous-davinci-vjwev0`.

## 1. `Assets/Wwise/` — 84 KB, **0 asset files**, 14 orphan `.meta`

Audiokinetic Wwise is licensed **per title**. The folder is a fossil of an earlier middleware
evaluation: it contains **no assets at all** — just 14 `.meta` files describing directories whose
contents are gone. Confirmed independently: **no first-party code references `AkSoundEngine`**, the
project's audio is FMOD (`Assets/Plugins/FMOD`), and CLAUDE.md already states the folder is inert and
that no new audio may be authored against it.

**The only question is a human one, and it is the owner's:** was a Wwise evaluation or project licence
ever signed during that evaluation? If yes, it should be recorded and closed out with Audiokinetic
rather than left implicit. **That is a paperwork question, not a code question** — do not let it block
the deletion.

**Then delete the folder.** 14 orphan `.meta` files describe nothing; Unity will not miss them.

**Two prose sites survive the deletion and must be swept with it** (re-measured at ship time —
no `AkSoundEngine`/`AkAudio` reference exists anywhere in `Assets/_Scripts`, so these are the
whole residue):

| Site | What it says | Do |
|---|---|---|
| `GameModePrefabKitSO.cs` — a serialized field's tooltip | `"Wwise audio entry point."` | re-word to FMOD, or delete the row if the entry point went with the folder |
| `CanvasUpgraderCodeScan.cs` — the comment beside its first-party-roots list | names `Wwise` among the third-party trees it excludes | update the list |

Neither blocks the deletion; both are how a deleted SDK goes on looking present to the next
person who greps for it.

## 2. `Assets/Parse/` — 76 KB, 2 DLLs, **0 references**

Parse (Parse Platform / originally Meta) support DLLs. Measured: **zero** inbound asset references and
**zero** first-party code references — no `ParseClient`, `ParseObject`, `ParseQuery` or `ParseConfig`
anywhere in `_Scripts/` or `FTUE/`. No licence document.

They ship if anything references them, and nothing does. **Delete, with the guid proof in the commit.**

## 3. `Assets/SerializeInterface/` — 290 lines, **7 consumers**, no vendor, no licence, no namespace

This one is **not dead** and must be rewritten rather than removed.

| File | Lines | Tier |
|---|---|---|
| `RequireInterfaceAttribute.cs` | 12 | runtime |
| `InterfaceReference.cs` | 36 | runtime |
| `Editor/InterfaceReferenceUtil.cs` | 44 | editor |
| `Editor/RequireInterfaceDrawer.cs` | 85 | editor |
| `Editor/InterfaceReferenceDrawer.cs` | 113 | editor |

Consumers of `[RequireInterface]`:

```
_Scripts/Controller/AI/AIGunner.cs
_Scripts/Controller/Player/PlayerSpawner.cs
_Scripts/Controller/ImpactEffects/ImpactCollider.cs
_Scripts/Controller/Vessel/VesselStatus.cs
_Scripts/Controller/Vessel/GunTransformer.cs
_Scripts/Controller/Vessel/VesselCollider.cs
_Scripts/UI/ObjectiveIndicator.cs
```

It is the well-known community `[RequireInterface]` pattern, which exists in several public
repositories **under different licences**. It carries no namespace, no header comment, no vendor and
no licence file, and it **ships** — no `.asmdef`, so it compiles into `Assembly-CSharp`.

**Rewrite it first-party.** 48 runtime lines and 242 editor lines, against a pattern whose behaviour
is fully specified by its seven call sites. Put it under `Assets/_Scripts/` in the project's own
namespace (`CosmicShore.Utility`), keep the attribute name `RequireInterface` so **no call site
changes**, and keep the drawers under an `Editor/` folder so they never reach the player
(`Docs/CONDITIONAL_COMPILATION.md`).

Two things to get right:

* **The attribute name and constructor signature must match exactly**, or all seven consumers change
  and the diff stops being reviewable.
* **The drawers are what make the attribute worth having.** A field marked `[RequireInterface]` with
  no drawer silently becomes an ordinary object field that accepts anything — which compiles, looks
  fine, and fails at runtime. Verify each of the seven fields still filters correctly in the
  inspector.

## The removal method

For each folder, in **its own commit**:

1. Read each asset's own guid out of its `.meta`.
2. Assert unique ownership project-wide: `grep -c "^guid: $g"` must be exactly 1.
3. Count holders of that guid outside the folder. **Never `grep -rl` piped to `head -1`** — CLAUDE.md
   records that it returns plausible false positives, and `Docs/LAUNCH_BLOCKER_INDEX.md` reproduces
   the hazard live.
4. Remember the two things a guid check **cannot** see: `Resources.Load` **by name**, and a **C# type
   reference** (`using PlayFab;` creates no guid link). Neither applies to Wwise or Parse — both have
   zero code references either way — but state that you checked rather than assuming.
5. Delete file **and** `.meta` together.

## Definition of done

1. `Assets/Wwise/` and `Assets/Parse/` removed, two commits, each with its proof.
2. The Wwise evaluation-licence question recorded as asked and answered (or explicitly outstanding) in
   `Docs/THIRD_PARTY_DECISIONS.md` §2.
3. `SerializeInterface` rewritten first-party, all seven consumers unchanged, `Assets/SerializeInterface/`
   removed.
4. `Docs/THIRD_PARTY_REGISTER.md` §0 rows 7–8, §2 and §6 updated.
5. Verified in the editor (`/verify-unity`): the project compiles and all seven `[RequireInterface]`
   fields still filter — or stated plainly that it was not.
