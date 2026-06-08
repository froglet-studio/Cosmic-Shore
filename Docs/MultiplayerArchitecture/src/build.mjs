// Assembles the Cosmic Shore Multiplayer/Netcode architecture document.
//   markdown (src/content/*.md)  +  diagrams (src/diagrams/*.svg)
//   -> build/document.html  -> WeasyPrint -> <root>/CosmicShore-Multiplayer-Netcode-Architecture.pdf
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import MarkdownIt from "markdown-it";
import container from "markdown-it-container";
import anchor from "markdown-it-anchor";
import hljs from "highlight.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const contentDir = path.join(here, "content");
const diagramsDir = path.join(here, "diagrams");
const buildDir = path.join(root, "build");
const OUT_PDF = path.join(root, "CosmicShore-Multiplayer-Netcode-Architecture.pdf");

const DATE = "June 2026";

/* --------------------------------------------------------- markdown-it set-up */
const md = new MarkdownIt({ html: true, linkify: true, typographer: true, breaks: false });

// global unique slugs across the whole document
const usedSlugs = new Set();
const slugify = (s) => {
  let base = String(s).toLowerCase().trim()
    .replace(/<[^>]+>/g, "")
    .replace(/[^\w\s-]/g, "")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "") || "sec";
  let slug = base, n = 2;
  while (usedSlugs.has(slug)) slug = `${base}-${n++}`;
  usedSlugs.add(slug);
  return slug;
};

let currentHeadings = [];
md.use(anchor, {
  slugify,
  tabIndex: false,
  callback: (token, info) => {
    const level = Number(token.tag.slice(1));
    if (level <= 2) currentHeadings.push({ level, id: info.slug, title: info.title });
  },
});

// every content <h1> becomes a section header (page break + running header)
const defHeadingOpen = md.renderer.rules.heading_open || ((t, i, o, e, s) => s.renderToken(t, i, o));
md.renderer.rules.heading_open = (tokens, idx, options, env, self) => {
  if (tokens[idx].tag === "h1") tokens[idx].attrJoin("class", "section");
  return defHeadingOpen(tokens, idx, options, env, self);
};

// fenced code with highlight.js + language chip
md.renderer.rules.fence = (tokens, idx) => {
  const token = tokens[idx];
  const lang = (token.info || "").trim().split(/\s+/)[0];
  let body;
  if (lang && hljs.getLanguage(lang)) {
    try { body = hljs.highlight(token.content, { language: lang, ignoreIllegal: true }).value; }
    catch { body = md.utils.escapeHtml(token.content); }
  } else { body = md.utils.escapeHtml(token.content); }
  const chip = lang ? `<span class="code-lang">${lang}</span>` : "";
  return `<div class="code-wrap">${chip}<pre><code class="hljs language-${lang}">${body}</code></pre></div>\n`;
};

/* ------------------------------------------------------------------ containers */
const firstWordStripped = (info, name) => info.trim().replace(new RegExp("^" + name + "\\s*"), "");

function callout(name, label) {
  md.use(container, name, {
    render(tokens, idx) {
      if (tokens[idx].nesting === 1) {
        let title = firstWordStripped(tokens[idx].info, name);
        let badge = "";
        const m = title.match(/\{(open|fixed|investigating|deferred)\}\s*$/i);
        if (m) {
          const s = m[1].toLowerCase();
          const lbl = { open: "🔴 Open", fixed: "🟢 Fixed", investigating: "🟡 Investigating", deferred: "⚪ Deferred" }[s];
          badge = ` <span class="badge ${s}">${lbl}</span>`;
          title = title.replace(m[0], "").trim();
        }
        const titleHtml = title ? `<div class="callout-title">${md.renderInline(title)}${badge}</div>` : "";
        return `<aside class="callout ${name}"><div class="callout-label">${label}${title ? "" : badge}</div>${titleHtml}<div class="callout-body">`;
      }
      return `</div></aside>\n`;
    },
  });
}
callout("decision", "Design decision");
callout("bug", "Bug → Root cause → Fix");
callout("insight", "Key insight");
callout("pitfall", "Pitfall / Anti-pattern");

md.use(container, "lead", {
  render: (t, i) => (t[i].nesting === 1 ? `<div class="lead">` : `</div>\n`),
});
md.use(container, "cols", {
  render: (t, i) => (t[i].nesting === 1 ? `<div class="two-col">` : `</div>\n`),
});

