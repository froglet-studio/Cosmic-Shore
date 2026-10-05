"""Inline the NCA creature modules into nca_gallery_src.html -> nca_gallery.html (the Lab's NCA bestiary page).

    python Tools/Ecology/flight/creatures/build_nca_gallery.py

Always includes nca_creature.js (the lizard); adds nca_whale.js / nca_jelly.js when they exist, and the page shows a
species button for each body it finds.
"""
import os
HERE = os.path.dirname(os.path.abspath(__file__))
names = ["nca_creature.js"] + [f for f in ("nca_whale.js", "nca_jelly.js") if os.path.exists(os.path.join(HERE, f))]
src = open(os.path.join(HERE, "nca_gallery_src.html"), encoding="utf-8").read()
js = "\n".join(open(os.path.join(HERE, n), encoding="utf-8").read().replace("</script", "<\\/script") for n in names)
assert src.count("/*__NCA__*/") == 1
out = src.replace("/*__NCA__*/", js)
open(os.path.join(HERE, "nca_gallery.html"), "w", encoding="utf-8").write(out)
print(f"nca_gallery.html {len(out) // 1024} KB: {', '.join(names)}")
