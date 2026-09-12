# Prompt — remove Wwise and Parse, and take `SerializeInterface` first-party

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
