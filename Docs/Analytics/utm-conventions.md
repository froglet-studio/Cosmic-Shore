# UTM Conventions — Outbound Link Tagging Vocabulary

> **Provenance.** Written on the `claude/analytics-attribution-viability-vwkunw` branch
> (PR #592) as an analysis/design pass, and salvaged onto bleeding-edge after the sink
> layer shipped separately. The *analysis* here stands. Where it describes implementation
> shape (interface signatures, class names, field lists), **`Docs/Analytics/DATA_ARCHITECTURE.md`
> is the authority** - the shipped `IAnalyticsSink` differs in detail (`RecordEvent` +
> `Identify` + `StartCollection`/`StopCollection`, person properties, a disk-persisted queue).

> Companion to `viability-report.md` (2026-07). This is a **fixed vocabulary**, not a
> guideline: Valve documents no case-folding or variant-merging for UTM values (see §4), so
> assume `Discord`, `discord`, and `discord-server` are three different rows forever — and
> because Steam suppresses low-volume rows, every needless variant risks pushing a channel
> below the reporting threshold. Every tagged link is built from the closed lists below.
> Adding a value to a list is a PR to this file.

## 1. Rules

1. **All lowercase, always.** No spaces. Words within a value are separated by `_`
   (underscore), never `-`, so values survive being pasted into systems that treat `-` as a
   word break.
2. `utm_source` = *where the click physically happened* (the platform/property).
   `utm_medium` = *what kind of placement it was*. `utm_campaign` = *which push it belongs
   to*. Never encode the same fact twice.
3. Every outbound link to a store page from a surface we control gets all three parameters.
   `utm_term` / `utm_content` are optional and only for paid-ad variant testing
   (`utm_content=<creative_slug>`).
4. Untagged store links are reserved for genuinely organic word-of-mouth — if we wrote the
   link, it is tagged.
5. One canonical URL per (surface × campaign). Build them from the tables below; do not
   hand-type.

## 2. Allowed values

### `utm_source` (closed list)

| Value | Meaning |
|---|---|
| `website` | froglet.games and any page on it |
| `discord` | The Cosmic Shore / Froglet Discord server |
| `newsletter` | Email list sends |
| `presskit` | Press kit page / distributed press materials |
| `youtube` | Froglet channel content + paid YouTube placements |
| `tiktok` | TikTok organic + paid |
| `instagram` | Instagram organic + paid |
| `x` | X/Twitter organic + paid |
| `reddit` | Reddit posts/comments/ads |
| `bluesky` | Bluesky posts |
| `itchio` | Links placed on our itch.io page |
| `devlog` | Devlog posts (wherever hosted) |
| `partner_<name>` | Cross-promo / creator collabs, e.g. `partner_pilotcast` — one value per partner, registered here |

### `utm_medium` (closed list)

| Value | Meaning |
|---|---|
| `social` | Organic social post |
| `video` | Organic video content (trailer embed, devlog video) |
| `cpc` | Any paid placement (cost-per-click/impression — all paid traffic uses this one value) |
| `email` | Newsletter/email body links |
| `referral` | Partner/press/cross-promo placements |
| `banner` | Static placements on sites we control (site header button, itch page link) |

### `utm_campaign` pattern

```
<yyyy>_<mm>_<slug>          e.g.  2026_10_steam_wishlist_push
```

- `slug` is 1-3 words, underscores, unique per push. Evergreen placements (site header
  button, Discord pinned link) use the standing campaign `evergreen`.
- Never reuse a campaign value across years — the date prefix guarantees this.

## 3. Link surface inventory

Exact tagged URLs for every surface we control. `{STEAM_URL}` is a placeholder until the
Steam store page exists (no Steam app ID exists yet — see `viability-report.md` §0); the
pattern is `https://store.steampowered.com/app/<APPID>/Cosmic_Shore/`. Evergreen rows use
`utm_campaign=evergreen`; campaign pushes substitute their own campaign value.

| Surface | Tagged URL |
|---|---|
| Website — header "Wishlist/Buy" button | `{STEAM_URL}?utm_source=website&utm_medium=banner&utm_campaign=evergreen` |
| Website — launch/announcement blog post | `{STEAM_URL}?utm_source=website&utm_medium=banner&utm_campaign=<campaign>` |
| Discord — pinned/welcome link | `{STEAM_URL}?utm_source=discord&utm_medium=social&utm_campaign=evergreen` |
| Discord — announcement post | `{STEAM_URL}?utm_source=discord&utm_medium=social&utm_campaign=<campaign>` |
| Newsletter | `{STEAM_URL}?utm_source=newsletter&utm_medium=email&utm_campaign=<campaign>` |
| Press kit | `{STEAM_URL}?utm_source=presskit&utm_medium=referral&utm_campaign=evergreen` |
| itch.io page → Steam cross-link | `{STEAM_URL}?utm_source=itchio&utm_medium=banner&utm_campaign=evergreen` |
| YouTube — video descriptions | `{STEAM_URL}?utm_source=youtube&utm_medium=video&utm_campaign=<campaign>` |
| YouTube — paid ads | `{STEAM_URL}?utm_source=youtube&utm_medium=cpc&utm_campaign=<campaign>&utm_content=<creative_slug>` |
| TikTok — organic | `{STEAM_URL}?utm_source=tiktok&utm_medium=social&utm_campaign=<campaign>` |
| TikTok — paid | `{STEAM_URL}?utm_source=tiktok&utm_medium=cpc&utm_campaign=<campaign>&utm_content=<creative_slug>` |
| Instagram — organic (link-in-bio) | `{STEAM_URL}?utm_source=instagram&utm_medium=social&utm_campaign=evergreen` |
| X/Twitter — organic | `{STEAM_URL}?utm_source=x&utm_medium=social&utm_campaign=<campaign>` |
| Reddit — organic posts | `{STEAM_URL}?utm_source=reddit&utm_medium=social&utm_campaign=<campaign>` |
| Reddit — paid | `{STEAM_URL}?utm_source=reddit&utm_medium=cpc&utm_campaign=<campaign>&utm_content=<creative_slug>` |
| Creator/partner collab | `{STEAM_URL}?utm_source=partner_<name>&utm_medium=referral&utm_campaign=<campaign>` |

**Today's live store surfaces** (pre-Steam) take the same parameters:

- itch.io page: `https://frogletgames.itch.io/cosmic-shore?utm_source=<source>&utm_medium=<medium>&utm_campaign=<campaign>` — itch.io does not have a Steam-style UTM report; UTM-tagged itch links are readable only in itch's referrer analytics, so keep tagging for consistency but expect coarse data.
- TestFlight invites (`https://testflight.apple.com/join/9ReKxeGf`) do not support UTM
  passthrough — use distinct invite links per channel if per-channel TestFlight attribution
  is ever needed.

## 4. Steam-side behavior (what the tags actually hit)

Verified against [Valve's UTM Analytics documentation](https://partner.steamgames.com/doc/marketing/utm_analytics)
on 2026-07-14:

- All five `utm_*` parameters are supported on `store.steampowered.com` **app pages and sale
  pages** only (not developer homepages or bundles). Since 2025-04-22, UTM parameters on
  sale-page links pass through to the individual games on that page.
- The Steamworks report shows, per unique UTM combination: **Total Visits** (any UTM click),
  **Trusted Visits** (bot-filtered), **Tracked Visits** (user logged into Steam in that
  browser), and conversions — **wishlist adds, purchases, activations** — attributed within
  **72 hours** of the click, same AppID only. CSV download; no API.
- **Only logged-in-browser users convert-attribute.** Practitioner measurements put tracked
  traffic at roughly ≤10% of clicks. Treat Steam UTM numbers as a consistent *sample*, valid
  for channel-vs-channel comparison, not as absolute counts.
- **Low-volume rows are suppressed** below an undisclosed threshold, and reports never
  include Steam IDs — aggregate only, by design. This is why the vocabulary above is small:
  fragmenting a channel across variant spellings can drop it below the threshold entirely.
- **Normalization is undocumented.** Nothing in Valve's docs says values are case-folded or
  merged; industry-standard UTM handling is case-sensitive. The lowercase-only rule in §1
  makes the question moot.
- In-game purchases, demo/DLC-only downloads, and playtest conversions are **not** counted.
- Age-gated store pages still record the UTM click.

## 5. Mobile counterparts (for completeness)

The same vocabulary feeds the mobile stores' native attribution when those channels matter:

- **Google Play**: append the same UTM set to the Play Store URL inside
  `referrer=` — e.g.
  `https://play.google.com/store/apps/details?id=<pkg>&referrer=utm_source%3Ddiscord%26utm_medium%3Dsocial%26utm_campaign%3D2026_10_launch`.
  Unlike Steam, Android delivers the referrer string **into the app** via the Play Install
  Referrer API — a per-install acquisition source with no coupon/backend work. This is the
  cheapest real attribution bridge the project has available on any platform today
  (see `viability-report.md` Option B′).
- **Apple App Store**: Apple ignores `utm_*`; use App Store Connect campaign links
  (`?pt=<provider>&ct=<campaign>&mt=8` on the store URL). `ct` values follow the same
  `<yyyy>_<mm>_<slug>` pattern, aggregate-only reporting in App Store Connect.
