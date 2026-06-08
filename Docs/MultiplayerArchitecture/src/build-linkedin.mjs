// Builds the LinkedIn carousel PDF (1080×1350, swipeable) from the same diagrams + fonts.
//   src/build-linkedin.mjs -> build/linkedin.html -> WeasyPrint -> CosmicShore-Multiplayer-LinkedIn-Carousel.pdf
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const diagramsDir = path.join(here, "diagrams");
const buildDir = path.join(root, "build");
const OUT = path.join(root, "CosmicShore-Multiplayer-LinkedIn-Carousel.pdf");

const svg = (name) =>
  existsSync(path.join(diagramsDir, name + ".svg"))
    ? readFileSync(path.join(diagramsDir, name + ".svg"), "utf8").replace(/<\?xml[^>]*\?>/, "")
    : `<div style="color:#b3373b">[${name} missing]</div>`;

const slides = [
  { kind: "cover",
    title: `Building <span class="accent">unbreakable</span><br>multiplayer`,
    body: `How Cosmic Shore wires Unity Netcode + Unity Gaming Services — the architecture, the four decisions it hangs on, and the bugs that taught us the most.`,
    swipe: "Swipe →" },

  { kind: "standard", kicker: "The core idea",
    title: "Split “discoverable” from “connected”",
    body: `Two sessions per player: a cheap, global <span class="hl">Presence Lobby</span> for discovery and invites, and a Relay-backed <span class="hl">Party Session</span> for the people actually flying together.` },

  { kind: "diagram", kicker: "Two-level model", title: "Discovery vs. gameplay", diagram: "two-level-model",
    body: `Lobby-only discovery costs no Relay and never disturbs Netcode. The invite payload is the bridge between the two layers.` },

  { kind: "standard", kicker: "Decision · 1",
    title: "Everyone hosts a party from the start",
    body: `<strong>EAGER “Always-InParty”:</strong> create your Relay session the moment you reach the menu. Now an invite is a simple <span class="hl">JOIN</span> — not a fragile create-then-handoff. An entire class of race-condition bugs simply disappears.` },

  { kind: "diagram", kicker: "The payoff", title: "An invite becomes a join", diagram: "player-journey",
    body: `Because a real session already exists, accepting is just “leave mine, join yours.”` },

  { kind: "code", kicker: "Bug that taught us · 1",
    title: "Your await came back<br>on the wrong thread",
    body: `UGS callbacks resume on the <strong>thread pool</strong>. Touch any Unity object there — even <code style="color:#ffcf8a">obj == null</code> — and it crashes. Worse: the obvious fix, <code style="color:#ffcf8a">UniTask.SwitchToMainThread()</code>, was a <span class="hl">no-op</span> on our version.`,
    code: `<span class="c">// back on Unity's main thread:</span>\nvar s = <span class="k">await</span> Multiplayer.Instance\n   .<span class="t">CreateSessionAsync</span>(opts)\n   .<span class="k">AsMainThread</span>();` },

  { kind: "diagram", kicker: "The fix", title: ".AsMainThread() at every cloud await", diagram: "threading-cascade",
    body: `A boundary helper built on Unity’s own SynchronizationContext — plus a canary that screams if anyone forgets it.` },

  { kind: "code", kicker: "Bug that taught us · 2",
    title: "A singleton pinned to null —<br>forever",
    body: `Caching <code style="color:#ffcf8a">MultiplayerService.Instance</code> in a constructor that runs <strong>before</strong> UGS initialises pins <span class="hl">null</span> permanently. Resolve it lazily instead.`,
    code: `<span class="c">// ❌ ctor runs before UGS init → null forever</span>\n<span class="c">// ✅ resolve at use time:</span>\n<span class="k">private</span> <span class="t">IMultiplayerService</span> _svc =&gt;\n   MultiplayerService.Instance;` },

  { kind: "standard", kicker: "Bug that taught us · 3",
    title: "When two truths disagree",
    body: `A player who left kept <span class="hl">flickering back</span> on the host every few seconds. A presence-lobby “hint” was overriding the authoritative session. <strong>Fix:</strong> pick one source of truth — the session — and make the hint defer to it.` },

  { kind: "list", kicker: "What made it stick",
    title: "Three disciplines",
    points: [
      `<b>Single-writer SOAP</b> — one owner per piece of shared state; everyone else reads.`,
      `<b>A validated state machine</b> — 7 explicit party states, not a drift-prone scatter of booleans.`,
      `<b>Classify every failure</b> — each catch maps to a named recovery: benign · rate-limit · gone · transient.` ] },

  { kind: "list", kicker: "Definition of done",
    title: "8 “unbreakable” criteria,<br>checked every commit",
    points: [
      `No fatal failure · no stuck UI · no silent state divergence`,
      `Every transition reversible · idempotent retries (double-tap is safe)`,
      `3-player & 4-player concurrent-invite tests green in Multiplayer Play Mode` ] },

  { kind: "list", kicker: "Takeaways",
    title: "5 reusable lessons",
    points: [
      `Delete a fragile transition by making its precondition <b>always true</b>.`,
      `Verify your async library’s thread-switch — <b>don’t trust it</b>.`,
      `Never cache a singleton before its init can complete.`,
      `One authoritative source; everything else <b>defers</b>.`,
      `Order object vs. scene lifecycles explicitly, or get orphans.` ] },

  { kind: "outro", kicker: "Cosmic Shore",
    title: "The party game<br>for pilots",
    body: `Built by <strong>Froglet Inc.</strong> on Unity 6, Netcode for GameObjects & Unity Gaming Services.`,
    tag: "Full 69-page technical dossier available on request" },
];

