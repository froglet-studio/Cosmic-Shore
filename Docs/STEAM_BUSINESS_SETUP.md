# Steam Business Setup — Workstream A runbook

Step-by-step for getting Cosmic Shore onto Steamworks. Everything here is off-machine: accounts,
paperwork, and store configuration. No code.

**Owners:** Shombith leads setup · Caleb and Garrett on administration and legal
**Related:** `Docs/STEAM_EA_INVESTOR_CHECKPOINT.pdf`, `Docs/BUILD_AND_DELIVERY.md`, `Tools/Steam/README.md`

---

## The one thing that determines your launch date

> **Steam enforces a 30-day waiting period between paying the app fee and being allowed to release.**

That clock starts the moment the $100 is paid — not when the build is ready, not when the store page
goes live. Nothing else in the plan can compress it.

**Consequence: pay the fee on day one.** Not after the store copy is written, not after the capsules
land. Everything else in Workstream A, and every other workstream, can proceed in parallel while the
clock runs. If the fee slips a week, launch slips a week regardless of how fast engineering moves.

Three timing gates stack on top of each other, and all three must be satisfied:

| Gate | Requirement | Starts when |
|---|---|---|
| Steam Direct waiting period | **30 days** | You pay the $100 fee |
| Coming Soon page live | **≥ 2 weeks** before release | Store page passes review and goes public |
| Store page + build review | 3–5 business days each; submit page **7 days** before you want it live | You submit for review |

---

## Before anyone touches Steamworks — gather this

Missing any one of these stalls the whole workstream. Collect it first.

| Item | Detail | Who |
|---|---|---|
| Legal entity name | Exactly as it appears on the bank account and tax filings. Froglet Inc., Delaware C-corp. | Garrett |
| Entity type + EIN | C-corporation; federal EIN | Garrett |
| Registered address | The company address of record, Grand Rapids MI | Garrett |
| Bank details | Routing number, account number, bank address. **The account holder name must match the legal entity name exactly** — this is the single most common onboarding rejection. | Garrett |
| Authorized signer | The agreements are legal documents binding the company. An officer must sign, not a contractor. | Garrett |
| Payment method | Card that can take the $100 charge | Caleb |
| Steam account for the partner admin | Use a **company-controlled** Steam account with its own email and Steam Guard, not someone's personal gaming account. Whoever holds it controls the app, and it should survive that person leaving. | Shombith |

> Tax verification runs through a third party and takes **2–7 business days**. You cannot edit tax
> information while it is in flight, so get it right the first time.

---

## Phase 1 — Day one (A1 + A2)

Do all of this in a single sitting. It is the long pole and it gates everything.

1. **Create the Steamworks partner account** — <https://partner.steamgames.com>, sign up as a company.
   Use the company-controlled Steam account from the table above. *(Shombith)*
2. **Sign the digital agreements** — the Steam Distribution Agreement and associated paperwork.
   Must be executed by the authorized signer. *(Garrett)*
3. **Pay the $100 Steam Direct fee and create the app.** ⏱ **This starts the 30-day clock.** The fee
   is per-product and non-refundable, but is recouped once the app clears $1,000 adjusted gross
   revenue. *(Shombith)*
4. **Submit banking, tax, and identity verification.** US company → the tax interview collects
   W-9-equivalent information. *(Garrett, with Caleb)*

**Record the app ID the moment it exists.** It unblocks `Tools/Steam/upload.sh` (checklist B4) —
set `STEAM_APPID` and `STEAM_DEPOTID`, and the upload path is live.

**Exit condition:** fee paid, app ID issued, paperwork submitted. Verification runs in the
background; do not wait on it before starting Phase 2.

---

## Phase 2 — While verification runs (A3 + A4 + A6)

Days 2–7. None of this needs verification to have completed.

### A3 · Content survey *(Garrett)*
The mature-content questionnaire in App Admin. Cosmic Shore is a non-violent abstract space game, so
this is quick — but answer it honestly rather than minimising; a wrong answer found later is a store
page taken down.

### A4 · Early Access questionnaire *(Caleb)*
Six required questions, shown verbatim on the store page. Draft answers below — edit for voice, keep
the substance, and keep every claim true. Steam's own guidance is to be conservative: players hold
you to these answers, and the store page shows a warning if an Early Access game goes 12 months
without an update.

> **Why Early Access?**
> Cosmic Shore is a party game for pilots, and party games are shaped by the people playing them.
> The core is finished and stable — eleven vessel classes, a tournament mode, a freestyle hub, and
> online multiplayer all work today. What we want now is a community telling us which vessels and
> modes deserve to grow next, before we commit the next year of development to guessing.

> **How long will this be in Early Access?**
> We are planning roughly 12 months, and we would rather move that date than ship a version we are
> not proud of. We will say so on the store page if it changes.

> **How is the full version planned to differ?**
> More vessel classes with their own genre of play, more party-game modes, and a deeper progression
> and episode structure. We also plan Steam achievements, controller rebinding, and additional
> languages. The full version is a wider game, not a repaired one.

> **What is the current state of the Early Access version?**
> Fully playable and feature-complete for what it advertises. Six flyable vessels, the Maelstrom
> tournament chaining three competitive modes, additional standalone modes, a freestyle hub with a
> painting gallery and toys, online multiplayer with parties and friends, and progression through
> quests and vessel unlocks. This is not a prototype or a demo.

> **Will the game be priced differently during and after Early Access?**
> No. The price stays the same when we leave Early Access. Anyone who buys during Early Access keeps
> the game and every update at no extra cost.

