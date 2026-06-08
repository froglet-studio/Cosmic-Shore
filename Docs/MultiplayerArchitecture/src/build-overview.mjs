// Builds the standalone LinkedIn "Curated Overview" PDF — Part I of the dossier
// (cover + the 7 curated sections) in the same branded report style.
//   src/build-overview.mjs -> build/overview.html -> WeasyPrint -> CosmicShore-Multiplayer-LinkedIn-Overview.pdf
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
const OUT_PDF = path.join(root, "CosmicShore-Multiplayer-LinkedIn-Overview.pdf");
const DATE = "June 2026";

/* --- markdown-it (same setup/containers as the dossier) --- */
const md = new MarkdownIt({ html: true, linkify: true, typographer: true });
const usedSlugs = new Set();
const slugify = (s) => {
  let base = String(s).toLowerCase().trim().replace(/<[^>]+>/g, "").replace(/[^\w\s-]/g, "")
    .replace(/\s+/g, "-").replace(/-+/g, "-").replace(/^-|-$/g, "") || "sec";
  let slug = base, n = 2;
  while (usedSlugs.has(slug)) slug = `${base}-${n++}`;
  usedSlugs.add(slug);
  return slug;
};
let currentHeadings = [];
md.use(anchor, { slugify, tabIndex: false,
  callback: (t, info) => { const l = Number(t.tag.slice(1)); if (l <= 2) currentHeadings.push({ level: l, id: info.slug, title: info.title }); } });
const defHeadingOpen = md.renderer.rules.heading_open || ((t, i, o, e, s) => s.renderToken(t, i, o));
md.renderer.rules.heading_open = (tokens, idx, options, env, self) => {
  if (tokens[idx].tag === "h1") tokens[idx].attrJoin("class", "section");
  return defHeadingOpen(tokens, idx, options, env, self);
};
md.renderer.rules.fence = (tokens, idx) => {
  const token = tokens[idx];
  const lang = (token.info || "").trim().split(/\s+/)[0];
  let body;
  if (lang && hljs.getLanguage(lang)) { try { body = hljs.highlight(token.content, { language: lang, ignoreIllegal: true }).value; } catch { body = md.utils.escapeHtml(token.content); } }
  else body = md.utils.escapeHtml(token.content);
  const chip = lang ? `<span class="code-lang">${lang}</span>` : "";
  return `<div class="code-wrap">${chip}<pre><code class="hljs language-${lang}">${body}</code></pre></div>\n`;
};
const firstWordStripped = (info, name) => info.trim().replace(new RegExp("^" + name + "\\s*"), "");
function callout(name, label) {
  md.use(container, name, { render(tokens, idx) {
    if (tokens[idx].nesting === 1) {
      let title = firstWordStripped(tokens[idx].info, name), badge = "";
      const m = title.match(/\{(open|fixed|investigating|deferred)\}\s*$/i);
      if (m) { const s = m[1].toLowerCase();
        badge = ` <span class="badge ${s}">${{ open: "🔴 Open", fixed: "🟢 Fixed", investigating: "🟡 Investigating", deferred: "⚪ Deferred" }[s]}</span>`;
        title = title.replace(m[0], "").trim(); }
      const titleHtml = title ? `<div class="callout-title">${md.renderInline(title)}${badge}</div>` : "";
      return `<aside class="callout ${name}"><div class="callout-label">${label}${title ? "" : badge}</div>${titleHtml}<div class="callout-body">`;
    }
    return `</div></aside>\n`;
  } });
}
callout("decision", "Design decision");
callout("bug", "Bug → Root cause → Fix");
callout("insight", "Key insight");
callout("pitfall", "Pitfall / Anti-pattern");
md.use(container, "lead", { render: (t, i) => (t[i].nesting === 1 ? `<div class="lead">` : `</div>\n`) });
md.use(container, "cols", { render: (t, i) => (t[i].nesting === 1 ? `<div class="two-col">` : `</div>\n`) });
md.use(container, "figure", { render(tokens, idx) {
  if (tokens[idx].nesting === 1) {
    const name = firstWordStripped(tokens[idx].info, "figure").trim();
    const p = path.join(diagramsDir, name + ".svg");
    const s = existsSync(p) ? readFileSync(p, "utf8").replace(/<\?xml[^>]*\?>/, "").replace(/<!DOCTYPE[^>]*>/, "")
      : `<div style="color:#b3373b;padding:20px">[diagram “${name}” not rendered]</div>`;
    return `<figure class="diagram">${s}<figcaption>`;
  }
  return `</figcaption></figure>\n`;
} });

