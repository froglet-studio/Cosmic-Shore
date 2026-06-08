// Renders every Mermaid source (src/diagrams/*.mmd) to a sibling SVG.
// Hand-authored SVGs (no matching .mmd) are left untouched.
import { readdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const diagramsDir = path.join(here, "diagrams");
const mmdc = path.join(here, "..", "node_modules", ".bin", "mmdc");
const mermaidCfg = path.join(here, "mermaid-config.json");
const puppeteerCfg = path.join(here, "puppeteer-config.json");

const files = (await readdir(diagramsDir)).filter((f) => f.endsWith(".mmd")).sort();
if (files.length === 0) {
  console.log("No .mmd diagrams found.");
  process.exit(0);
}

let ok = 0;
let fail = 0;
for (const f of files) {
  const input = path.join(diagramsDir, f);
  const output = path.join(diagramsDir, f.replace(/\.mmd$/, ".svg"));
  try {
    execFileSync(
      "node",
      [mmdc, "-i", input, "-o", output, "-c", mermaidCfg, "-p", puppeteerCfg, "-b", "transparent"],
      { stdio: ["ignore", "ignore", "pipe"] }
    );
    if (existsSync(output)) {
      console.log(`  ✓ ${f} → ${path.basename(output)}`);
      ok++;
    } else {
      console.error(`  ✗ ${f} produced no SVG`);
      fail++;
    }
  } catch (e) {
    console.error(`  ✗ ${f}: ${String(e.stderr || e.message).split("\n").slice(-3).join(" ")}`);
    fail++;
  }
}
console.log(`\nDiagrams: ${ok} rendered, ${fail} failed.`);
if (fail > 0) process.exitCode = 1;