> **How are you planning on involving the Community?**
> Our Discord is where development actually happens: build announcements, playtests on a public beta
> branch, and direct conversation about which vessel or mode we build next. We also read and reply
> on the Steam forums.

### A6 · Pricing *(Garrett)*
**Base price: $15.00 USD.** The price does **not** rise at 1.0. Regional pricing: accept Valve's
suggested conversions. Launch discount is still undecided.
Set the base price and any launch discount. Two things to know: Steam requires a **minimum interval
between discounts**, and there are rules about how soon after launch you may discount — plan the
launch discount deliberately rather than adding one late. Regional pricing can be generated from
Valve's suggested conversions; take the suggestion unless you have a reason not to.

---

## Phase 3 — Store page (A5) 🔴 the gate everyone waits on

This is the item to protect. The Coming Soon page must be public **at least two weeks** before
release, and every extra week live is wishlists accumulating.

**Submit the page for review at least 7 days before you want it public.** Review takes 3–5 business
days and a rejection costs another round.

### Required assets

Capsule dimensions changed — these are the **current upload sizes**, not the smaller display sizes
that older guides (and the original punch list) quote. Give Will these numbers, not the old ones.

| Asset | Upload size | Notes |
|---|---|---|
| **Header capsule** | **920 × 430** | The only capsule every game must have. Top of the store page, home-page recommendations, Big Picture. |
| **Small capsule** | **462 × 174** | Downscaled hard — appears as small as 120 × 45 in search and Top Sellers. **Design for legibility at 120 × 45**, not at full size. |
| **Main capsule** | **1232 × 706** | Sale pages and front-page promotions. |
| **Vertical capsule** | **748 × 896** | Seasonal sales and featured placement. |
| **Library capsule** | **600 × 900** | 2:3 portrait. What owners see in their own library grid. |

All five take JPG or PNG, up to 2 MB each.

Also required: **at least 5 screenshots at 1920×1080** (one per minigame plus tournament and
freestyle), short and long descriptions, feature bullets, tags, genre, and the Discord and support
links. A trailer is not strictly required to publish, but a store page without one converts poorly.

### Steps
1. Fill the store page in App Admin: descriptions, tags, links, system requirements *(Caleb)*
2. Upload the five capsules and screenshots *(Will → Caleb to place)*
3. Set the release status to **Coming Soon** *(Caleb)*
4. Submit for review *(Shombith)*
5. On approval, make it public and **start tracking wishlists from day one** (checklist E9)

**System requirements to enter** — the floor from the launch plan, confirmed by the profiling session
(checklist D1): Windows 10 64-bit, GTX 1060-class GPU, 8 GB RAM. Do not enter these until D1
confirms them.

---

## Phase 4 — Build (A7)

1. Build and upload to the `internal` branch — `Docs/BUILD_AND_DELIVERY.md` §3–4 *(Shombith)*
2. Smoke test the Steam install; verify the overlay renders (checklist B7)
3. Promote to `beta` for the closed playtest (checklist E7)
4. Mark the build for review. Store page review must pass **before** build review begins.

---

## Phase 5 — Release

Steam does **not** release automatically. Someone clicks **Release App** at the moment you choose,
and only once all of these are true:

- [ ] 30 days elapsed since the app fee was paid
- [ ] Coming Soon page public for ≥ 2 weeks
- [ ] Store page review passed
- [ ] Build review passed
- [ ] Banking and tax verification complete
- [ ] Exit checklist signed (checklist D5)

---

## Master checklist

| # | Task | Owner | Blocks |
|---|---|---|---|
| A1a | Gather entity, banking, tax, signer details | Garrett | Everything |
| A1b | Create Steamworks partner account | Shombith | A1c |
| A1c | Sign digital agreements | Garrett | A2 |
| A2 | **Pay $100 fee, create app, record app ID** | Shombith | **30-day clock, B4** |
| A1d | Submit banking + tax + identity | Garrett, Caleb | Release |
| A3 | Content survey | Garrett | Store review |
| A4 | Early Access questionnaire | Caleb | Store review |
| A6 | Price + launch discount | Garrett | Store review |
| A5 | Store page built and submitted | Caleb | **Coming Soon 2-week clock** |
| A7 | Build uploaded and submitted for review | Shombith | Release |

---

## Failure modes worth pre-empting

- **Bank account name mismatch.** The account holder must match the legal entity exactly. The most
  common rejection, and it costs days.
- **Personal Steam account as partner admin.** Whoever owns it owns the app. Use a company account
  with its own email and Steam Guard from the start; migrating later is painful.
- **Paying the fee late.** The 30-day clock is the hard floor on the launch date. Nothing recovers it.
- **Submitting the store page without the two-week buffer.** Review is 3–5 business days *and then*
  the Coming Soon page needs two weeks. Treat "page submitted" as three weeks before launch, minimum.
- **Old capsule dimensions.** Several widely-cited guides still list the display sizes. Use the
  upload sizes in the table above.
- **Over-promising in the Early Access questionnaire.** The answers are public and permanent-feeling.
  Under-promise on scope and timeline.

---

## Sources

Verified against Steamworks documentation, July 2026:
[Steam Direct Fee](https://partner.steamgames.com/doc/gettingstarted/appfee) ·
[Onboarding](https://partner.steamgames.com/doc/gettingstarted/onboarding) ·
[Release Process](https://partner.steamgames.com/doc/store/releasing) ·
[Early Access](https://partner.steamgames.com/doc/store/earlyaccess)

Steam's rules change. Re-check the fee, the waiting period, and capsule dimensions in App Admin
before acting on anything time-critical here.
