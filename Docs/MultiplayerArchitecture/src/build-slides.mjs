// Builds the LinkedIn slide deck (1080×1350, swipeable) from the curated Part I narrative.
//   src/build-slides.mjs -> build/slides.html -> WeasyPrint -> CosmicShore-Multiplayer-LinkedIn-Slides.pdf
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const diagramsDir = path.join(here, "diagrams");
const buildDir = path.join(root, "build");
const OUT = path.join(root, "CosmicShore-Multiplayer-LinkedIn-Slides.pdf");

const svg = (name) =>
  existsSync(path.join(diagramsDir, name + ".svg"))
    ? readFileSync(path.join(diagramsDir, name + ".svg"), "utf8").replace(/<\?xml[^>]*\?>/, "")
    : `<div style="color:#b3373b">[${name} missing]</div>`;

const slides = [
  { kind: "cover",
    title: `How we built<br><span class="accent">unbreakable</span> multiplayer`,
    body: `A plain-language tour of Cosmic Shore's online stack — Unity Netcode + Unity Gaming Services: the two-level session model, the decisions it hangs on, and the five bugs that taught us the most.`,
    swipe: "Swipe →" },

  { kind: "standard", kicker: "The challenge",
    title: "Multiplayer is really three hard questions",
    body: `Where does shared state live? Which thread is your cloud callback on when it returns? And what do you do when two clients disagree about who's in the party? Answer them well and it feels seamless — answer them badly and it's endlessly flaky.` },

  { kind: "diagram", kicker: "The core idea", title: "Split discovery from gameplay", diagram: "two-level-model",
    body: `A lobby-only <span class="hl">Presence Lobby</span> finds players cheaply; a Relay-backed <span class="hl">Party Session</span> runs the actual game. The invite is the bridge between them.` },

  { kind: "list", kicker: "Why two layers", title: "Cheap to discover, small to play",
    points: [
      `<b>Presence Lobby</b> — up to 100 players, no Relay, and it never disturbs an active NetworkManager.`,
      `<b>Invites need no host</b> — they ride per-player lobby properties, so anyone can invite anyone.`,
      `<b>Party Session</b> — Relay-backed, capped, and exists only for the people actually flying together.` ] },

  { kind: "standard", kicker: "Decision · 1",
    title: "Everyone hosts a party from the start",
    body: `<strong>EAGER “Always-InParty”:</strong> you create your Relay session the moment you reach the menu. So an invite becomes a simple <span class="hl">join</span> — not a fragile create-then-hand-off. An entire class of race-condition bugs simply disappears.` },

  { kind: "diagram", kicker: "The payoff", title: "An invite is just a join", diagram: "player-journey",
    body: `Because a real session already exists, accepting is “leave mine, join yours” — no session being built while another player waits to connect.` },

  { kind: "list", kicker: "Decisions · 2–4", title: "Three more things that keep it honest",
    points: [
      `<b>Single-writer SOAP</b> — one owner per piece of shared state; everyone else reads.`,
      `<b>A validated 7-state machine</b> — not a drift-prone scatter of boolean flags.`,
      `<b>.AsMainThread() at every cloud await</b>, and server-authoritative spawning.` ] },

  { kind: "code", kicker: "Bug that taught us · 1", fixed: true,
    title: "Your await came back<br>on the wrong thread",
    body: `UGS callbacks resume on the <strong>thread pool</strong>. Touch any Unity object there — even <code style="color:#ffcf8a">obj == null</code> — and it crashes. Worse: the obvious fix, <code style="color:#ffcf8a">UniTask.SwitchToMainThread()</code>, was a <span class="hl">no-op</span> on our version.`,
    code: `<span class="c">// back on Unity's main thread:</span>\nvar s = <span class="k">await</span> Multiplayer.Instance\n   .<span class="t">CreateSessionAsync</span>(opts)\n   .<span class="k">AsMainThread</span>();` },

  { kind: "diagram", kicker: "The fix", title: ".AsMainThread() everywhere", diagram: "threading-cascade",
    body: `A boundary helper built on Unity's own SynchronizationContext — plus a canary that screams if anyone forgets it.` },

  { kind: "code", kicker: "Bug · 2", fixed: true,
    title: "A singleton pinned to null —<br>forever",
    body: `Caching <code style="color:#ffcf8a">MultiplayerService.Instance</code> in a constructor that runs <strong>before</strong> UGS initialises pins <span class="hl">null</span> permanently. Resolve it lazily instead.`,
    code: `<span class="c">// ❌ ctor runs before UGS init → null forever</span>\n<span class="c">// ✅ resolve at use time:</span>\n<span class="k">private</span> <span class="t">IMultiplayerService</span> _svc =&gt;\n   MultiplayerService.Instance;` },

  { kind: "standard", kicker: "Bug · 3", fixed: true,
    title: "When two truths disagree",
    body: `After a player left, the host kept <span class="hl">flickering them back</span> every few seconds — a presence-lobby “hint” was overriding the authoritative session. Fix: pick one source of truth (the session) and make the hint defer to it.` },

  { kind: "standard", kicker: "Bug · 4", fixed: true,
    title: "Two ships and dead controls",
    body: `Leaving a party spawned a new vessel <strong>before</strong> the menu finished reloading — so the reload spawned a second one, and the first was orphaned with no controls. Object and scene lifecycles are different clocks; order them explicitly or you get orphans.` },

  { kind: "list", kicker: "Definition of done", title: "8 “unbreakable” criteria — every commit",
    points: [
      `No fatal failure · no stuck UI · no silent state divergence`,
      `Every transition reversible · idempotent retries (double-tap is safe)`,
      `3- and 4-player concurrent-invite tests green in Multiplayer Play Mode` ] },

  { kind: "list", kicker: "Honest limitations", title: "What we'd improve next",
    points: [
      `<b>Host-loss resilience</b> — today the host is a player; if they drop, the party ends.`,
      `<b>Prove 3–4-player parties</b> — close the second-joiner edge case.`,
      `<b>Push over polling</b> — event-driven invites to cut latency + rate-limit churn.`,
      `<b>Production telemetry</b> — measure party success and join latency in shipped builds.` ] },

  { kind: "list", kicker: "Takeaways", title: "5 reusable lessons",
    points: [
      `Delete a fragile transition by making its precondition <b>always true</b>.`,
      `Verify your async library's thread-switch — <b>don't trust it</b>.`,
      `Never cache a singleton before its init can complete.`,
      `One authoritative source; everything else <b>defers</b>.`,
      `Order object vs. scene lifecycles explicitly, or get orphans.` ] },

  { kind: "outro", kicker: "Cosmic Shore",
    title: "The party game<br>for pilots",
    body: `Built by <strong>Froglet Inc.</strong> on Unity 6, Netcode for GameObjects & Unity Gaming Services.`,
    tag: "Found this useful? Follow along for more engineering deep-dives." },
];

