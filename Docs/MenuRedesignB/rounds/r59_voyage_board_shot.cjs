// The HyperSea board's pictures for r59_ark_hangar_render.py: the Voyage Board at 2x with its chrome hidden.
//   node r59_voyage_board_shot.cjs <out_dir>   -> hi_Sector.png (board_sector.png), hi_Arm.png (board_galaxy.png), hi_Region.png
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const out = process.argv[2];
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1800, height: 1100 }, deviceScaleFactor: 2 });
  await p.goto('file://' + path.resolve(__dirname, '../../Voyages/VoyageBoard/voyage-board.html'), { waitUntil: 'load' });
  await p.waitForTimeout(2500);
  const hide = async () => p.addStyleTag({ content: '#ladder,.tb,.readout,.legend,[class*=toolbar],[class*=key]{visibility:hidden!important}' });
  const cv = await p.$('#cv');
  for (const t of ['Sector', 'Arm', 'Region']) {
    await p.$eval(`#ladder button[data-t="${t}"]`, b => b.click());
    await p.waitForTimeout(1800);
    await hide();
    await p.waitForTimeout(300);
    await cv.screenshot({ path: `${out}/hi_${t}.png` });
  }
  await b.close();
})();
