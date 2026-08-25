#!/usr/bin/env python3
"""Emit the UI sprite kit demonstration scene as Unity YAML.

Called by ``author_ui_sprite_kit.py``.  The scene is generated rather than
hand-authored so it cannot drift from :data:`author_ui_sprite_kit.KIT`, and so
``--check`` covers it too.

It ships **no new C#**.  Everything is declarative UGUI, which keeps the scene
outside the ``/verify-unity`` compile gate entirely.

Layout: one full-screen page per sprite family, page 1 active and the rest
inactive - toggle the siblings in the Hierarchy.  Each row shows one sprite at
three widths (plus, where the sprite carries vertical borders, a fourth sample
at double height) and tints each sample a different Sec.2 colour, which is what
demonstrates that the asset itself carries no colour.
"""

from __future__ import annotations

# UGUI script GUIDs, read out of Menu_Main.unity / SafeAreaFitterTestScene.unity.
GUID_CANVAS_SCALER = "0cd44c1031e13a943bb63640046fad76"
GUID_GRAPHIC_RAYCASTER = "dc42784cf147c0c48a680349fa168899"
GUID_IMAGE = "fe87c0e1cc204ed48ad3b37840f39efc"
GUID_TEXT = "5f7201a12d95ffc409449d95f23cf332"
# Unity's built-in legacy font. Chosen over TMP on purpose: a test scene should
# not take a dependency on a font asset that task T5 is still replacing.
FONT_BUILTIN = "{fileID: 10102, guid: 0000000000000000e000000000000000, type: 0}"

REF_W, REF_H = 1920, 1080

# Style Foundation Sec.2 / Sec.11.
PALETTE = {
    "black": "00010A",
    "light": "E6E9FF",
    "inactiveLight": "5C5F70",
    "cta": "99FF80",
    "gold": "FFAE00",
    "jade": "00D4FF",
    "ruby": "A600FF",
    "jadeLight": "80EAFF",
    "neutralLightest": "747BAD",
}

#: Cycled across the samples in a row.  Four different tints of one white
#: source is the whole point of the kit.
SAMPLE_TINTS = ("light", "cta", "gold", "jade")

#: Page order and which kit groups land on each.  Split so no page overflows
#: 1080 at the authored heights.
PAGES = (
    ("Buttons - Sec.10.1", ("Buttons",)),
    ("Panels / popups - Sec.10.3", ("Panels",)),
    ("Cards - Sec.10.6 / Sec.10.13", ("Cards",)),
    ("Currency pill + hexagons - Sec.10.4 / Sec.10.5 / Sec.10.7 / Sec.10.12",
     ("Pill", "Hex")),
    ("End-of-game banner - Sec.10.9", ("Banner",)),
)

LABEL_W = 300
CONTENT_X = 320
GAP = 20
TOP_Y = 96