md.use(container, "figure", {
  render(tokens, idx) {
    if (tokens[idx].nesting === 1) {
      const name = firstWordStripped(tokens[idx].info, "figure").trim();
      const svgPath = path.join(diagramsDir, name + ".svg");
      const svg = existsSync(svgPath)
        ? readFileSync(svgPath, "utf8").replace(/<\?xml[^>]*\?>/, "").replace(/<!DOCTYPE[^>]*>/, "")
        : `<div style="color:#b3373b;padding:20px;font-family:sans-serif">[diagram “${name}” not rendered — run npm run diagrams]</div>`;
      return `<figure class="diagram">${svg}<figcaption>`;
    }
    return `</figcaption></figure>\n`;
  },
});

/* ----------------------------------------------------------------- the manifest */
const manifest = [
  { type: "divider", eyebrow: "Part I", title: "The Curated Overview", num: "I",
    body: "A fast, narrative tour of the online stack — the mental model, the load-bearing design decisions, and the five most instructive bugs. Readable end-to-end in ~15 minutes; the deep mechanics live in Part II." },
  { type: "section", part: "I", file: "01-executive-summary.md" },
  { type: "section", part: "I", file: "02-system-context.md" },
  { type: "section", part: "I", file: "03-two-level-model.md" },
  { type: "section", part: "I", file: "04-player-journey.md" },
  { type: "section", part: "I", file: "05-key-decisions.md" },
  { type: "section", part: "I", file: "06-highlight-bugs.md" },
  { type: "section", part: "I", file: "07-current-state.md" },

  { type: "divider", eyebrow: "Part II", title: "The Comprehensive Deep-Dive", num: "II",
    body: "Every subsystem, end to end: the UGS surface, presence lobby, party session and its extracted services, the invite protocol, the Netcode spawn pipeline, threading, SOAP, resilience, the full bug catalogue, testing, and diagnostics." },
  { type: "section", part: "II", file: "10-tech-stack.md" },
  { type: "section", part: "II", file: "11-architecture-layers.md" },
  { type: "section", part: "II", file: "12-auth-bootstrap.md" },
  { type: "section", part: "II", file: "13-presence-lobby.md" },
  { type: "section", part: "II", file: "14-party-session.md" },
  { type: "section", part: "II", file: "15-invite-flow.md" },
  { type: "section", part: "II", file: "16-spawn-pipeline.md" },
  { type: "section", part: "II", file: "17-game-flow.md" },
  { type: "section", part: "II", file: "18-friends.md" },
  { type: "section", part: "II", file: "19-threading.md" },
  { type: "section", part: "II", file: "20-soap-architecture.md" },
  { type: "section", part: "II", file: "21-resilience.md" },
  { type: "section", part: "II", file: "22-bug-catalog.md" },
  { type: "section", part: "II", file: "23-testing.md" },
  { type: "section", part: "II", file: "24-diagnostics.md" },
  { type: "section", part: "II", file: "25-decisions-ledger.md" },
  { type: "section", part: "II", file: "26-future-roadmap.md" },

  { type: "divider", eyebrow: "Appendices", title: "Reference", num: "A",
    body: "File and class index, glossary of networking and project terms, and the UGS SDK call map." },
  { type: "section", part: "A", file: "90-appendix-files.md" },
  { type: "section", part: "A", file: "91-appendix-glossary.md" },
  { type: "section", part: "A", file: "92-appendix-ugs-map.md" },
];

/* -------------------------------------------------------------------- assemble */
const esc = (s) => md.utils.escapeHtml(s);
const tocParts = [];
let bodyHtml = "";
let missing = 0;

for (const item of manifest) {
  if (item.type === "divider") {
    tocParts.push({ label: `${item.eyebrow} — ${item.title}`, items: [] });
    bodyHtml += `
<section class="divider">
  <div class="divider-num">${esc(item.num)}</div>
  <div class="divider-inner">
    <div class="eyebrow">${esc(item.eyebrow)}</div>
    <div class="divider-title">${esc(item.title)}</div>
    <div class="divider-body">${esc(item.body)}</div>
    <div class="accent-bar"><span class="s1"></span><span class="s2"></span><span class="s3"></span></div>
  </div>
</section>\n`;
    continue;
  }
  const fp = path.join(contentDir, item.file);
  if (!existsSync(fp)) { console.warn(`  ! missing content: ${item.file}`); missing++; continue; }
  currentHeadings = [];
  const html = md.render(readFileSync(fp, "utf8"));
  bodyHtml += `\n<!-- ${item.file} -->\n${html}\n`;
  const part = tocParts[tocParts.length - 1];
  if (part) for (const h of currentHeadings) part.items.push(h);
}

/* ------------------------------------------------------------------------- TOC */
let tocHtml = `<section class="toc"><h1 class="section">Contents</h1><ol>`;
for (const part of tocParts) {
  if (part.items.length === 0) continue;
  tocHtml += `<li class="toc-part">${esc(part.label)}</li>`;
  for (const h of part.items) {
    const cls = h.level === 1 ? "toc-link" : "toc-link sub";
    tocHtml += `<li><a class="${cls}" href="#${h.id}"><span class="t">${md.renderInline(h.title)}</span><span class="dots"></span></a></li>`;
  }
}
tocHtml += `</ol></section>`;

