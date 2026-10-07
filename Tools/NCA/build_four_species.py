"""Build the Four Swarm Species page for the Living Ecology Lab: one self-contained HTML (GIFs inlined as data URIs).

    python Tools/NCA/build_four_species.py            # writes Tools/NCA/results/four_species.html

The numbers are quoted from each species' NOTE.md (results/posinfo2, results/emergent, results/arms) and from
DISCOVERIES.md "The four swarm species". Re-run after a species republishes. The page has a #top bar so
Tools/Ecology/common/build_lab.py can insert its back link.
"""
from __future__ import annotations

import base64
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "results", "four_species.html")


def gif(rel: str) -> str:
    with open(os.path.join(HERE, rel), "rb") as fh:
        return "data:image/gif;base64," + base64.b64encode(fh.read()).decode("ascii")


CSS = """
:root{--bg:#f3f4f8;--panel:#ffffff;--fg:#151826;--mute:#5b6280;--line:#dde0ea;--acc:#1f6fd1;--warm:#c4521a;--ok:#1d8a52;--bad:#b3261e;color-scheme:light}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--bg:#0a0c13;--panel:#121725;--fg:#dde1f2;--mute:#8a92b6;--line:#232b44;--acc:#7fc4ff;--warm:#ff9a5c;--ok:#5fd394;--bad:#ff8a80;color-scheme:dark}}
:root[data-theme="dark"]{--bg:#0a0c13;--panel:#121725;--fg:#dde1f2;--mute:#8a92b6;--line:#232b44;--acc:#7fc4ff;--warm:#ff9a5c;--ok:#5fd394;--bad:#ff8a80;color-scheme:dark}
body{background:var(--bg);color:var(--fg);font:15px/1.55 Barlow,system-ui,sans-serif;padding-inline:16px;margin:0}
#top{display:flex;gap:10px;align-items:center;flex-wrap:wrap;max-width:1080px;margin:0 auto;padding-top:16px}
#top .k{margin-left:auto}
.wrap{max-width:1080px;margin:0 auto;padding-block:20px 64px}
h1{font:700 clamp(28px,5vw,44px)/1.05 "Chakra Petch",system-ui,sans-serif;margin:0 0 10px;text-wrap:balance}
h2{font:700 20px/1.2 "Chakra Petch",system-ui,sans-serif;margin:0}
h3{font:700 18px/1.2 "Chakra Petch",system-ui,sans-serif;margin:0 0 10px}
.lede{color:var(--mute);max-width:70ch;margin:0 0 28px}
.k{font:600 11px/1 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.12em;text-transform:uppercase;color:var(--acc)}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px}
.card{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:18px;display:flex;flex-direction:column;gap:10px;min-width:0}
.card p{margin:0}
.card img{width:100%;height:auto;border-radius:6px;background:#05070d;display:block}
.noimg{border:1px dashed var(--line);border-radius:6px;padding:14px;color:var(--mute);font-size:14px}
.num{font-family:"JetBrains Mono",ui-monospace,monospace;font-size:13px;color:var(--mute);font-variant-numeric:tabular-nums}
.verdict{font-size:14px}
.pass{color:var(--ok)} .fail{color:var(--bad)} .warn{color:var(--warm)}
section{margin-top:40px}
.tbl{overflow-x:auto;border:1px solid var(--line);border-radius:10px;background:var(--panel)}
table{border-collapse:collapse;width:100%;min-width:760px;font-size:14px}
th,td{text-align:left;padding:9px 12px;border-bottom:1px solid var(--line);vertical-align:top}
th{font:600 11px/1.3 "JetBrains Mono",ui-monospace,monospace;letter-spacing:.08em;text-transform:uppercase;color:var(--mute)}
tr:last-child td{border-bottom:0}
td b{font-weight:500}
ul{margin:0;padding-left:20px;max-width:74ch}
li{margin:5px 0}
a.go{align-self:flex-start;font:600 14px/1 "Chakra Petch",system-ui,sans-serif;color:var(--bg);background:var(--acc);text-decoration:none;padding:10px 14px;border-radius:7px}
"""


