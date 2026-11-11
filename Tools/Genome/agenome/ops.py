"""Operator table for the Amoebius genome (format amoebius-genome/0).

A genome is a werkkzeug3-style operator DAG: every node is one operator with typed inputs and a fixed list of
byte parameters (0..255, like werkkzeug's packed parameter blocks). This table is the single source of truth:
the byte codec, the GLSL compiler, the agent simulator, the mutation operators and genome/SPEC.md all read it.

Types:
  T  texture   - a 2D field uv -> rgb (werkkzeug Bitmap_* ops)          -> GLSL  vec3 tN(vec2 uv)
  S  shape     - a signed distance field with a material (Mesh_* ops)   -> GLSL  float sdN(vec3 p), Mat mtN(vec3 p)
  A  agents    - populations of movers / static instances + trails      -> numpy simulation, instanced meshes
  C  camera    - a camera rig over time                                 -> numpy per-frame camera
  P  post      - exposure, bloom, fog, grading, feedback                -> post shader uniforms
  H  hud       - 2D overlay primitives, chained                         -> overlay shader uniforms
  O  output    - the root: wires scene, agents, camera, post, sky, hud
"""
from dataclasses import dataclass, field
import math

TYPES = "TSACPHO"


@dataclass(frozen=True)
class P:
    """One byte parameter. kind: lin | exp | int | enum | hue | scub (signed cubic) | bits"""
    name: str
    kind: str = "lin"
    lo: float = 0.0
    hi: float = 1.0
    default: int = 128

    def decode(self, b):
        b = max(0, min(255, int(b)))
        u = b / 255.0
        k = self.kind
        if k == "lin":
            return self.lo + (self.hi - self.lo) * u
        if k == "exp":
            return self.lo * (self.hi / self.lo) ** u
        if k == "int":
            return int(round(self.lo + (self.hi - self.lo) * u))
        if k == "enum":
            n = int(self.hi)
            return min(n - 1, int(u * n))
        if k == "hue":
            return u
        if k == "scub":
            s = (b - 127.5) / 127.5
            return self.hi * s * s * s
        if k == "bits":
            return b & int(self.hi)
        raise ValueError(k)

    def encode(self, v):
        """Nearest byte for a decoded value (used by translators writing seeds from real values)."""
        best, berr = 0, float("inf")
        for b in range(256):
            d = self.decode(b)
            e = abs(d - v)
            if e < berr:
                best, berr = b, e
        return best


@dataclass(frozen=True)
class Op:
    code: int
    name: str
    out: str
    inputs: tuple  # tuple of type letters; lowercase = optional
    params: tuple
    doc: str = ""
    lineage: str = ""


def _hsv(prefix="", h=128, s=128, v=200):
    return (P(prefix + "h", "hue", default=h), P(prefix + "s", "lin", 0, 1, s), P(prefix + "v", "exp", 0.01, 4.0, v))


def _hsvd(prefix="", h=128, s=128, v=128):
    """A colour whose value reaches the very dark linear colours a sky is made of."""
    return (P(prefix + "h", "hue", default=h), P(prefix + "s", "lin", 0, 1, s), P(prefix + "v", "exp", 0.0003, 3.0, v))


ANCH = P("anchor", "enum", 0, 9, 0)  # 0..8: TL T TR L C R BL B BR