/* ----------------------------------------------------------------------- cover */
const coverHtml = `
<section class="cover">
  <div class="cover-inner">
    <div class="cover-kicker">Cosmic Shore · Architecture Dossier</div>
    <h1>Multiplayer, Netcode<br>&amp; <span class="accent">Live Services</span></h1>
    <div class="subtitle">Unity Netcode for GameObjects and Unity Gaming Services — the two-level
      Presence&nbsp;Lobby + Party&nbsp;Session model, server-authoritative vessel spawning, the
      main-thread affinity contract, and the design decisions, bugs, and fixes behind a party
      system hardened toward “unbreakable”.</div>
    <div class="accent-bar"><span class="s1"></span><span class="s2"></span><span class="s3"></span><span class="s4"></span></div>
  </div>
  <div class="cover-foot">
    <div>
      <span class="meta-label">Project</span><strong>Cosmic Shore</strong> — Froglet Inc.<br>
      <span class="meta-label" style="margin-top:8px">Audience</span>CTO review · engineering reference · technical deep-dive
    </div>
    <div style="text-align:right">
      <span class="meta-label">Scope</span>Party · Presence · Friends · Netcode · UGS<br>
      <span class="meta-label" style="margin-top:8px">Revised</span><strong>${DATE}</strong>
    </div>
  </div>
</section>`;

/* ------------------------------------------------------------ how-to-read page */
const howtoHtml = `
<section class="howto">
  <div class="sec-eyebrow">Orientation</div>
  <h1 class="section">How to read this document</h1>
  <div class="lead">This dossier is written in two passes over the same system, so it serves both a
    quick executive read and a deep engineering reference without forcing either audience through
    the other’s level of detail.</div>
  <div class="howto-grid">
    <div class="howto-card">
      <div class="who">Start here</div>
      <h3>Part I — Curated Overview</h3>
      <p>The mental model in plain language: the two-session architecture, the player journey, the
      handful of decisions everything hangs on, and the bugs that taught us the most. Skimmable in
      ~15&nbsp;minutes.</p>
    </div>
    <div class="howto-card two">
      <div class="who">Go deep</div>
      <h3>Part II — Comprehensive Deep-Dive</h3>
      <p>Subsystem by subsystem, with the exact classes, NetworkVariables, RPCs, retry policies,
      threading rules, the full B-series bug catalogue, the resilience matrix, tests, and
      diagnostics.</p>
    </div>
  </div>
  <p><strong>Conventions.</strong> <span class="tag">monospace</span> denotes a file, class, or
  API. Coloured call-outs flag <em>design decisions</em>, <em>bug→fix</em> stories,
  <em>key insights</em>, and <em>pitfalls</em>. Status pills use the project legend:
  <span class="badge open">🔴 Open</span> <span class="badge investigating">🟡 Investigating</span>
  <span class="badge fixed">🟢 Fixed</span> <span class="badge deferred">⚪ Deferred</span>.
  Figures are redrawn from the canonical engineering docs under <span class="fileref">Docs/</span>.</p>
</section>`;

/* -------------------------------------------------------------- final document */
const fontsCss = existsSync(path.join(here, "fonts.css")) ? readFileSync(path.join(here, "fonts.css"), "utf8") : "";
const themeCss = readFileSync(path.join(here, "theme.css"), "utf8");

const docHtml = `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>Cosmic Shore — Multiplayer, Netcode &amp; Live Services Architecture</title>
<style>${fontsCss}</style>
<style>${themeCss}</style>
</head><body>
${coverHtml}
${howtoHtml}
${tocHtml}
${bodyHtml}
</body></html>`;

if (!existsSync(buildDir)) mkdirSync(buildDir, { recursive: true });
const htmlPath = path.join(buildDir, "document.html");
writeFileSync(htmlPath, docHtml, "utf8");
console.log(`HTML written: ${path.relative(root, htmlPath)} (${(docHtml.length / 1024).toFixed(0)} KB)` + (missing ? `  [${missing} content files missing]` : ""));

/* ---------------------------------------------------------------- render PDF */
function renderWeasy() {
  execFileSync("weasyprint", [htmlPath, OUT_PDF, "-q"], { stdio: ["ignore", "inherit", "inherit"] });
}
try {
  renderWeasy();
  console.log(`PDF written:  ${path.relative(root, OUT_PDF)}`);
} catch (e) {
  console.error("WeasyPrint failed:", e.message);
  process.exitCode = 1;
}
