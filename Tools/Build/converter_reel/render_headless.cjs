// Renders the Converter Reel headless, frame-exact, from the built page (Docs/Studios/ConverterReelLab.html), piping raw
// RGBA frames to ffmpeg (libx264, BT.709, +faststart). The same frame(tau) function the page's own exporter encodes.
//   node render_headless.cjs <out.mp4> [--format 0] [--fps 60] [--ss 2] [--from 0] [--to N] [--settings '{"bodyBright":"#13fff2"}']
//                                      [--crf 15] [--preset slow]
// --settings takes any of the page's P keys (a saved look's settings). --from/--to render a frame range, so several
// processes can split one loop; join the parts with ffmpeg's concat demuxer (-c copy). Needs Playwright (NODE_PATH) and
// ffmpeg. Software GL (SwiftShader) here costs about 2 s a frame at 2x supersampling.
const path = require('path');
let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { console.error('render_headless: cannot load playwright (' + e.message + '); set NODE_PATH'); process.exit(2); }
const PAGE = 'file://' + path.resolve(__dirname, '../../../Docs/Studios/ConverterReelLab.html');
const CDN = process.env.REEL_CDN_DIR;   // optional: a folder holding mp4-muxer.js / webm-muxer.js when jsDelivr is unreachable
const { spawn } = require('child_process');
const args = process.argv.slice(2), opt = (k, d) => { const i = args.indexOf('--' + k); return i >= 0 ? args[i + 1] : d; };
const out = args[0];
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1200, height: 900 } });
  const errs = [];
  page.on('pageerror', (e) => errs.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errs.push(m.text()); });
  // The muxers only serve the page's own exporter; this script encodes with ffmpeg.
  if (CDN) await page.route('**/*-muxer.min.js', (r) => r.fulfill({ path: path.join(CDN, path.basename(r.request().url()).replace('.min', '')), contentType: 'text/javascript' }));
  else await page.route('**/*-muxer.min.js', (r) => r.fulfill({ body: '', contentType: 'text/javascript' }));
  await page.goto(PAGE);
  await page.waitForFunction(() => window.__lab && window.__lab.reel && window.__lab.reel.T);
  const settings = JSON.parse(opt('settings', '{}'));
  const info = await page.evaluate(([s, f, fps, ss]) => { window.__lab.manualClock = true; if (Object.keys(s).length) window.__lab.apply(s); return window.__lab.prepareExport({ format: f, fps, supersample: ss }); }, [settings, +opt('format', 0), +opt('fps', 60), +opt('ss', 2)]);
  const from = +opt('from', 0), to = Math.min(+opt('to', info.frames), info.frames);
  console.error('frames', info.frames, 'size', info.w + 'x' + info.h, 'fps', info.fps, 'rendering', from, '..', to);
  const ff = spawn('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgba', '-s', info.w + 'x' + info.h, '-r', String(info.fps), '-i', '-',
    '-vf', 'vflip,format=yuv420p', '-c:v', 'libx264', '-preset', opt('preset', 'slow'), '-crf', opt('crf', '14'), '-profile:v', 'high', '-tune', 'film',
    '-g', String(info.fps * 2), '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709', '-movflags', '+faststart', out], { stdio: ['pipe', 'inherit', 'inherit'] });
  const t0 = Date.now();
  for (let i = from; i < to; i++) {
    const b64 = await page.evaluate((i) => { window.__lab.renderFrame(i); const px = window.__lab.readPixels(); let s = ''; const CH = 0x8000; for (let k = 0; k < px.length; k += CH) s += String.fromCharCode.apply(null, px.subarray(k, k + CH)); return btoa(s); }, i);
    const buf = Buffer.from(b64, 'base64');
    if (!ff.stdin.write(buf)) await new Promise((r) => ff.stdin.once('drain', r));
    if ((i - from) % 30 === 0) console.error('frame', i, ((Date.now() - t0) / (i - from + 1)).toFixed(0), 'ms/frame');
  }
  ff.stdin.end();
  await new Promise((r) => ff.on('close', r));
  if (errs.length) console.error('page errors:', errs.slice(0, 5));
  await browser.close();
})();
