"""Headless check that a viewer HTML renders and replays (screenshots at a few frames). Needs network for the CDN."""
import sys, asyncio
from playwright.async_api import async_playwright

async def main(html, png, frames=(0.3, 0.9)):
    async with async_playwright() as p:
        b = await p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1194/chrome-linux/chrome", args=["--use-gl=swiftshader", "--enable-webgl", "--ignore-gpu-blocklist", "--ignore-certificate-errors"])
        pg = await b.new_page(viewport={"width": 1000, "height": 640})
        errs = []; pg.on("pageerror", lambda e: errs.append(str(e))); pg.on("console", lambda m: m.type == "error" and errs.append(m.text))
        await pg.goto("file://" + html); await pg.wait_for_timeout(2500)
        for j, f in enumerate(frames):
            await pg.evaluate(f"(()=>{{const s=document.getElementById('scrub');s.value=Math.floor(s.max*{f});s.dispatchEvent(new Event('input'));document.getElementById('play').click()}})()")
            await pg.wait_for_timeout(700)
            await pg.screenshot(path=png.replace(".png", f"_{j}.png"))
            await pg.evaluate("document.getElementById('play').click()")
        print("errors:", errs)
        await b.close()

asyncio.run(main(sys.argv[1], sys.argv[2]))