const N = slides.length;
const foot = (i) => `<div class="foot"><div class="bar"><span class="s1"></span><span class="s2"></span><span class="s3"></span></div><div>${String(i + 1).padStart(2, "0")} / ${N}</div></div>`;

function renderSlide(s, i) {
  const head = `<div class="head"><div class="kicker">${s.kicker || ""}</div><div class="logo">COSMIC&nbsp;SHORE</div></div>`;
  let main = "";
  if (s.kind === "cover") {
    main = `<div class="main"><div class="kicker" style="margin-bottom:24px">Cosmic Shore · Engineering</div>
      <h2 class="title">${s.title}</h2><p class="body">${s.body}</p><div class="swipe">${s.swipe}</div></div>`;
    return `<section class="slide cover">${main}${foot(i)}</section>`;
  }
  if (s.kind === "outro") {
    main = `<div class="main"><h2 class="title">${s.title}</h2><p class="body">${s.body}</p><div class="tagchip">${s.tag}</div></div>`;
    return `<section class="slide outro">${head}${main}${foot(i)}</section>`;
  }
  const title = s.title ? `<h2 class="title">${s.title}</h2>` : "";
  const body = s.body ? `<p class="body">${s.body}</p>` : "";
  if (s.kind === "diagram")
    main = `<div class="main">${title}<div class="fig-card">${svg(s.diagram)}</div>${body}</div>`;
  else if (s.kind === "code")
    main = `<div class="main">${title}${body}<div class="code">${s.code}</div></div>`;
  else if (s.kind === "list")
    main = `<div class="main">${title}<ul class="points">${s.points.map((p) => `<li>${p}</li>`).join("")}</ul></div>`;
  else main = `<div class="main">${title}${body}</div>`;
  return `<section class="slide">${head}${main}${foot(i)}</section>`;
}

const fontsCss = existsSync(path.join(here, "fonts.css")) ? readFileSync(path.join(here, "fonts.css"), "utf8") : "";
const themeCss = readFileSync(path.join(here, "linkedin-theme.css"), "utf8");
const html = `<!doctype html><html><head><meta charset="utf-8"><style>${fontsCss}</style><style>${themeCss}</style></head>
<body>${slides.map(renderSlide).join("\n")}</body></html>`;

if (!existsSync(buildDir)) mkdirSync(buildDir, { recursive: true });
const htmlPath = path.join(buildDir, "linkedin.html");
writeFileSync(htmlPath, html, "utf8");
console.log(`HTML written: ${path.relative(root, htmlPath)} — ${N} slides`);
try {
  execFileSync("weasyprint", [htmlPath, OUT, "-q"], { stdio: ["ignore", "inherit", "inherit"] });
  console.log(`PDF written:  ${path.relative(root, OUT)}`);
} catch (e) {
  console.error("WeasyPrint failed:", e.message);
  process.exitCode = 1;
}
