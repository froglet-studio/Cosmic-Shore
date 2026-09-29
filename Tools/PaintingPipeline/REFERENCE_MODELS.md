# campB — 3D Reference Model Manifest

Retrieved 2026-07-09. Base dir: `/tmp/claude-0/-home-user-Cosmic-Shore/dcd49b4e-ed43-5dc3-81da-2e2179e80c09/scratchpad/campB`
Original archives in `dl/`, extracted meshes in `meshes/`, trimesh stats in `mesh_stats.json`, shaded verification renders in `shaded_*.png`.
Total on disk: 612 MB (186 MB archives + 417 MB extracted). All meshes verified loadable with trimesh (python3).

## License basis — threedscans.com (Oliver Laric project)

- Current site tagline: **"Three D Scans — Free 3D scan archive"** (https://threedscans.com/).
- Explicit license statement, quoted from the project's Info page (as archived 2019-06-20, https://web.archive.org/web/20190620080832/http://threedscans.com/info/ — the live page today lists the same institutions and contact but dropped the sentence):
  > **"All scans can be downloaded and used without copyright restrictions. If you find them useful, please let us know: contact@threedscans.com"**
- Why safe: the project exists to publish museum-sculpture scans restriction-free; underlying works are centuries-old (public domain); scans distributed directly by the project's own WordPress uploads.

## License basis — archive.org mirrors of Thingiverse things

archive.org preserves each thing as item `thingiverse-<id>` with the license URL in item metadata and the license/attribution files inside the zip. Direct HTTP download, no login. CC licenses are irrevocable once granted.

---

## 1. LION (primary) — "Lion statue from Ribalta Park" (Castellón, Spain)

| | |
|---|---|
| Subject | Seated lion, paw on sphere, full flowing sculpted mane (classic Medici-lion pose) |
| File | `meshes/Lion_statue_from_Ribalta_Park_1243777/files/Lion_statue_from_Ribalta_Park.stl` |
| Source URL | https://archive.org/download/thingiverse-1243777/Lion_statue_from_Ribalta_Park_1243777.zip (mirror of https://www.thingiverse.com/thing:1243777) |
| License | **CC-BY 4.0** per archive.org item metadata (https://creativecommons.org/licenses/by/4.0/). In-zip README: *"Lion statue from Ribalta Park by wakuhn is licensed under the Creative Commons - Attribution license. http://creativecommons.org/licenses/by/3.0/"*; derivative of the photogrammetry scan by **egiptologo91** on Sketchfab (https://sketchfab.com/models/27a30b2000394664b4c60a3811659dbe), which the README documents as *"egiptologo91 posted his scan under the creative commons attribution license, http://creativecommons.org/licenses/by/4.0/"* (2016) |
| Caveat | The Sketchfab page has SINCE been switched to Sketchfab "Editorial" license (checked via api.sketchfab.com 2026-07-09). The 2016 CC-BY grant under which wakuhn redistributed is irrevocable, and the mirror preserves that grant — but if zero ambiguity is required, use the CC0 Temperance lion or a threedscans lion below. Attribute: "Lion statue from Ribalta Park — scan by egiptologo91, print adaptation by wakuhn, CC-BY." |
| Mesh stats | 987,402 verts / 329,134 faces, extents 45.7 × 68.0 × 100.0, loads clean, not watertight |
| Verification | `shaded_RibaltaLion.png` — mane locks clearly resolved in the mesh |

## 1b. LION (CC0 alternate) — Temperance Union Lion, Las Vegas NM (Angelo de Tullio, 1896)

| | |
|---|---|
| File | `meshes/Temperance_Lion_985693/LLV_lion_fixed_02.stl` (also low-res `temperance_lion_-_las_vegas_nm.stl`) |
| Source URL | https://archive.org/download/thingiverse-985693/Temperance_Union_Lion_Statue_-_lil__039_Las_Vegas_New_Mexico_985693.zip (mirror of https://www.thingiverse.com/thing:985693) |
| License | **CC0 1.0** per archive.org item metadata: https://creativecommons.org/publicdomain/zero/1.0/ — no ambiguity, no attribution required |
| Mesh stats | 1,734,786 verts / 578,262 faces, extents 45.6 × 86.2 × 65.5, loads clean |
| Verification | `shaded_TemperanceLion.png` — couchant lion, curly folk-art mane clearly visible |

## 1c. LION (threedscans fallbacks)

- **Seated Lion** (stone, The Collection, Lincoln; 32 × 15 × 23 cm) — `meshes/Lion.stl/Lion.stl`, from https://threedscans.com/lincoln/leon/ → https://threedscans.com/wp-content/uploads/2016/02/Lion.stl.zip. 303,990 verts / 101,330 faces. Heavily weathered; mane reads as mass, little lock detail (`shaded_Lion_stl.png`).
- **Bayon Lion** (Musée Guimet, Khmer, Preah Khan Kompong Svay) — `meshes/Bayon-Lion.OBJ/Bayon Lion.OBJ`, from https://threedscans.com/uncategorized/lion/ → https://threedscans.com/wp-content/uploads/2016/10/Bayon-Lion.OBJ.zip. 749,999 verts / 1,500,006 faces. Stylized Khmer guardian lion — fallback only, per brief.
- **Drame au désert** (Georges Gardet, 1887, plaster) — `meshes/Georges-Gardet.OBJ/Georges Gardet.OBJ`, from https://threedscans.com/depot-des-sculptures-de-la-ville-de-paris/drameaudesert/ → https://threedscans.com/wp-content/uploads/2016/10/Georges-Gardet.OBJ.zip. 494,981 verts / 990,718 faces. Investigated as a lion candidate; turned out to be a big cat crouched beneath a rock ledge (mane not prominent) — kept as an alternate feline, dramatic composition.
- **Marble statue of a lion** (Greek, ca. 400–390 BC, The Met acc. 09.221.3) — `meshes/Marble_statue_of_a_lion_268358/Marble_statue_of_a_lion/marble_statue_of_a_lion.stl` (+ .obj), from https://archive.org/download/thingiverse-268358/Marble_statue_of_a_lion_268358.zip (mirror of https://www.thingiverse.com/thing:268358). License **CC-BY-SA 3.0** per archive.org metadata (note the ShareAlike condition). 130,434 verts / 43,478 faces. Ancient Greek funerary lion, shallow archaic mane.

## 2. PEACOCK — "クジャク（Peafowl）3Dデータ" by YahooJAPAN

| | |
|---|---|
| Subject | Peafowl/peacock figure — crest (crown feathers), body, and long sweeping tail; sculptural feather detail |
| File | `meshes/Peafowl_182242/files/Peafowl_t.stl` |
| Source URL | https://archive.org/download/thingiverse-182242/クジャクPeafowl3Dデータ_182242.zip (mirror of https://www.thingiverse.com/thing:182242, Yahoo! JAPAN Hack Day, https://hackday.jp) |
| License | **CC-BY 4.0** per archive.org item metadata (https://creativecommons.org/licenses/by/4.0/). In-zip LICENSE.txt: *"This thing was created by Thingiverse user YahooJAPAN, and is licensed under cc."* README.txt: *"クジャク（Peafowl）3Dデータ by YahooJAPAN on Thingiverse: https://www.thingiverse.com/thing:182242"*. Attribute: "Peafowl 3D data by YahooJAPAN, CC-BY 4.0." |
| Mesh stats | 302,934 verts / 100,978 faces, extents 94.1 × 34.1 × 70.2, loads clean |
| Verification | `shaded_Peafowl.png` — crest and layered tail feathers confirmed in the mesh |

## 3. BIRD WITH WINGS (Phoenix reference) — "Striding Eagle"

| | |
|---|---|
| Subject | Marble eagle, 16th century, one wing raised/spread, highly detailed feathering (Saint Louis Art Museum, 76.8 × 58.4 × 66 cm) |
| File | `meshes/Eagle_custom_Normals.obj/Eagle_custom_Normals.obj` |
| Source URL | https://threedscans.com/saint-louis-art-museum/striding-eagle/ → https://threedscans.com/wp-content/uploads/2019/02/Eagle_custom_Normals.obj.zip |
| License | threedscans: **"All scans can be downloaded and used without copyright restrictions."** (see license basis above) |
| Mesh stats | 272,776 verts / 540,970 faces, extents 584 × 770 × 565 (mm-ish units), loads clean |
| Verification | `shaded_Eagle_custom_Normals_obj.png` — individual flight feathers, wing coverts, head/beak all crisply resolved |

## 4. EXTRA #1 — Horse Head ("Medici Riccardi" horse)

| | |
|---|---|
| Subject | Monumental Greek bronze horse head, 2nd half 4th c. BC (Museo archeologico nazionale, Florence) — flared nostrils, cropped mane, superb line-engraving subject |
| File | `meshes/Horse_Head.obj/Horse_Head.obj` |
| Source URL | https://threedscans.com/museo-archeologico-nazionale/horse/ → https://threedscans.com/wp-content/uploads/2016/02/Horse_Head.obj.zip |
| License | threedscans: **"All scans can be downloaded and used without copyright restrictions."** |
| Mesh stats | 1,250,402 verts / 2,499,712 faces, extents 908 × 764 × 502, loads clean |

## 5. EXTRA #2 — Glycon (coiled serpent deity)

| | |
|---|---|
| Subject | Glycon snake-god statue — coiled serpent body with humanoid hair/mane; iconic, sinuous, ideal for engraving linework |
| File | `meshes/Glykon.obj/Glykon.obj` |
| Source URL | https://threedscans.com/uncategorized/glycon/ → https://threedscans.com/wp-content/uploads/2023/02/Glykon.obj.zip |
| License | threedscans: **"All scans can be downloaded and used without copyright restrictions."** |
| Mesh stats | 1,281,218 verts / 2,562,504 faces, extents 26.3 × 32.3 × 26.2, **watertight**, loads clean |

---

## 6. MOUNTAIN — Matterhorn DEM (AWS Terrain Tiles)

| | |
|---|---|
| Subject | The Matterhorn (Zermatt, 45.9766°N 7.6585°E) — real elevation raster, used for contour + ridgeline strokes in `Painting_AlmightyMountain.asset` |
| Source | AWS Open Data **Terrain Tiles** (Mapzen terrarium PNG tiles), `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png` |
| License | Open data, **attribution REQUIRED** (must appear in the game's credits): *"Terrain Tiles: DEM sources include SRTM (NASA), GMTED2010 and ETOPO1 (USGS), EU-DEM (European Environment Agency / Copernicus), and other open datasets; composited by Mapzen, hosted by AWS Open Data."* |
| Verification | `mtn_front/side/top.png` — the north-face profile and Hörnli/Zmutt/Furggen/Lion ridges read correctly against reference photographs |

---

## Verification method

Every mesh was loaded with `trimesh` (python3) — vertex/face counts and bounding boxes recorded in `mesh_stats.json`; zero load failures. Each key mesh was additionally rendered as a 3-view shaded point-splat PNG (`shaded_*.png`) and visually inspected to confirm the subject and surface detail (mane locks, feathers, crest).

## Sources that did NOT pan out

- Wikimedia Commons: no peacock/lion STL or GLB files at all.
- Smithsonian 3D: 403 through the proxy (known).
- Sketchfab / MyMiniFactory (Scan the World, incl. the actual Medici Lion): downloads login-walled; STW is CC-BY-NC (non-commercial) — rejected on license grounds.
- Europeana 3D records: media links point back to Sketchfab embeds (login-walled), mostly NC/ND variants.
- poly.pizza (Google Poly mirror), OpenGameArt, NHM data portal, Artec gallery: no peacock mesh.