def page() -> str:
    swim = gif("results/emergent/swim.gif")
    school = gif("results/arms/school_a9_g1500.gif")
    chase = gif("results/arms/encounter_g1460_g1460.gif")
    return f"""<title>Four Swarm Species</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@500;700&family=Barlow:wght@400;500&family=JetBrains+Mono:wght@400&display=swap">
<style>{CSS}</style>
<div id="top"><span class="k">Tools/NCA · four swarm species · 2026-10-07</span></div>
<div class="wrap">
  <h1>Four swarm species</h1>
  <p class="lede">Each answers the same question differently: how much of a swarm should be designed, and how much
  should emerge from local rules? All four conserve mass, and nothing in them dies on a timer. Three of them are
  fully local: each tadpole perceives only its neighbours and its surroundings. Numbers are from each species'
  <span class="num">NOTE.md</span> on the branch shown.</p>

  <div class="grid">
    <div class="card"><div class="k">1 · designed</div><h2>Field swarm (in game)</h2>
      <div class="noimg">Running in the game today: three big swarms with GPU-drawn members, prey for other fauna,
      multi-domain colour.</div>
      <p>The shape comes from designed attractor fields and global slot assignment. Composition is designed too.
      Accurate and cheap, but the lead's verdict was that it lost too much organic imperfection.</p>
      <div class="num">cece/swarm-fauna-game · Docs/SWARM_FAUNA.md</div></div>

    <div class="card"><div class="k">2 · hybrid · frozen</div><h2>Peak shapeshifter (posinfo2)</h2>
      <div class="noimg">Viewer: the swarm viewer's posinfo2 row (results/posinfo2/rollout.json).</div>
      <p>A learned per-tadpole rule decides where everyone swims. A designed controller decides who is laid and who
      molts into what. It morphs between all four elemental body plans when the majority changes.</p>
      <div class="num">51 / 52 transitions under the loss-8 bar · own plans 1.6–2.7 · 0 self-inflicted deaths · 5.2 ms/step · organic</div>
      <div class="verdict pass">The reference. Locality is mixed: it reads a body frame and an element census.</div></div>

    <div class="card"><div class="k">3 · pure emergent</div><h2>The local jellyfish</h2>
      <img src="{swim}" alt="A cloud of tadpoles growing into a jellyfish shape beside the target frame">
      <p>No controller and no global inputs. Each tadpole reads neighbours within 8 units, plus four chemicals the
      swarm secretes into the water. Laying and death are learned outputs. Trained on one animated target, a
      pulsing jellyfish.</p>
      <div class="num">strict shape 8.9 (seeds 7.5 / 11.2 / 11.5 / 5.6) · heal 0.69 · 0 deaths in 793k tadpole-steps · organic · 14.6 ms/step</div>
      <div class="verdict"><span class="warn">Close, not under the bar:</span> two of four seeds pass. <span class="fail">The pulse never
      emerged</span>, and the body fills the world cap (280 tadpoles, plan 88). The chemicals became load-bearing
      late in training: cutting them costs +2.9.</div></div>

    <div class="card"><div class="k">4 · co-evolved</div><h2>Predator and prey</h2>
      <img src="{school}" alt="Prey tadpoles schooling while larger predators hunt them">
      <p>Two local policies trained against each other: 8 big, bursting, turn-limited predators against 120 agile
      grazing prey. A catch moves the prey's mass into the predator; starvation is the only other death.</p>
      <div class="num">0 / 560 cycles in the generation matrix · prey juke 76 → 327 deg/s · schooling under selfish-herd selection: polarisation 0.53 → 0.86 · 1.86 ms/step</div>
      <div class="verdict pass">A real arms race. Generation 0 is a designed script (learning from scratch failed),
      and the open economy needs a designed satiety gate.</div>
      <a class="go" href="arms.html">Open the arms-race viewer</a></div>
  </div>

  <section>
    <h3>Designed, learned, emergent</h3>
    <div class="tbl"><table>
      <tr><th>species</th><th>composition</th><th>shape / behaviour</th><th>perception</th><th>emerged without being asked</th></tr>
      <tr><td><b>1 field</b></td><td>designed</td><td>designed fields + boids</td><td>global slots</td><td>little: its mistakes read as bugs</td></tr>
      <tr><td><b>2 posinfo2</b></td><td>designed (homeo-7)</td><td>learned MLP</td><td>mixed: neighbours + body frame + census</td><td>motion feel; healing after strikes</td></tr>
      <tr><td><b>3 emergent</b></td><td>emerges from breed-true laying</td><td>learned MLP + secreted chemicals</td><td>local</td><td>growth from 16; healing; zero deaths; organic motion</td></tr>
      <tr><td><b>4 arms race</b></td><td>open economy (designed rules)</td><td>learned by co-evolution from a designed seed</td><td>local</td><td>juking, wall pinning, probe-and-abandon bursts, ignoring an uncatchable vessel, schooling (a9)</td></tr>
    </table></div>
  </section>

  <section>
    <h3>What the four say together</h3>
    <ul>
      <li><b>Local rules can grow, keep and heal an outline, and lose nothing doing it.</b> Holding a crisp plan,
      a headcount or an animation still needs something body-wide, designed or perceived. Positional information is
      what took the hybrid from about 7 to 1.7. A census was worth 0.03 to the pure rule.</li>
      <li><b>Grouping needs individual stakes.</b> Under species-mean fitness the prey never schooled, including four
      follow-up runs built to provoke it. With four competing tribes per pond (a selfish herd) they schooled within 1500
      generations, and the predators answered by packing.</li>
      <li><b>Evolution beats the script on feel.</b> The designed seed moves in machine-smooth lines (jerk 0.16).
      Every evolved pair is in the organic band.</li>
      <li><b>Economies need a brake a policy never trained with.</b> Without the satiety gate, predators breed to
      20–30 and eat the pond out in 2–4 minutes.</li>
    </ul>
  </section>

  <section>
    <h3>A hunt before schooling (generation 1460)</h3>
    <div class="grid"><div class="card"><img src="{chase}" alt="Predators bursting after juking prey">
      <p class="num">Bursts are drawn red: the whole telegraph, about 0.8 s before a catch. 78% of them fail to a juke.</p></div></div>
  </section>
</div>
"""


if __name__ == "__main__":
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(page())
    print(OUT, os.path.getsize(OUT))
