# Multiplayer / Netcode / UGS Architecture — PDF dossier

A branded technical PDF documenting Cosmic Shore's entire online stack: Unity Netcode for
GameObjects, Unity Gaming Services (Auth · Sessions · Lobby · Relay · Friends), the two-level
Presence Lobby + Party Session model, server-authoritative vessel spawning, the `.AsMainThread()`
threading contract, the SOAP single-writer data flow, the full bug catalogue, resilience matrix,
tests, and diagnostics.

Structured in two passes: **Part I — Curated Overview** (accessible) and **Part II — Comprehensive
Deep-Dive** (exhaustive). Synthesised from the canonical engineering docs under `Docs/` and the
source under `Assets/_Scripts/`.

**Output:** `CosmicShore-Multiplayer-Netcode-Architecture.pdf` (~69 pages).

## Regenerate

Requirements: Node 18+, Python 3.9+ with [WeasyPrint](https://weasyprint.org/).

```bash
npm install            # puppeteer (Chromium), @mermaid-js/mermaid-cli, markdown-it, highlight.js
pip install weasyprint
npm run all            # render diagrams (mmd → svg) then build the PDF
# equivalently:
node src/render-diagrams.mjs
node src/build.mjs
```

## Layout

```
src/
├── content/        # Markdown source, one file per section (also reusable as article text)
├── diagrams/       # *.mmd Mermaid sources → *.svg (rendered, git-ignored)
├── theme.css       # branded WeasyPrint paged-media theme
├── fonts.css       # inlined Space Grotesk / Inter / JetBrains Mono (base64)
├── mermaid-config.json / puppeteer-config.json
├── render-diagrams.mjs
└── build.mjs       # markdown-it (+ containers, highlight.js) → HTML → WeasyPrint → PDF
```

## Editing

- **Prose / structure:** edit `src/content/*.md`. Section order and the part dividers are defined in
  the `manifest` array in `src/build.mjs`.
- **Diagrams:** edit `src/diagrams/*.mmd`, then `npm run all`. Mermaid runs with `htmlLabels:false`
  so the SVGs use `<text>` (no `foreignObject`) and render in WeasyPrint.
- **Look & feel:** `src/theme.css`. Call-out containers: `decision`, `bug`, `insight`, `pitfall`;
  layout containers: `figure`, `divider` (via build), `lead`, `cols`.
