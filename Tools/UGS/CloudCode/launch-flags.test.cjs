// Tests for launch-flags.js: node Tools/UGS/CloudCode/launch-flags.test.cjs
//
// The first test is the gate: the flags document as it stands in launch-flags.js must be valid, so a typo (a version
// that is not semver, a kill switch the game does not know, a GameModes name that does not exist) fails here, and
// deploy.sh runs this before any upload. The rest pin the rules, the game's side of the names, and the wiring.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const ROOT = path.resolve(__dirname, "../../..");
const read = (rel) => fs.readFileSync(path.join(ROOT, rel), "utf8");

const script = require(path.join(__dirname, "launch-flags.js"));
const { FLAGS, KNOWN_KILL_SWITCHES, validate, compareSemver, parseSemver } = script._test;
const logger = { info() {}, warning() {}, error() {} };

// The GameModes member names, read from the enum the game compiles.
function gameModeNames() {
  const src = read("Assets/_Scripts/Data/Enums/GameModes.cs");
  const body = src.slice(src.indexOf("enum GameModes"));
  return new Set([...body.matchAll(/^\s*([A-Za-z][A-Za-z0-9]*)\s*=\s*-?\d+/gm)].map((m) => m[1]));
}

// The kill-switch constants the game knows: LaunchKillSwitch in LaunchFlags.cs, as { Const: "name" }.
function gameKillSwitches() {
  const src = read("Assets/_Scripts/System/CloudData/LaunchFlags.cs");
  const start = src.indexOf("static class LaunchKillSwitch");
  assert.ok(start >= 0, "LaunchKillSwitch not found in LaunchFlags.cs");
  const block = src.slice(start, src.indexOf("}", start));
  return Object.fromEntries([...block.matchAll(/public const string (\w+) = "(\w+)";/g)].map((m) => [m[1], m[2]]));
}

// Which gate each kill switch switches, and the file it lives in.
const GATE_FILES = {
  economy: "Assets/_Scripts/System/CloudData/ServerEconomy.cs",
  scores: "Assets/_Scripts/System/CloudData/ServerScores.cs",
  matchReports: "Assets/_Scripts/System/CloudData/ServerMatchReports.cs",
  trust: "Assets/_Scripts/System/CloudData/ServerTrust.cs",
  entitlements: "Assets/_Scripts/System/Monetization/ServerEntitlements.cs",
};

const clone = (o) => JSON.parse(JSON.stringify(o));
const good = () => clone(FLAGS);
const refuses = (doc, fragment) => {
  const errors = validate(doc);
  assert.ok(errors.some((e) => e.includes(fragment)), `expected an error containing "${fragment}", got ${JSON.stringify(errors)}`);
};

let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