OPS_LIST = [
    # ---------------------------------------------------------------- textures (werkkzeug3 genbitmap.cpp)
    Op(0x01, "tex.flat", "T", (), _hsv(), "constant colour", "Bitmap_Flat"),
    Op(0x02, "tex.gradient", "T", (), _hsv("a", 0, 0, 0) + _hsv("b", 0, 0, 230) + (
        P("angle", "lin", 0, 2 * math.pi, 64), P("pos", "lin", -1, 1, 128), P("length", "exp", 0.05, 4, 128),
        P("mode", "enum", 0, 3, 0)), "linear / radial / ring gradient", "Bitmap_Gradient"),
    Op(0x03, "tex.perlin", "T", (), (
        P("freq", "exp", 1, 32, 60), P("oct", "int", 1, 6, 128), P("persist", "lin", 0.2, 0.8, 128),
        P("seed", "int", 0, 255, 0), P("gamma", "exp", 0.25, 4, 128)) + _hsv("a", 0, 0, 0) + _hsv("b", 0, 0, 230),
       "fractal noise mapped between two colours", "Bitmap_Perlin"),
    Op(0x04, "tex.cell", "T", (), (
        P("count", "exp", 2, 64, 100), P("seed", "int", 0, 255, 0), P("mode", "enum", 0, 3, 0),
        P("gamma", "exp", 0.25, 4, 128)) + _hsv("a", 0, 0, 0) + _hsv("b", 0, 0, 230),
       "voronoi cells: F1, F2-F1 edges, crackle", "Bitmap_Cell"),
    Op(0x05, "tex.blend", "T", ("T", "T"), (P("mode", "enum", 0, 7, 0), P("amount", "lin", 0, 1, 255)),
       "add mul sub max min screen lerp-by-luma(b)", "Bitmap_Merge"),
    Op(0x06, "tex.distort", "T", ("T", "T"), (P("amount", "lin", 0, 0.5, 40),), "offset a's uv by b", "Bitmap_Distort"),
    Op(0x07, "tex.rotozoom", "T", ("T",), (P("angle", "lin", 0, 2 * math.pi, 0), P("zoom", "exp", 0.25, 8, 85),
                                          P("tx", "lin", 0, 1, 0), P("ty", "lin", 0, 1, 0)), "", "Bitmap_Rotate"),
    Op(0x08, "tex.hscb", "T", ("T",), (P("hue", "lin", -0.5, 0.5, 128), P("sat", "exp", 0.1, 4, 128),
                                      P("contrast", "exp", 0.25, 4, 128), P("bright", "exp", 0.25, 4, 128)),
       "hue / saturation / contrast / brightness", "Bitmap_HSCB"),
    Op(0x09, "tex.colorize", "T", ("T",), _hsv("a", 0, 0, 0) + _hsv("b", 0, 0, 230), "luma -> two-colour ramp",
       "Bitmap_Range"),
    Op(0x0A, "tex.twirl", "T", ("T",), (P("strength", "scub", 0, 10, 160), P("radius", "exp", 0.05, 1, 180)), "",
       "Bitmap_Twirl"),
    Op(0x0B, "tex.glowrect", "T", ("T",), (P("cx", "lin", 0, 1, 128), P("cy", "lin", 0, 1, 128),
                                          P("rx", "exp", 0.01, 1, 128), P("ry", "exp", 0.01, 1, 128),
                                          P("power", "exp", 0.25, 8, 128)) + _hsv(), "additive glow blob",
       "Bitmap_GlowRect"),
    Op(0x0C, "tex.bricks", "T", (), (P("tx", "int", 1, 32, 30), P("ty", "int", 1, 32, 60), P("fuge", "lin", 0, 0.3, 40),
                                    P("seed", "int", 0, 255, 0)) + _hsv("a", 0, 0, 60) + _hsv("b", 0, 0, 200), "",
       "Bitmap_Bricks"),
    Op(0x0D, "tex.mirror", "T", ("T",), (P("axes", "enum", 0, 3, 0),),
       "fold uv into a triangle wave on x, y or both: tiles without seams; as a sky it hides the equirect wrap",
       "Bitmap_Flip"),
    # ---------------------------------------------------------------- shapes (werkkzeug3 genmesh.cpp, as SDFs)
    Op(0x20, "sdf.sphere", "S", (), (P("r", "exp", 0.05, 200, 100),), "", "Mesh_Sphere"),
    Op(0x21, "sdf.box", "S", (), (P("sx", "exp", 0.02, 100, 100), P("sy", "exp", 0.02, 100, 100),
                                 P("sz", "exp", 0.02, 100, 100), P("round", "lin", 0, 0.5, 0)), "", "Mesh_Cube"),
    Op(0x22, "sdf.torus", "S", (), (P("R", "exp", 0.05, 100, 100), P("r", "lin", 0.01, 0.9, 60)), "", "Mesh_Torus"),
    Op(0x23, "sdf.cylinder", "S", (), (P("r", "exp", 0.02, 100, 80), P("h", "exp", 0.02, 200, 100)), "",
       "Mesh_Cylinder"),
    Op(0x24, "sdf.octa", "S", (), (P("r", "exp", 0.02, 100, 100),), "octahedron (crystal)", ""),
    Op(0x25, "sdf.gyroid", "S", (), (P("scale", "exp", 0.02, 5, 128), P("thick", "lin", 0.02, 1.0, 40),
                                    P("bound", "exp", 1, 400, 128)), "gyroid sheet inside a sphere (flora lattice)",
       "Cosmic Shore flora"),
    Op(0x26, "sdf.plane", "S", (), (P("theta", "lin", 0, math.pi, 0), P("phi", "lin", 0, 2 * math.pi, 0),
                                   P("offset", "scub", 0, 200, 128)), "", "Mesh_Grid"),
    Op(0x27, "sdf.shell", "S", ("S",), (P("thick", "exp", 0.005, 10, 80),), "onion: |d| - t (membranes)", ""),
    Op(0x28, "sdf.invert", "S", ("S",), (), "inside out (view a cell from inside)", "Mesh_Invert"),
    Op(0x30, "sdf.union", "S", ("S", "S"), (P("k", "exp", 0.001, 20, 0),), "smooth union", "Mesh_Add"),
    Op(0x31, "sdf.subtract", "S", ("S", "S"), (P("k", "exp", 0.001, 20, 0),), "a minus b", "Mesh_BSP"),
    Op(0x32, "sdf.intersect", "S", ("S", "S"), (P("k", "exp", 0.001, 20, 0),), "", "Mesh_BSP"),
    Op(0x38, "sdf.translate", "S", ("S",), (P("x", "scub", 0, 200), P("y", "scub", 0, 200), P("z", "scub", 0, 200)),
       "", "Mesh_Transform"),
    Op(0x39, "sdf.rotate", "S", ("S",), (P("ax", "lin", -math.pi, math.pi), P("ay", "lin", -math.pi, math.pi),
                                        P("az", "lin", -math.pi, math.pi)), "", "Mesh_Transform"),
    Op(0x3A, "sdf.scale", "S", ("S",), (P("s", "exp", 0.05, 20, 128),), "", "Mesh_Transform"),
    Op(0x3B, "sdf.repeat", "S", ("S",), (P("axes", "bits", 0, 7, 7), P("px", "exp", 0.1, 200, 128),
                                        P("py", "exp", 0.1, 200, 128), P("pz", "exp", 0.1, 200, 128)),
       "domain repetition (werkkzeug Multiply)", "Mesh_Multiply"),
    Op(0x3C, "sdf.polar", "S", ("S",), (P("count", "int", 1, 32, 40), P("axis", "enum", 0, 3, 1)),
       "radial repetition", "Mesh_Multiply2"),
    Op(0x3D, "sdf.twist", "S", ("S",), (P("amount", "scub", 0, 2, 160), P("axis", "enum", 0, 3, 1)), "",
       "Mesh_Bend"),
    Op(0x3E, "sdf.displace", "S", ("S",), (P("freq", "exp", 0.05, 20, 128), P("amp", "exp", 0.001, 5, 60),
                                          P("seed", "int", 0, 255, 0)), "noise displacement", "Mesh_Perlin"),
    Op(0x3F, "sdf.mirror", "S", ("S",), (P("axes", "bits", 0, 7, 1),), "", ""),
    Op(0x40, "sdf.spin", "S", ("S",), (P("axis", "enum", 0, 3, 1), P("speed", "scub", 0, 3, 160)),
       "rotate over time", "anim"),
    Op(0x41, "sdf.bob", "S", ("S",), (P("axis", "enum", 0, 3, 1), P("amp", "exp", 0.001, 20, 80),
                                     P("freq", "exp", 0.05, 5, 100)), "oscillate over time", "anim"),
    Op(0x49, "sdf.rim", "S", ("S",), _hsv("f", 170, 200, 15) + _hsv("r", 140, 160, 200) + (
        P("power", "exp", 0.5, 16, 150), P("emit", "exp", 0.05, 10, 140)),
       "unlit fresnel rim: mix(face, rim, (1-N.V)^power), how the game shades prisms and crystals",
       "Cosmic Shore prism shader"),
    Op(0x48, "sdf.material", "S", ("S", "t"), _hsv("a", 0, 0, 120) + _hsv("e", 0, 0, 0) + (
        P("emit", "exp", 0.001, 10, 0), P("glow", "exp", 0.0001, 2, 0), P("falloff", "exp", 0.2, 50, 128),
        P("texscale", "exp", 0.005, 10, 128), P("texmix", "lin", 0, 1, 255), P("spec", "lin", 0, 1, 60)),
       "albedo, emission, volumetric glow, triplanar texture", "Material_Add"),
    # ---------------------------------------------------------------- agents (boids / trails / instances)
    Op(0x50, "ag.spawn", "A", (), (
        P("n", "exp", 1, 512, 128), P("radius", "exp", 0.5, 300, 128), P("shape", "enum", 0, 4, 0),
        P("speed", "exp", 0.1, 200, 128), P("seed", "int", 0, 255, 0), P("teams", "int", 1, 4, 255),
        P("hue0", "hue", default=110), P("huestep", "lin", 0, 1, 64), P("sat", "lin", 0, 1, 200),
        P("val", "exp", 0.05, 4, 200)), "movers: ball / shell / ring / line; teams get hues", "boids"),
    Op(0x51, "ag.boids", "A", ("A",), (P("coh", "exp", 0.001, 10, 60), P("ali", "exp", 0.001, 10, 100),
                                      P("sep", "exp", 0.001, 10, 100), P("radius", "exp", 0.1, 100, 128),
                                      P("turn", "exp", 0.05, 20, 128)), "Reynolds flocking", "boids"),
    Op(0x52, "ag.wander", "A", ("A",), (P("freq", "exp", 0.02, 5, 100), P("amp", "exp", 0.01, 50, 128)),
       "smooth random steering (a pilot)", ""),
    Op(0x53, "ag.attract", "A", ("A",), (P("cx", "scub", 0, 200), P("cy", "scub", 0, 200), P("cz", "scub", 0, 200),
                                        P("strength", "exp", 0.001, 20, 100), P("orbit", "scub", 0, 2, 128),
                                        P("radius", "exp", 0.1, 300, 128)),
       "spring toward a sphere around a centre, with tangential swirl", ""),
    Op(0x54, "ag.bound", "A", ("A",), (P("radius", "exp", 1, 1000, 128), P("stiff", "exp", 0.01, 100, 128)),
       "stay inside the cell", ""),
    Op(0x55, "ag.trail", "A", ("A",), (
        P("interval", "exp", 0.02, 2, 60), P("sx", "exp", 0.01, 20, 100), P("sy", "exp", 0.01, 20, 100),
        P("sz", "exp", 0.01, 20, 140), P("life", "exp", 0.2, 120, 160), P("emit", "exp", 0.01, 20, 128),
        P("colormode", "enum", 0, 2, 0), P("edge", "lin", 0, 4, 0), P("mesh", "enum", 0, 6, 0)) + _hsv(),
       "lay prisms behind each mover (seconds between prisms); edge = neon edge emission; mesh as ag.body",
       "Cosmic Shore prism trails"),
    Op(0x56, "ag.body", "A", ("A",), (P("shape", "enum", 0, 6, 86), P("sx", "exp", 0.01, 20, 100),
                                     P("sy", "exp", 0.01, 20, 80), P("sz", "exp", 0.01, 20, 140),
                                     P("emit", "exp", 0.01, 20, 100), P("colormode", "enum", 0, 2, 0),
                                     P("edge", "lin", 0, 4, 0)) + _hsv(),
       "visible body per mover: box / octa / dart / icosa / ring / pyramid", "vessels"),
    Op(0x57, "ag.merge", "A", ("A", "A"), (), "two populations in one", ""),
    Op(0x58, "ag.scatter", "A", (), (
        P("n", "exp", 1, 4096, 128), P("radius", "exp", 0.5, 400, 128), P("shape", "enum", 0, 4, 0),
        P("mesh", "enum", 0, 6, 43), P("size", "exp", 0.01, 50, 100), P("sizevar", "lin", 0, 1, 128),
        P("spin", "scub", 0, 3, 128), P("emit", "exp", 0.01, 20, 100), P("seed", "int", 0, 255, 0),
        P("huevar", "lin", 0, 1, 0), P("edge", "lin", 0, 4, 0)) + _hsv(),
       "static instances: crystals, motes, flora bits", "Mesh_Multiply"),
    Op(0x59, "ag.rim", "A", ("A",), (P("power", "exp", 0.5, 16, 150), P("bright", "exp", 0.1, 8, 128)),
       "fresnel rim shading for every instance (trail prisms, bodies, scatter) of the populations under it: a bright "
       "rim in the population colour over a dark complementary face, as the game shades prisms and crystals",
       "Cosmic Shore prism shader"),
    # ---------------------------------------------------------------- the game's own mechanics (agenome/game.py)
    Op(0x5A, "ag.course", "A", (), (
        P("kind", "enum", 0, 4, 0), P("radius", "exp", 100, 1100, 207), P("gates", "int", 4, 32, 36),
        P("perturb", "lin", 0, 300, 0), P("mouth", "exp", 8, 120, 103), P("seed", "int", 0, 255, 0)),
       "closed course: flat octagon / Catmull loop / gate loop / harmonic ring, with gate frames", "Skim Race, Regatta"),
    Op(0x5B, "ag.rail", "A", ("A",), (
        P("lanes", "int", 1, 3, 0), P("offset", "lin", 0, 40, 0), P("twist", "lin", 0, 2, 0),
        P("sx", "exp", 0.5, 24, 197), P("sy", "exp", 0.5, 24, 46), P("sz", "exp", 0.5, 24, 118),
        P("spacing", "exp", 4, 24, 156), P("state", "enum", 0, 4, 140), P("colour", "enum", 0, 2, 0),
        P("marker", "lin", 1, 3, 128), P("emit", "exp", 0.05, 10, 100)) + _hsv("", 163, 150, 180),
       "static prisms along the course: the Skim Race ribbon, the Regatta rails (state super = stellated mesh)",
       "RegattaCourse, SpawnableWaypointTrack"),
    Op(0x5C, "ag.gate", "A", ("A",), (P("emit", "exp", 0.05, 10, 128),) + _hsv("", 163, 150, 200),
       "a ring per gate at the mouth radius", "RaceGateRing"),
    Op(0x5D, "ag.crystal", "A", ("A",), (
        P("jitter", "lin", 0, 60, 149), P("radius", "exp", 2, 30, 234), P("inset", "lin", 0, 60, 110),
        P("lift", "lin", -30, 30, 128), P("emit", "exp", 0.05, 10, 150)),
       "one crystal ring per vessel in its colour: collect, +boost, drop a prism ring, jump to the next anchor",
       "CrystalManager, CrystalRaceRing"),
    Op(0x5E, "ag.vessel", "A", ("A",), (
        P("count", "int", 1, 12, 46), P("domains", "int", 1, 3, 255), P("cruise", "exp", 10, 200, 153),
        P("minspeed", "lin", 0, 20, 0), P("turn", "lin", 20, 180, 159), P("roll", "lin", 0, 180, 184),
        P("rotthr", "lin", 0, 0.5, 0), P("follow", "exp", 0.5, 12, 88), P("accel", "exp", 0.5, 6, 113),
        P("boostmax", "lin", 1, 8, 146), P("decay", "exp", 0.03, 1, 167),
        P("ha", "hue", default=137), P("hb", "hue", default=199), P("hc", "hue", default=24),
        P("sat", "lin", 0, 1, 255), P("val", "exp", 0.05, 4, 190)),
       "the hull integrator: commanded frame, lagging nose, speed lerp, decaying boost; three domain hues",
       "VesselTransformer"),
    Op(0x5F, "ag.pilot", "A", ("A",), (
        P("law", "enum", 0, 2, 255), P("look", "lin", 0, 1.5, 145), P("lookmin", "exp", 20, 120, 99),
        P("lookmax", "exp", 100, 600, 216), P("lead", "lin", 1, 4, 128), P("maxlead", "lin", 0, 90, 65),
        P("gain", "exp", 0.02, 0.3, 135), P("height", "lin", 0, 12, 93), P("thrlo", "lin", 0, 1, 51),
        P("thrhi", "lin", 0, 1, 255), P("bend", "exp", 50, 600, 180), P("bank", "exp", 0.1, 10, 128)),
       "stick input: bang-bang pursuit or lead-compensated line follow, bending through the crystal",
       "SkimRaceDriver, AIPilot"),
    Op(0x60, "ag.skim", "A", ("A",), (P("radius", "lin", 2, 20, 78), P("add", "lin", 0, 0.5, 51),
                                      P("pickup", "lin", 0, 2, 102)),
       "boost from grazing prisms and from crystals; a hull hit resets it", "SkimmerBoostPrismEffect"),
    Op(0x61, "ag.lay", "A", ("A",), (
        P("wave", "exp", 1, 20, 166), P("sx", "exp", 0.2, 24, 245), P("sy", "exp", 0.2, 8, 91),
        P("sz", "exp", 0.2, 12, 206), P("gap", "lin", 0, 20, 236), P("offset", "lin", 0, 20, 0),
        P("delay", "lin", 0, 3, 170), P("grow", "exp", 0.5, 5, 101), P("emit", "exp", 0.05, 10, 128)),
       "lay prisms per distance in the hull frame, two rails with a gap, persistent, exponential grow-in",
       "VesselPrismController"),
    Op(0x64, "ag.shards", "A", ("A",), (
        P("spacing", "exp", 60, 300, 110), P("radius", "exp", 200, 2000, 198), P("scale", "lin", 0, 20, 128),
        P("emit", "exp", 0.05, 10, 40)) + _hsv("", 170, 60, 120),
       "static needles filling the cell, aimed at the crystal and growing with distance from it", "SnowChanger"),
    # ---------------------------------------------------------------- cameras
    Op(0x70, "cam.chase", "C", ("A",), (P("agent", "int", 0, 15, 0), P("dist", "exp", 0.1, 200, 128),
                                       P("height", "scub", 0, 100, 150), P("lag", "lin", 0, 0.98, 160),
                                       P("fov", "lin", 20, 120, 128), P("look", "exp", 0.01, 20, 100)),
       "third-person chase cam behind one mover", "game camera"),
    Op(0x71, "cam.orbit", "C", (), (P("radius", "exp", 0.5, 1000, 128), P("height", "scub", 0, 400, 140),
                                   P("speed", "scub", 0, 2, 150), P("fov", "lin", 20, 120, 128),
                                   P("phase", "lin", 0, 2 * math.pi, 0)), "", "demo camera"),
    Op(0x72, "cam.fly", "C", (), (P("radius", "exp", 0.5, 1000, 128), P("speed", "exp", 0.01, 5, 100),
                                 P("fov", "lin", 20, 120, 128), P("seed", "int", 0, 255, 0)),
       "smooth noise fly-through", "demo camera"),
    Op(0x74, "cam.lens", "C", ("C",), (P("panini", "lin", 0, 1, 179), P("crop", "lin", 0, 1, 255)),
       "Panini projection over the camera's image, as URP's post effect (the game uses distance 0.7, crop to fit)",
       "URP PaniniProjection"),
    Op(0x73, "cam.shake", "C", ("C",), (P("amp", "exp", 0.0001, 2, 60), P("freq", "exp", 0.5, 30, 128)), "", ""),
    Op(0x75, "cam.attach", "C", ("A",), (P("agent", "int", 0, 11, 0), P("dist", "exp", 5, 300, 76),
                                        P("height", "lin", 0, 20, 0), P("fov", "lin", 40, 110, 182)),
       "camera rigidly attached in the hull frame (rolls with it), as the game's FixedCamera", "CustomCameraController"),
    Op(0x76, "cam.tunnel", "C", ("C",), (P("lo", "lin", 0, 150, 119), P("hi", "lin", 100, 400, 153),
                                        P("fovdrop", "lin", 0, 40, 159), P("panini", "lin", 0, 1, 179),
                                        P("pdrop", "lin", 0, 1, 128)),
       "speed tunnel: FOV and Panini follow the followed vessel's speed", "SpeedTunnelConfig"),
    # ---------------------------------------------------------------- post (werkkzeug IPP)
    Op(0x80, "post.grade", "P", (), (
        P("exposure", "exp", 0.25, 8, 128), P("thresh", "lin", 0, 2, 100), P("bloom", "exp", 0.001, 4, 128),
        P("bloomrad", "exp", 0.5, 8, 128)) + _hsv("f", 160, 128, 20) + (
        P("fog", "exp", 1e-5, 1, 60), P("vignette", "lin", 0, 1.5, 80), P("sat", "exp", 0.25, 4, 128),
        P("contrast", "exp", 0.5, 2, 128), P("feedback", "lin", 0, 0.95, 0), P("grain", "lin", 0, 0.2, 0),
        P("aberr", "lin", 0, 0.02, 0)), "exposure, bloom, fog, vignette, grading, feedback", "GenIPP"),
    Op(0x81, "post.urp", "P", (), (P("thresh", "lin", 0, 1, 51), P("intensity", "exp", 0.05, 16, 173),
        P("scatter", "lin", 0, 1, 173), P("exposure", "exp", 0.25, 4, 128)) + _hsv("f", 160, 128, 20) + (
        P("fog", "exp", 1e-6, 0.1, 0),), "the game's own post: soft-knee pyramid bloom, no tonemap, sRGB out", "URP Bloom"),
    # ---------------------------------------------------------------- sky layers (HyperSeaSkybox.shader, by direction)
    # T ops evaluated on the view direction (uv is the lat-long of it), so they are seamless on the sphere and still
    # compose with every tex.* op. Each adds its layer to the optional input. sky.base also sets what the layers share:
    # the galactic plane's normal and width, and the drift speed.
    Op(0x0E, "sky.base", "T", (), _hsvd("d", 170, 200, 60) + _hsvd("a", 180, 180, 90) + (
        P("ntheta", "lin", 0, math.pi, 12), P("nphi", "lin", 0, 2 * math.pi, 40), P("width", "exp", 0.005, 0.5, 124),
        P("drift", "lin", 0, 0.5, 51)), "deep colour + ambient noise; galactic normal, width, drift", "HyperSea ambient"),
    Op(0x0F, "sky.band", "T", ("t",), _hsvd("c", 20, 180, 200) + _hsvd("e", 160, 110, 210) + (
        P("nscale", "exp", 1, 32, 190), P("nstr", "lin", 0, 2, 112)), "galactic band", "HyperSea galactic plane"),
    Op(0x10, "sky.stars", "T", ("t",), (P("density", "exp", 8, 128, 196), P("bright", "exp", 0.01, 4, 135),
        P("base", "lin", 0, 0.5, 0), P("boost", "lin", 0, 0.5, 45), P("conc", "exp", 0.5, 8, 128)),
       "stars and small galaxies, denser near the plane", "HyperSea stars"),
    Op(0x11, "sky.nebula", "T", ("t",), (P("scale", "exp", 0.5, 8, 148),) + _hsvd("a", 170, 240, 200)
       + _hsvd("b", 150, 235, 200) + _hsvd("c", 190, 215, 200), "three domain-warped clouds", "HyperSea nebulae"),
    Op(0x12, "sky.dust", "T", ("t",), (P("scale", "exp", 0.5, 16, 170), P("strength", "lin", 0, 1, 241)),
       "dark lanes near the plane (multiplies the input)", "HyperSea dust"),
    Op(0x13, "sky.core", "T", ("t",), (P("theta", "lin", 0, math.pi, 135), P("phi", "lin", 0, 2 * math.pi, 12))
       + _hsvd("c", 26, 150, 220) + _hsvd("h", 20, 145, 190) + (P("size", "exp", 0.003, 0.5, 113),
        P("halo", "exp", 0.03, 1, 168)), "galactic core on the plane", "HyperSea core"),
    Op(0x14, "sky.galaxy", "T", ("t",), (P("theta", "lin", 0, math.pi, 100), P("phi", "lin", 0, 2 * math.pi, 86))
       + _hsvd("c", 32, 200, 180) + _hsvd("n", 17, 160, 180) + (P("size", "exp", 0.05, 1, 177),),
       "one inclined spiral galaxy", "HyperSea Andromeda"),
    Op(0x15, "sky.web", "T", ("t",), (P("scale", "exp", 2, 48, 144), P("sharp", "exp", 2, 64, 165),
        P("interior", "lin", 0, 0.5, 77)) + _hsvd("", 166, 255, 190), "3D Voronoi edge web", "HyperSea cell overlay"),
    Op(0x16, "sky.haze", "T", ("t",), _hsvd("", 170, 255, 100) + (P("height", "lin", -1, 1, 216),
        P("falloff", "exp", 1, 32, 153)), "haze band by height", "HyperSea atmosphere bridge"),
    # ---------------------------------------------------------------- hud (werkkzeug genoverlay)
    Op(0x90, "hud.rect", "H", ("h",), (ANCH, P("x", "lin", -0.5, 0.5, 128), P("y", "lin", -0.5, 0.5, 128),
                                      P("w", "exp", 0.003, 1, 100), P("hh", "exp", 0.003, 1, 100)) + _hsv() + (
        P("alpha", "lin", 0, 1, 200), P("outline", "lin", 0, 0.03, 0)), "", "overlay"),
    Op(0x91, "hud.ring", "H", ("h",), (ANCH, P("x", "lin", -0.5, 0.5, 128), P("y", "lin", -0.5, 0.5, 128),
                                      P("r", "exp", 0.005, 0.5, 100), P("thick", "exp", 0.001, 0.2, 80),
                                      P("arc", "lin", 0, 1, 255)) + _hsv() + (P("alpha", "lin", 0, 1, 200),), "",
       "overlay"),
    Op(0x92, "hud.bar", "H", ("h",), (ANCH, P("x", "lin", -0.5, 0.5, 128), P("y", "lin", -0.5, 0.5, 128),
                                     P("w", "exp", 0.003, 1, 120), P("hh", "exp", 0.003, 1, 40),
                                     P("segs", "int", 1, 20, 0), P("fill", "lin", 0, 1, 200)) + _hsv() + (
        P("alpha", "lin", 0, 1, 200),), "segmented bar (element meters)", "overlay"),
    Op(0x93, "hud.text", "H", ("h",), (ANCH, P("x", "lin", -0.5, 0.5, 128), P("y", "lin", -0.5, 0.5, 128),
                                      P("size", "exp", 0.005, 0.1, 100), P("chars", "int", 1, 40, 40),
                                      P("rows", "int", 1, 8, 0), P("seed", "int", 0, 255, 0)) + _hsv() + (
        P("alpha", "lin", 0, 1, 220),), "glyph-like text block", "Bitmap_Text"),
    # ---------------------------------------------------------------- root
    Op(0xF0, "out", "O", ("s", "a", "c", "p", "t", "h"), (
        P("sky", "exp", 0.001, 4, 128), P("steps", "int", 16, 128, 100), P("far", "exp", 10, 4000, 128),
        P("ltheta", "lin", 0, math.pi, 60), P("lphi", "lin", 0, 2 * math.pi, 40), P("ambient", "lin", 0, 1, 50),
        P("warmup", "lin", 0, 30, 60))
       + _hsv("l", 0, 0, 200), "root: scene, agents, camera, post, sky texture, hud", "KOp root"),
]

OPS = {o.name: o for o in OPS_LIST}
BY_CODE = {o.code: o for o in OPS_LIST}
assert len(BY_CODE) == len(OPS_LIST), "duplicate opcode"
for _o in OPS_LIST:
    _names = [p.name for p in _o.params]
    assert len(set(_names)) == len(_names), _o.name


def ops_of_type(t):
    return [o for o in OPS_LIST if o.out == t]


def spec_markdown():
    lines = ["| code | op | out | inputs | params (byte -> value) | lineage |", "|---|---|---|---|---|---|"]
    for o in OPS_LIST:
        ps = ", ".join(f"{p.name}:{p.kind}[{p.lo:g},{p.hi:g}]" if p.kind not in ("hue", "enum", "bits")
                       else f"{p.name}:{p.kind}{'' if p.kind == 'hue' else int(p.hi)}" for p in o.params)
        ins = ",".join(o.inputs) or "-"
        lines.append(f"| 0x{o.code:02X} | `{o.name}` | {o.out} | {ins} | {ps} | {o.lineage} |")
    return "\n".join(lines)