/* --- Part I sections only --- */
const sections = [
  "01-executive-summary.md", "02-system-context.md", "03-two-level-model.md",
  "04-player-journey.md", "05-key-decisions.md", "06-highlight-bugs.md", "07-current-state.md",
];
let body = "", headings = [];
for (const file of sections) {
  const fp = path.join(contentDir, file);
  if (!existsSync(fp)) { console.warn("  ! missing", file); continue; }
  currentHeadings = [];
  body += `\n<!-- ${file} -->\n` + md.render(readFileSync(fp, "utf8")) + "\n";
  headings.push(...currentHeadings);
}

/* --- compact TOC --- */
let toc = `<section class="toc"><h1 class="section">Contents</h1><ol>`;
for (const h of headings) {
  const cls = h.level === 1 ? "toc-link" : "toc-link sub";
  toc += `<li><a class="${cls}" href="#${h.id}"><span class="t">${md.renderInline(h.title)}</span><span class="dots"></span></a></li>`;
}
toc += `</ol></section>`;

/* --- cover (LinkedIn-friendly hook) --- */
const cover = `
<section class="cover">
  <div class="cover-inner">
    <div class="cover-kicker">Cosmic Shore · Curated Technical Overview</div>
    <h1>How we built<br><span class="accent">unbreakable</span> multiplayer</h1>
    <div class="subtitle">A plain-language tour of the online stack — Unity Netcode for GameObjects +
      Unity Gaming Services: the two-level Presence&nbsp;+&nbsp;Party session model, the handful of
      decisions everything hangs on, and the five bugs that taught us the most.</div>
    <div class="accent-bar"><span class="s1"></span><span class="s2"></span><span class="s3"></span><span class="s4"></span></div>
  </div>
  <div class="cover-foot">
    <div><span class="meta-label">Project</span><strong>Cosmic Shore</strong> — Froglet Inc.<br>
      <span class="meta-label" style="margin-top:8px">Read time</span>~15 minutes</div>
    <div style="text-align:right"><span class="meta-label">Topics</span>Party · Presence · Netcode · UGS<br>
      <span class="meta-label" style="margin-top:8px">Published</span><strong>${DATE}</strong></div>
  </div>
</section>`;

/* --- assemble (override the internal footer label for a public doc) --- */
const fontsCss = existsSync(path.join(here, "fonts.css")) ? readFileSync(path.join(here, "fonts.css"), "utf8") : "";
const themeCss = readFileSync(path.join(here, "theme.css"), "utf8");
const overrideCss = `@page { @bottom-left { content: "Cosmic Shore · Froglet Inc."; } }`;
const html = `<!doctype html><html lang="en"><head><meta charset="utf-8">
<title>Cosmic Shore — Multiplayer &amp; Netcode: A Curated Overview</title>
<style>${fontsCss}</style><style>${themeCss}</style><style>${overrideCss}</style>
</head><body>${cover}${toc}${body}</body></html>`;

if (!existsSync(buildDir)) mkdirSync(buildDir, { recursive: true });
const htmlPath = path.join(buildDir, "overview.html");
writeFileSync(htmlPath, html, "utf8");
console.log(`HTML written: ${path.relative(root, htmlPath)}`);
try {
  execFileSync("weasyprint", [htmlPath, OUT_PDF, "-q"], { stdio: ["ignore", "inherit", "inherit"] });
  console.log(`PDF written:  ${path.relative(root, OUT_PDF)}`);
} catch (e) { console.error("WeasyPrint failed:", e.message); process.exitCode = 1; }