(async () => {
  await test("THE GATE: the flags document in launch-flags.js is valid", () => {
    const errors = validate(FLAGS);
    assert.ok(errors.length === 0, `the flags document in launch-flags.js is invalid - fix it before deploying: ${errors.join("; ")}`);
  });
  await test("every disabledModes entry is a GameModes member", () => {
    const names = gameModeNames();
    assert.ok(names.has("Elimination") && names.size > 30, "GameModes.cs was not read");
    for (const m of FLAGS.disabledModes) assert.ok(names.has(m), `"${m}" is not a GameModes member (Assets/_Scripts/Data/Enums/GameModes.cs)`);
  });
  await test("the script's kill switches are exactly the game's (LaunchKillSwitch)", () => {
    assert.deepEqual(Object.values(gameKillSwitches()).sort(), [...KNOWN_KILL_SWITCHES].sort());
    assert.deepEqual(Object.keys(GATE_FILES).sort(), [...KNOWN_KILL_SWITCHES].sort());
  });
  await test("each kill switch is read by the gate it switches", () => {
    const consts = gameKillSwitches();
    for (const [constName, name] of Object.entries(consts)) {
      const src = read(GATE_FILES[name]);
      assert.ok(src.includes(`LaunchFlags.Gate(Enabled, LaunchKillSwitch.${constName})`),
        `${GATE_FILES[name]} does not read LaunchKillSwitch.${constName} through LaunchFlags.Gate`);
    }
  });
  await test("the game calls this endpoint by this file's name, and deploy.sh uploads it", () => {
    assert.match(read("Assets/_Scripts/System/CloudData/LaunchFlagsService.cs"), /public const string Endpoint = "launch-flags";/);
    assert.match(read("Tools/UGS/deploy.sh"), /Tools\/UGS\/CloudCode\/launch-flags\.js/);
  });
  await test("the endpoint returns the document, as a copy", async () => {
    const before = FLAGS.killSwitches.economy;
    const a = await script({ params: {}, context: { projectId: "p", playerId: "a" }, logger });
    assert.deepEqual(a, FLAGS);
    a.killSwitches.economy = !before;
    assert.equal(FLAGS.killSwitches.economy, before);
    const b = await script({ params: {}, context: { projectId: "p", playerId: "b" }, logger });
    assert.deepEqual(b, FLAGS);
  });
  await test("the endpoint refuses to serve an invalid document (the game then keeps its last answer)", async () => {
    const saved = FLAGS.minimumVersion;
    FLAGS.minimumVersion = "1.0";
    try {
      await assert.rejects(script({ params: {}, context: {}, logger }), /launch flags invalid/);
    } finally { FLAGS.minimumVersion = saved; }
  });
  await test("semver: numeric order, pre-releases below their release", () => {
    assert.equal(compareSemver("1.2.3", "1.2.3"), 0);
    assert.equal(compareSemver("1.2.3", "1.10.0"), -1);
    assert.equal(compareSemver("2.0.0", "1.99.99"), 1);
    assert.equal(compareSemver("1.0.0-beta", "1.0.0"), -1);
    assert.equal(compareSemver("1.0.0-beta.2", "1.0.0-beta.11"), -1);
    assert.equal(compareSemver("1.0.0-alpha", "1.0.0-alpha.1"), -1);
    assert.equal(compareSemver("1.0.0-rc.1", "1.0.0-beta"), 1);
    for (const bad of ["1.0", "v1.0.0", "1.0.0.0", "01.0.0", "1.0.0+build", "", null, 1])
      assert.equal(parseSemver(bad), null, `${JSON.stringify(bad)} should not parse`);
  });
  await test("refused: a version that is not semver", () => {
    refuses({ ...good(), minimumVersion: "1.0" }, "minimumVersion is not semver");
    refuses({ ...good(), recommendedVersion: "v0.3.0" }, "recommendedVersion is not semver");
  });
  await test("refused: recommendedVersion below minimumVersion", () => {
    refuses({ ...good(), minimumVersion: "0.3.0", recommendedVersion: "0.2.9" }, "is below minimumVersion");
  });
  await test("refused: an unknown kill switch, a missing one, a non-boolean one", () => {
    const d1 = good(); d1.killSwitches.econmy = true; refuses(d1, 'unknown kill switch "econmy"');
    const d2 = good(); delete d2.killSwitches.trust; refuses(d2, "killSwitches.trust is missing");
    const d3 = good(); d3.killSwitches.scores = "true"; refuses(d3, "killSwitches.scores must be true or false");
  });
  await test("refused: bad update links (http, a signed-looking query, an unknown platform, a missing one)", () => {
    const d1 = good(); d1.updateUrls.windows = "http://www.froglet.games"; refuses(d1, "updateUrls.windows");
    const d2 = good(); d2.updateUrls.ios = "https://x.example/a?token=abc"; refuses(d2, "updateUrls.ios");
    const d3 = good(); d3.updateUrls.mac = "https://www.froglet.games"; refuses(d3, 'unknown updateUrls platform "mac"');
    const d4 = good(); delete d4.updateUrls.android; refuses(d4, "updateUrls.android");
    const d5 = good(); d5.updateUrls.windows = "steam://run/123"; d5.updateUrls.ios = "itms-beta://"; assert.deepEqual(validate(d5), []);
  });
  await test("refused: unknown top-level fields, a wrong schema, a duplicated or malformed mode", () => {
    refuses({ ...good(), remoteConfig: {} }, 'unknown field "remoteConfig"');
    refuses({ ...good(), schema: 2 }, "schema must be 1");
    refuses({ ...good(), disabledModes: ["Elimination", "Elimination"] }, 'lists "Elimination" twice');
    refuses({ ...good(), disabledModes: ["skim race"] }, "is not a GameModes name");
  });
  await test("refused: a message body with no title, a multi-line title", () => {
    refuses({ ...good(), message: { title: "", body: "Servers are down tonight." } }, "message.title is empty");
    refuses({ ...good(), message: { title: "a\nb", body: "" } }, "message.title must be one line");
    assert.deepEqual(validate({ ...good(), message: { title: "Maintenance", body: "Online play pauses at 02:00 UTC." } }), []);
  });
  console.log(`\n${passed} passed`);
})().catch((e) => { console.error("FAILED:", e.message || e); process.exit(1); });
