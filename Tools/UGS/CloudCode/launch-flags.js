// launch-flags.js - UGS Cloud Code script: the game's live flags, one document read once at boot after sign-in
// (Docs/LAUNCH/LIVE_FLAGS.md; launch blockers #6 and #11 in Docs/LAUNCH/READINESS.md section 10).
//
// The flags live HERE, in FLAGS below, and nowhere else: not in the dashboard, not in Cloud Save, not in a Remote
// Config package (Shombith, 2026-11-05: Cloud Code / Cloud Save, no new package). So the only way a flag changes is a
// pull request that edits this file, then `ugs-deploy` (development first, then production), and every change is
// diffable and revertible in git. The tests (launch-flags.test.cjs, run by deploy.sh before any upload) refuse a typo:
// a version that is not semver, a kill switch the game does not know, a mode name GameModes.cs does not have.
//
// What the game does with it (LaunchFlags.cs):
//   killSwitches  can only turn a feature OFF. effective = the build's compiled gate && !killed. A kill switch never
//                 turns on a gate a build shipped off.
//   minimumVersion  a build below it shows a blocking "update required" screen (UpdateNoticeOverlay) with the
//                 platform's updateUrls link. "0.0.0" lets every build in.
//   recommendedVersion  a build below it shows a dismissible "update available" notice.
//   disabledModes  read and parsed by the game (LaunchFlags.IsModeDisabled); no menu reads it yet (LIVE_FLAGS.md).
//   message       an optional notice (title and body) shown once per distinct message. Empty title = none.
// If this script cannot be reached, the game uses its last answer if it is fresh enough, else the safe default
// (nothing killed, every version allowed), so offline play always keeps working.
//
// Params: none.  Returns: the flags document below.
// Tests: node Tools/UGS/CloudCode/launch-flags.test.cjs   Deploy: Tools/UGS/README.md, "Deploying the scripts".

// ─── The flags. Edit these, open a PR, deploy to development, check, then production. ────────────────────────────
const FLAGS = {
  schema: 1,
  // Every build at or above this plays; below it gets the blocking update screen. Semver: MAJOR.MINOR.PATCH.
  minimumVersion: "0.0.0",
  // Below this, a dismissible "update available" notice. Must be >= minimumVersion.
  recommendedVersion: "0.0.0",
  // Where the update button sends a player, per platform. https (or a store's own scheme); the only query allowed is
  // a store page's "?id=<package>". Windows and iOS point at the website until the Steam app id and the new TestFlight
  // public link exist (READINESS.md section 10, #3 and #16).
  updateUrls: {
    windows: "https://www.froglet.games",
    ios: "https://www.froglet.games",
    android: "https://play.google.com/store/apps/details?id=com.FrogletGames.CosmicShore",
  },
  // true = that server feature is OFF for every build, whatever its compiled gate says. false = the build decides.
  killSwitches: {
    economy: false,       // ServerEconomyGate      - the server-owned crystal wallet (economy.js)
    scores: false,        // ServerScoreGate        - weekly-challenge times through weekly-score.js
    matchReports: false,  // MatchReportGate        - match start / end reports (match-report.js)
    trust: false,         // FairPlayTrustGate      - the trust record and pools (trust.js)
    entitlements: false,  // ServerEntitlementGate  - episode tokens and episodes (Commerce/entitlements.js)
  },
  // GameModes member names (Assets/_Scripts/Data/Enums/GameModes.cs), exact case.
  disabledModes: [],
  // An optional notice. An empty title shows nothing.
  message: { title: "", body: "" },
};
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

// The kill switches the game knows (LaunchKillSwitch in Assets/_Scripts/System/CloudData/LaunchFlags.cs; the test
// checks the two lists match). A new one is added in both places in the same PR, and to the gate it switches.
const KNOWN_KILL_SWITCHES = ["economy", "scores", "matchReports", "trust", "entitlements"];
const PLATFORMS = ["windows", "ios", "android"];
const TOP_LEVEL = ["schema", "minimumVersion", "recommendedVersion", "updateUrls", "killSwitches", "disabledModes", "message"];

// Strict semver 2.0 core with an optional pre-release; no build metadata (the game compares MAJOR.MINOR.PATCH).
const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/;
// An https link (a store page's own "?id=<package>" is the one query allowed), or a store's own scheme.
const URL_OK = /^(https:\/\/[A-Za-z0-9.-]+(\/[A-Za-z0-9._~%\/-]*)?(\?id=[A-Za-z0-9._]+)?|steam:\/\/[A-Za-z0-9\/._-]+|itms-(beta|apps):\/\/[A-Za-z0-9\/._-]*)$/;
const MODE_NAME = /^[A-Z][A-Za-z0-9]*$/;

function parseSemver(v) {
  const m = typeof v === "string" ? SEMVER.exec(v) : null;
  return m ? { nums: [Number(m[1]), Number(m[2]), Number(m[3])], pre: m[4] || "" } : null;
}