const N = slides.length;
const foot = (i) => `<div class="foot"><div class="bar"><span class="s1"></span><span class="s2"></span><span class="s3"></span></div><div>${String(i + 1).padStart(2, "0")} / ${N}</div></div>`;
const chip = (s) => (s.fixed ? ` <span class="badge-fixed">🟢 Fixed</span>` : "");

function renderSlide(s, i) {
  const head = `<div class="head"><div class="kicker">${s.kicker || ""}</div><div class="logo">COSMIC&nbsp;SHORE</div></div>`;
  if (s.kind === "cover") {
    return `<section class="slide cover"><div class="main">
      <div class="kicker" style="margin-bottom:24px">Cosmic Shore · Engineering</div>
      <h2 class="title">${s.title}</h2><p class="body">${s.body}</p><div class="swipe">${s.swipe}</div></div>${foot(i)}</section>`;
  }
  if (s.kind === "outro") {
    return `<section class="slide outro">${head}<div class="main">
      <h2 class="title">${s.title}</h2><p class="body">${s.body}</p><div class="tagchip">${s.tag}</div></div>${foot(i)}</section>`;
  }
  const title = s.title ? `<h2 class="title">${s.title}${chip(s)}</h2>` : "";
  const body = s.body ? `<p class="body">${s.body}</p>` : "";
  let main;
  if (s.kind === "diagram") main = `<div class="main">${title}<div class="fig-card">${svg(s.diagram)}</div>${body}</div>`;
  else if (s.kind === "code") main = `<div class="main">${title}${body}<div class="code">${s.code}</div></div>`;
  else if (s.kind === "list") main = `<div class="main">${title}<ul class="points">${s.points.map((p) => `<li>${p}</li>`).join("")}</ul></div>`;
  else main = `<div class="main">${title}${body}</div>`;
  return `<section class="slide">${head}${main}${foot(i)}</section>`;
}

const fontsCss = existsSync(path.join(here, "fonts.css")) ? readFileSync(path.join(here, "fonts.css"), "utf8") : "";
const themeCss = readFileSync(path.join(here, "linkedin-theme.css"), "utf8");
const html = `<!doctype html><html><head><meta charset="utf-8"><style>${fontsCss}</style><style>${themeCss}</style></head>
<body>${slides.map(renderSlide).join("\n")}</body></html>`;

if (!existsSync(buildDir)) mkdirSync(buildDir, { recursive: true });
const htmlPath = path.join(buildDir, "slides.html");
writeFileSync(htmlPath, html, "utf8");
console.log(`HTML written: ${path.relative(root, htmlPath)} — ${N} slides`);
try {
  execFileSync("weasyprint", [htmlPath, OUT, "-q"], { stdio: ["ignore", "inherit", "inherit"] });
  console.log(`PDF written:  ${path.relative(root, OUT)}`);
} catch (e) { console.error("WeasyPrint failed:", e.message); process.exitCode = 1; }
