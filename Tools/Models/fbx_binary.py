"""Minimal FBX binary (7.x) reader/writer — pure python, no deps."""
import struct, zlib


class Node:
    __slots__ = ("name", "props", "prop_types", "children")

    def __init__(self, name=b"", props=None, prop_types=None, children=None):
        self.name = name
        self.props = props if props is not None else []
        self.prop_types = prop_types if prop_types is not None else []
        self.children = children if children is not None else []

    def find(self, name):
        for c in self.children:
            if c.name == name:
                return c
        return None

    def findall(self, name):
        return [c for c in self.children if c.name == name]

    def __repr__(self):
        return f"<Node {self.name!r} props={len(self.props)} kids={len(self.children)}>"


# ---------------------------------------------------------------- reading

def _read_prop(f):
    t = f.read(1)
    if t in b"YCILFD":
        fmt = {b"Y": "<h", b"C": "<?", b"I": "<i", b"L": "<q", b"F": "<f", b"D": "<d"}[t]
        n = struct.calcsize(fmt)
        return t, struct.unpack(fmt, f.read(n))[0]
    if t in b"fdlic b":
        pass
    if t in b"fdlib" or t in b"i" or t in b"l" or t in b"c":
        length, encoding, comp_len = struct.unpack("<III", f.read(12))
        raw = f.read(comp_len)
        if encoding == 1:
            raw = zlib.decompress(raw)
        fmt = {b"f": "f", b"d": "d", b"l": "q", b"i": "i", b"b": "?", b"c": "B"}[t]
        return t, list(struct.unpack("<" + fmt * length, raw))
    if t == b"S" or t == b"R":
        (length,) = struct.unpack("<I", f.read(4))
        return t, f.read(length)
    raise ValueError(f"unknown prop type {t!r} @ {f.tell()}")


def _read_node(f, ver):
    if ver >= 7500:
        end_off, num_props, prop_len = struct.unpack("<QQQ", f.read(24))
        (name_len,) = struct.unpack("<B", f.read(1))
        hdr = 25
    else:
        end_off, num_props, prop_len = struct.unpack("<III", f.read(12))
        (name_len,) = struct.unpack("<B", f.read(1))
        hdr = 13
    if end_off == 0:
        return None
    name = f.read(name_len)
    node = Node(name)
    for _ in range(num_props):
        t, v = _read_prop(f)
        node.prop_types.append(t)
        node.props.append(v)
    while f.tell() < end_off:
        c = _read_node(f, ver)
        if c is None:
            break
        node.children.append(c)
    f.seek(end_off)
    return node


def read(path):
    with open(path, "rb") as f:
        head = f.read(23)
        assert head[:20] == b"Kaydara FBX Binary  ", head[:20]
        (ver,) = struct.unpack("<I", f.read(4))
        root = Node(b"")
        while True:
            n = _read_node(f, ver)
            if n is None:
                break
            root.children.append(n)
        return ver, root


# ---------------------------------------------------------------- writing

def _prop_bytes(t, v):
    if t in b"YCILFD":
        fmt = {b"Y": "<h", b"C": "<?", b"I": "<i", b"L": "<q", b"F": "<f", b"D": "<d"}[t]
        return t + struct.pack(fmt, v)
    if t in b"S" or t in b"R":
        return t + struct.pack("<I", len(v)) + v
    fmt = {b"f": "f", b"d": "d", b"l": "q", b"i": "i", b"b": "?", b"c": "B"}[t]
    raw = struct.pack("<" + fmt * len(v), *v)
    comp = zlib.compress(raw)
    if len(comp) < len(raw):
        return t + struct.pack("<III", len(v), 1, len(comp)) + comp
    return t + struct.pack("<III", len(v), 0, len(raw)) + raw


def _node_bytes(node, ver, offset):
    props = b"".join(_prop_bytes(t, v) for t, v in zip(node.prop_types, node.props))
    hdr = 25 if ver >= 7500 else 13
    body_start = offset + hdr + len(node.name)
    kids = b""
    cur = body_start + len(props)
    for c in node.children:
        b = _node_bytes(c, ver, cur)
        kids += b
        cur += len(b)
    if node.children:
        kids += b"\0" * (25 if ver >= 7500 else 13)
        cur += 25 if ver >= 7500 else 13
    end = cur
    if ver >= 7500:
        h = struct.pack("<QQQB", end, len(node.props), len(props), len(node.name))
    else:
        h = struct.pack("<IIIB", end, len(node.props), len(props), len(node.name))
    return h + node.name + props + kids


def write(path, ver, root):
    out = bytearray()
    out += b"Kaydara FBX Binary  \0" + b"\x1a\x00" + struct.pack("<I", ver)
    for n in root.children:
        out += _node_bytes(n, ver, len(out))
    out += b"\0" * (25 if ver >= 7500 else 13)
    # footer: unknown 16 bytes, pad to 16, version, 120 zeros, magic
    out += b"\xfa\xbc\xab\x09\xd0\xc8\xd4\x66\xb1\x76\xfb\x83\x1c\xf7\x26\x7e"
    # pad so the version field itself starts on a 16-byte boundary (verified against
    # Blender's own exports: footer id lands at i%16==9, version at %16==0)
    while len(out) % 16 != 0:
        out += b"\0"
    out += struct.pack("<I", ver)
    out += b"\0" * 120
    out += bytes([0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e,
                  0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b])
    with open(path, "wb") as f:
        f.write(bytes(out))


def dump(node, depth=0, maxdepth=6, out=None):
    if out is None:
        out = []
    if depth > maxdepth:
        return out
    for c in node.children:
        desc = []
        for t, v in zip(c.prop_types, c.props):
            if isinstance(v, list):
                desc.append(f"{t.decode()}[{len(v)}]")
            elif isinstance(v, bytes):
                desc.append(repr(v[:60]))
            else:
                desc.append(str(v))
        out.append("  " * depth + c.name.decode() + ": " + ", ".join(desc))
        dump(c, depth + 1, maxdepth, out)
    return out
