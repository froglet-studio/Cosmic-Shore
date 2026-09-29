# Prompt — cohort every player by invite wave

Paste everything below into a fresh session.

---

The paid-EA gate is defined to read retention and stability **cohorted by invite wave** — D1 ≥ 35%
and D7 ≥ 15% *across two consecutive waves*, crash-free sessions ≥ 99.5% on a rolling window. Every
funnel and retention chart is supposed to split by wave, and the gate reads directly off those
dashboards.

No cohort dimension exists in the analytics code. The gate currently has no data source.

This is the missing piece of checklist item **C7**. The rest of C7 shipped: both sinks
(`PostHogAnalyticsSink`, `UgsAnalyticsSink`) behind a consent overlay, a real event surface, the
match envelope, onboarding and game funnels.

Read `Docs/STEAM_RELEASE_TASKS.md` (item R9), `Docs/Analytics/DATA_ARCHITECTURE.md` §7.1 and
`Docs/Analytics/event-taxonomy.md` first.

## The constraint that decides the design — read this before writing anything

**The wave belongs on the PERSON record, not on every event.** `AnalyticsServiceFacade` fans each
event to every sink with an identical payload, and the existing envelope is deliberately a PostHog
concern that lives in the PostHog sink. The docstring on `BuildSinkEnvelope()`
(`AnalyticsServiceFacade.cs:405`) says exactly why:

> *UGS already auto-collects the player id, platform and app version as core data, and it validates
> every custom parameter against a schema hand-created in the dashboard Event Manager — where
> events and parameters, once created, **can never be deleted** and count against a
> **1,500-per-environment cap**. Stamping six redundant fields onto 28 events would have cost 168
> permanent, unnecessary schema rows for data UGS already has.*

Stamping `invite_wave` onto every event repeats that mistake: it is one more permanent schema row
per event, for a value that never changes for a given player. Put it where it belongs —

```
AnalyticsServiceFacade  →  IdentifyPlayer(...)  →  sink.Identify(distinctId, personProperties)
                                                    // AnalyticsServiceFacade.cs:486
```

— as a **person property**. PostHog's cohorts and retention are person-based, so a person property
is directly filterable on every insight with no per-event cost, and sinks with no person concept
(UGS) are documented as free to ignore `Identify`. That is the whole design.

## The harder question: where does a wave id come from?

**Steam does not hand the client a wave number.** Grants are issued in Steamworks; the client has
no API for "which batch was I in", and there is no Steam SDK integration in this build at all — the
checkpoint's Engineering Positions state *"Steam SDK: none at launch — friend invites are Playtest
configuration on Valve's side, not client integration."* So the wave must be **derived**, and the
honest options are:

| Option | How | Cost |
|---|---|---|
| **First-seen week** *(recommended)* | The UTC week of the player's first session, stored once and never rewritten. Waves are granted in weekly batches, so week-of-first-play is a faithful proxy for wave. | Needs one durable first-seen timestamp. |
| Manual cohort tag | An operator sets a build-time or config value per wave. | Wrong the moment a tester installs late, which is most of them. |
| Steam SDK | Real grant data. | Explicitly cut from this window. |

Take **first-seen week** unless you find a reason not to, and implement it with the same UTC-Monday
boundary the weekly challenge already uses — `Docs/WEEKLY_CHALLENGE.md` records both the ISO-8601
rule and the trap that `DayOfWeek` numbers Sunday as 0, which puts a naive subtraction in the
*following* week. Reuse that period-key helper rather than writing a second one; two different
definitions of "which week" in one project is a reporting bug nobody will find.

**First-seen must be durable and must never be rewritten.** It belongs in the cloud profile
alongside the other per-player facts, so it survives a reinstall and roams across devices —
otherwise a player who reinstalls appears in a later cohort and silently inflates that wave's D1.
Look at the repositories under `Assets/_Scripts/System/CloudData/Repositories/` and pick the one
that already carries profile-level data rather than adding a new one.

## What to build

1. A `FirstSeenUtc` (or equivalent) on the player profile: written **once**, on the first session
   where it is absent, never updated afterwards.
2. A derived `invite_wave` person property — the UTC-Monday week key of `FirstSeenUtc` — plus
   `first_seen_utc` itself, so analysis can re-bucket later without a client change. Send both
   through `Identify`.
3. Whatever `IdentifyPlayer` needs so the properties are sent on **every** session, not only the
   first. PostHog person properties are last-write-wins; a value only sent at install is lost the
   moment the person record is touched by anything else.
4. A short section in `Docs/Analytics/DATA_ARCHITECTURE.md` stating the derivation, the UTC-Monday
   rule, and — explicitly — that `invite_wave` is a **proxy for a grant batch, not the grant batch
   itself**. Whoever reads the gate needs to know that, because a tester who requested access in
   week 1 and installed in week 3 lands in wave 3.

## Constraints

- **Do not add a per-event parameter.** If you find yourself adding `invite_wave` to
  `BuildSinkEnvelope` or to `RecordEvent`'s payload, re-read the constraint section above.
- Consent and the COPPA age gate live **upstream** of `IAnalyticsSink`, in the facade. Do not
  reach around them, and do not let a sink opt out — adding a dimension must not widen collection.
  A player who has not consented still gets no events and no `Identify`.
- Do not create a second "which week is it" implementation.
- `RequestDataDeletion` must still be able to honour erasure for a player carrying these
  properties.

## Definition of done

1. A fresh install records a first-seen timestamp exactly once; a second launch does not change it.
2. Every session sends `invite_wave` and `first_seen_utc` as person properties through `Identify`.
3. Zero new UGS event-schema parameters.
4. The derivation and its proxy caveat are written down where an analyst will read them.
5. A player who declines consent produces no events and no identify call.
6. `Docs/STEAM_RELEASE_TASKS.md` R9 is ticked, and the PR notes that Definition of Done #6 now has
   a data source — while the dashboards themselves still have to be built in PostHog.