// -1, 0, 1 as semver orders them (a pre-release sorts below its release). Throws on a non-semver string.
function compareSemver(a, b) {
  const x = parseSemver(a), y = parseSemver(b);
  if (!x || !y) throw new Error(`not semver: ${!x ? a : b}`);
  for (let i = 0; i < 3; i++) if (x.nums[i] !== y.nums[i]) return x.nums[i] < y.nums[i] ? -1 : 1;
  if (x.pre === y.pre) return 0;
  if (!x.pre) return 1;
  if (!y.pre) return -1;
  const p = x.pre.split("."), q = y.pre.split(".");
  for (let i = 0; i < Math.max(p.length, q.length); i++) {
    if (p[i] === undefined) return -1;
    if (q[i] === undefined) return 1;
    const pn = /^\d+$/.test(p[i]), qn = /^\d+$/.test(q[i]);
    if (pn && qn) { const d = Number(p[i]) - Number(q[i]); if (d) return d < 0 ? -1 : 1; continue; }
    if (pn !== qn) return pn ? -1 : 1;
    if (p[i] !== q[i]) return p[i] < q[i] ? -1 : 1;
  }
  return 0;
}

const isPlainObject = (o) => o !== null && typeof o === "object" && !Array.isArray(o);

// Every problem with a flags document, as a list of plain sentences. Empty = valid.
function validate(doc) {
  const errors = [];
  if (!isPlainObject(doc)) return ["the flags document is not an object"];
  for (const k of Object.keys(doc)) if (!TOP_LEVEL.includes(k)) errors.push(`unknown field "${k}"`);
  for (const k of TOP_LEVEL) if (!(k in doc)) errors.push(`missing field "${k}"`);

  if (doc.schema !== 1) errors.push(`schema must be 1, got ${JSON.stringify(doc.schema)}`);
  for (const k of ["minimumVersion", "recommendedVersion"])
    if (!parseSemver(doc[k])) errors.push(`${k} is not semver (MAJOR.MINOR.PATCH): ${JSON.stringify(doc[k])}`);
  if (parseSemver(doc.minimumVersion) && parseSemver(doc.recommendedVersion) &&
      compareSemver(doc.recommendedVersion, doc.minimumVersion) < 0)
    errors.push(`recommendedVersion ${doc.recommendedVersion} is below minimumVersion ${doc.minimumVersion}`);

  if (!isPlainObject(doc.updateUrls)) errors.push("updateUrls must be an object");
  else {
    for (const k of Object.keys(doc.updateUrls)) if (!PLATFORMS.includes(k)) errors.push(`unknown updateUrls platform "${k}"`);
    for (const p of PLATFORMS) {
      const u = doc.updateUrls[p];
      if (typeof u !== "string" || u.length > 300 || !URL_OK.test(u))
        errors.push(`updateUrls.${p} must be an https (or steam:// / itms-*) link, no query but a store's ?id=: ${JSON.stringify(u)}`);
    }
  }

  if (!isPlainObject(doc.killSwitches)) errors.push("killSwitches must be an object");
  else {
    for (const [k, v] of Object.entries(doc.killSwitches)) {
      if (!KNOWN_KILL_SWITCHES.includes(k)) errors.push(`unknown kill switch "${k}" (known: ${KNOWN_KILL_SWITCHES.join(", ")})`);
      else if (typeof v !== "boolean") errors.push(`killSwitches.${k} must be true or false, got ${JSON.stringify(v)}`);
    }
    for (const k of KNOWN_KILL_SWITCHES) if (!(k in doc.killSwitches)) errors.push(`killSwitches.${k} is missing (write false to leave it on)`);
  }

  if (!Array.isArray(doc.disabledModes)) errors.push("disabledModes must be a list");
  else {
    const seen = new Set();
    for (const m of doc.disabledModes) {
      if (typeof m !== "string" || !MODE_NAME.test(m)) errors.push(`disabledModes entry is not a GameModes name: ${JSON.stringify(m)}`);
      else if (seen.has(m)) errors.push(`disabledModes lists "${m}" twice`);
      seen.add(m);
    }
  }

  if (!isPlainObject(doc.message)) errors.push("message must be an object with title and body");
  else {
    for (const k of Object.keys(doc.message)) if (k !== "title" && k !== "body") errors.push(`unknown message field "${k}"`);
    const { title, body } = doc.message;
    if (typeof title !== "string" || title.length > 80 || /[\r\n]/.test(title)) errors.push("message.title must be one line of at most 80 characters");
    if (typeof body !== "string" || body.length > 400) errors.push("message.body must be text of at most 400 characters");
    if (typeof title === "string" && typeof body === "string" && !title && body) errors.push("message.body is set but message.title is empty (an empty title shows nothing)");
  }
  return errors;
}

module.exports = async ({ logger }) => {
  const errors = validate(FLAGS);
  if (errors.length) {
    // Unreachable after the tests, which deploy.sh runs first. Fail loud: the game then keeps its last answer.
    logger.error(`launch flags invalid: ${errors.join("; ")}`);
    throw new Error("launch flags invalid");
  }
  return JSON.parse(JSON.stringify(FLAGS));
};

module.exports.params = {};

module.exports._test = { FLAGS, KNOWN_KILL_SWITCHES, PLATFORMS, validate, compareSemver, parseSemver };