def rgb(name: str, alpha: float = 1.0) -> str:
    h = PALETTE[name]
    r, g, b = (int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return f"{{r: {r:.4g}, g: {g:.4g}, b: {b:.4g}, a: {alpha:.4g}}}"


def sample_widths(w: int, border: tuple[int, int, int, int]) -> list[int]:
    """Three widths: the minimum legal one, 1:1, and a long stretch.

    The minimum - where the left and right border columns touch and the centre
    is zero wide - is the harshest case for a 9-slice, so it leads.
    """
    left, _, right, _ = border
    if left or right:
        return [left + right, w, 480]
    return [w // 2, w, w * 2]          # no horizontal border: uniform scale


def _obj(fid: int, name: str, components: list[int], active: bool = True,
         layer: int = 5) -> str:
    comps = "\n".join(f"  - component: {{fileID: {c}}}" for c in components)
    return f"""--- !u!1 &{fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comps}
  m_Layer: {layer}
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {1 if active else 0}
"""


def _rect(fid: int, go: int, father: int, children: list[int],
          anchor_min: tuple[float, float], anchor_max: tuple[float, float],
          pivot: tuple[float, float], pos: tuple[float, float],
          size: tuple[float, float]) -> str:
    kids = ("[]" if not children else
            "\n" + "\n".join(f"  - {{fileID: {c}}}" for c in children))
    return f"""--- !u!224 &{fid}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: {kids}
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {anchor_min[0]:.4g}, y: {anchor_min[1]:.4g}}}
  m_AnchorMax: {{x: {anchor_max[0]:.4g}, y: {anchor_max[1]:.4g}}}
  m_AnchoredPosition: {{x: {pos[0]:.6g}, y: {pos[1]:.6g}}}
  m_SizeDelta: {{x: {size[0]:.6g}, y: {size[1]:.6g}}}
  m_Pivot: {{x: {pivot[0]:.4g}, y: {pivot[1]:.4g}}}
"""


def _canvas_renderer(fid: int, go: int) -> str:
    return f"""--- !u!222 &{fid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
"""


def _image(fid: int, go: int, color: str, sprite: str, sliced: bool) -> str:
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_IMAGE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {color}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {sprite}
  m_Type: {1 if sliced else 0}
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""


def _text(fid: int, go: int, color: str, body: str, size: int,
          align: int = 0) -> str:
    esc = body.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_TEXT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {color}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {FONT_BUILTIN}
    m_FontSize: {size}
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 1
    m_MaxSize: 40
    m_Alignment: {align}
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 1
    m_VerticalOverflow: 1
    m_LineSpacing: 1
  m_Text: "{esc}"
"""


HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_IndirectSpecularColor: {r: 0, g: 0, b: 0, a: 1}
  m_UseRadianceAmbientProbe: 0
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
--- !u!1 &100001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 100003}
  - component: {fileID: 100002}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!20 &100002
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: 0, g: 0.004, b: 0.039, a: 1}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_FocalLength: 50
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 0
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!4 &100003
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: -10}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
"""


def build_scene(entries, guid_for, unity_path_for) -> str:
    """``entries`` is a list of dicts with keys name/group/component/note/
    border/size - a named record rather than tuple indices, because this
    emitter reading the kit table positionally is exactly how it once silently
    produced an empty scene."""
    blocks: list[str] = [HEADER]
    fid = 1000000

    def nxt() -> int:
        nonlocal fid
        fid += 1
        return fid

    canvas_go, canvas_rt = 200001, 200002
    canvas_children: list[int] = []
    body: list[str] = []

    # ---- backdrop -------------------------------------------------------
    go, rt, cr, im = nxt(), nxt(), nxt(), nxt()
    canvas_children.append(rt)
    body.append(_obj(go, "Backdrop", [rt, cr, im]))
    body.append(_rect(rt, go, canvas_rt, [], (0, 0), (1, 1), (0.5, 0.5),
                      (0, 0), (0, 0)))
    body.append(_canvas_renderer(cr, go))
    body.append(_image(im, go, rgb("black"), "{fileID: 0}", False))

    total_pages = len(PAGES)
    for page_index, (page_title, groups) in enumerate(PAGES):
        rows = [e for e in entries if e['group'] in groups]
        page_go, page_rt = nxt(), nxt()
        page_children: list[int] = []
        page_body: list[str] = []
        canvas_children.append(page_rt)

        # page title
        tgo, trt, tcr, ttx = nxt(), nxt(), nxt(), nxt()
        page_children.append(trt)
        page_body.append(_obj(tgo, "Title", [trt, tcr, ttx]))
        page_body.append(_rect(trt, tgo, page_rt, [], (0, 1), (0, 1), (0, 1),
                               (48, -32), (1800, 56)))
        page_body.append(_canvas_renderer(tcr, tgo))
        page_body.append(_text(
            ttx, tgo, rgb("cta"),
            f"UI SPRITE KIT  -  page {page_index + 1}/{total_pages}  -  "
            f"{page_title}\n"
            f"White + alpha only; every sample below is the same asset under a "
            f"different Image.color. Toggle the sibling 'Page N' objects to "
            f"see the rest.", 20))

        y = TOP_Y
        for entry in rows:
            name = entry['name']
            border = entry['border']
            component, note = entry['component'], entry['note']
            sw, sh = entry['size']
            widths = sample_widths(sw, border)
            vertical = bool(border[1] or border[3])
            heights = [sh] * len(widths)
            if vertical:
                # A fourth sample at double height: with top/bottom borders the
                # corner regions stay unstretched, so the sliver must be
                # identical here too.
                widths = widths + [widths[-1]]
                heights = heights + [sh * 2]
            elif not (border[0] or border[2]):
                heights = [round(sh * w / sw) for w in widths]  # uniform scale

            row_h = max(heights) + 20

            # label
            lgo, lrt, lcr, ltx = nxt(), nxt(), nxt(), nxt()
            page_children.append(lrt)
            page_body.append(_obj(lgo, f"{name} - label", [lrt, lcr, ltx]))
            page_body.append(_rect(lrt, lgo, page_rt, [], (0, 1), (0, 1),
                                   (0, 1), (48, -y), (LABEL_W, row_h)))
            page_body.append(_canvas_renderer(lcr, lgo))
            btxt = ",".join(str(b) for b in border)
            page_body.append(_text(
                ltx, lgo, rgb("light"),
                f"{name}\n{sw}x{sh}  border {btxt}\n{component}"
                + (f"\n{note}" if note else ""), 13))

            x = CONTENT_X
            for i, (w, h) in enumerate(zip(widths, heights)):
                sgo, srt, scr, sim = nxt(), nxt(), nxt(), nxt()
                page_children.append(srt)
                tall = vertical and i == len(widths) - 1
                label = f"{w}x{h}" + (" (2x height)" if tall else "")
                page_body.append(_obj(sgo, f"{name} @ {label}",
                                      [srt, scr, sim]))
                page_body.append(_rect(
                    srt, sgo, page_rt, [], (0, 1), (0, 1), (0, 1),
                    (x, -(y + (row_h - h) / 2.0)), (w, h)))
                page_body.append(_canvas_renderer(scr, sgo))
                sliced = any(border)
                sprite_ref = (f"{{fileID: 21300000, "
                              f"guid: {guid_for(unity_path_for(name))}, "
                              f"type: 3}}")
                page_body.append(_image(
                    sim, sgo, rgb(SAMPLE_TINTS[i % len(SAMPLE_TINTS)]),
                    sprite_ref, sliced))
                x += w + GAP
            y += row_h

        if "Banner" in groups:
            # The three banner sprites only make sense together, so show the
            # Sec.10.9 header assembled: caps at lower alpha, as the spec says.
            y += 24
            lgo, lrt, lcr, ltx = nxt(), nxt(), nxt(), nxt()
            page_children.append(lrt)
            page_body.append(_obj(lgo, "Assembled header - label",
                                  [lrt, lcr, ltx]))
            page_body.append(_rect(lrt, lgo, page_rt, [], (0, 1), (0, 1),
                                   (0, 1), (48, -y), (LABEL_W, 80)))
            page_body.append(_canvas_renderer(lcr, lgo))
            page_body.append(_text(
                ltx, lgo, rgb("light"),
                "assembled\nSec.10.9 {DOMAIN} VICTORY\ncaps at lower alpha, "
                "body stretched", 13))
            x = CONTENT_X
            for part, w, alpha in (("UIKit_BannerCap_Left", 32, 0.45),
                                   ("UIKit_Banner_Fill", 900, 1.0),
                                   ("UIKit_BannerCap_Right", 32, 0.45)):
                sgo, srt, scr, sim = nxt(), nxt(), nxt(), nxt()
                page_children.append(srt)
                page_body.append(_obj(sgo, f"assembled - {part}",
                                      [srt, scr, sim]))
                page_body.append(_rect(srt, sgo, page_rt, [], (0, 1), (0, 1),
                                       (0, 1), (x, -y), (w, 64)))
                page_body.append(_canvas_renderer(scr, sgo))
                page_body.append(_image(
                    sim, sgo, rgb("jadeLight", alpha),
                    f"{{fileID: 21300000, "
                    f"guid: {guid_for(unity_path_for(part))}, type: 3}}",
                    part == "UIKit_Banner_Fill"))
                x += w + 8
            y += 96

        blocks_page = _obj(page_go, f"Page {page_index + 1} - {page_title}",
                           [page_rt], active=(page_index == 0))
        blocks_page += _rect(page_rt, page_go, canvas_rt, page_children,
                             (0, 0), (1, 1), (0.5, 0.5), (0, 0), (0, 0))
        body.append(blocks_page)
        body.extend(page_body)

    # ---- canvas ---------------------------------------------------------
    canvas = _obj(canvas_go, "Canvas",
                  [canvas_rt, 200003, 200004, 200005])
    canvas += _rect(canvas_rt, canvas_go, 0, canvas_children,
                    (0, 0), (0, 0), (0, 0), (0, 0), (0, 0))
    canvas += f"""--- !u!223 &200003
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 1
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0
--- !u!114 &200004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_CANVAS_SCALER}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: {REF_W}, y: {REF_H}}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 1
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
--- !u!114 &200005
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {canvas_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_GRAPHIC_RAYCASTER}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
"""
    blocks.append(canvas)
    blocks.extend(body)
    blocks.append(f"""--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {{fileID: 100003}}
  - {{fileID: {canvas_rt}}}
""")
    return "".join(blocks)
