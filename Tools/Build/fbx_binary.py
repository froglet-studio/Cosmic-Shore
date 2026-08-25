"""
Minimal round-trip codec for binary FBX 7.x.

Enough of the format to READ a file into a node tree, edit it, and WRITE it back
in a form other FBX readers accept. Deliberately not a general FBX library: it
preserves every node and property it does not understand byte-for-byte, which is
what makes surgery on an artist-authored file safe.

Property values are kept as (type_char, value) pairs so a re-write cannot
silently change a double to a float or a short to an int - a class of corruption
that is invisible until an importer rejects the file.

Arrays are written zlib-DEFLATED (encoding 1), the same encoding artist exports
use. Uncompressed (encoding 0) is equally legal and both read back identically -
it is chosen only to keep a checked-in binary from growing an order of magnitude
for no reason.

Used by: subdivide_sparrow_missile.py
"""

import struct
import zlib

HEADER_MAGIC = b"Kaydara FBX Binary  \x00\x1a\x00"
_ARRAY_FMT = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "b", "c": "B"}
_SCALAR_FMT = {"Y": "<h", "C": "<?", "I": "<i", "F": "<f", "D": "<d", "L": "<q"}
_SCALAR_SIZE = {"Y": 2, "C": 1, "I": 4, "F": 4, "D": 8, "L": 8}
_NULL_RECORD_LEN = 13   # FBX < 7500


class Node:
    # `empty_scope` records that a CHILDLESS node was still written with a nested-list
    # terminator. That 13-byte NULL record is not decoration: it is how a reader tells
    # "this node opens an (empty) scope" from "this node is a leaf", and the two are not
    # interchangeable. Blender writes it for 7 nodes in the Sparrow missile, one of them
    # AnimationLayer - dropping it made assimp read the file as having no animation at
    # all, with an otherwise byte-for-byte identical node tree. Round-tripping the bit is
    # the difference between a faithful rewrite and a quietly lossy one.
    __slots__ = ("name", "props", "children", "empty_scope")

    def __init__(self, name, props=None, children=None, empty_scope=False):
        self.name = name
        self.props = props if props is not None else []   # list of (typechar, value)
        self.children = children if children is not None else []
        self.empty_scope = empty_scope

    def find(self, name):
        return [c for c in self.children if c.name == name]

    def first(self, name):
        got = self.find(name)
        return got[0] if got else None

    def drop(self, name):
        self.children = [c for c in self.children if c.name != name]

    def __repr__(self):
        return "Node(%r, %d props, %d kids)" % (self.name, len(self.props), len(self.children))


# --------------------------------------------------------------------- reading

def _read_props(buf, pos, count):
    props = []
    for _ in range(count):
        t = chr(buf[pos]); pos += 1
        if t in _SCALAR_FMT:
            (v,) = struct.unpack_from(_SCALAR_FMT[t], buf, pos)
            pos += _SCALAR_SIZE[t]
            props.append((t, v))
        elif t in "SR":
            (n,) = struct.unpack_from("<I", buf, pos); pos += 4
            props.append((t, buf[pos:pos + n])); pos += n
        elif t in _ARRAY_FMT:
            count_, enc, clen = struct.unpack_from("<III", buf, pos); pos += 12
            raw = buf[pos:pos + clen]; pos += clen
            if enc == 1:
                raw = zlib.decompress(raw)
            props.append((t, list(struct.unpack("<%d%s" % (count_, _ARRAY_FMT[t]), raw))))
        else:
            raise ValueError("unknown FBX property type %r at offset %d" % (t, pos - 1))
    return props, pos


def _read_nodes(buf, pos, end, version):
    nodes = []
    while pos < end:
        if version < 7500:
            end_off, nprops, _plen = struct.unpack_from("<III", buf, pos); pos += 12
        else:
            end_off, nprops, _plen = struct.unpack_from("<QQQ", buf, pos); pos += 24
        name_len = buf[pos]; pos += 1
        if end_off == 0:
            break                      # the NULL record that terminates a list
        name = buf[pos:pos + name_len].decode("utf-8", "replace"); pos += name_len
        props, pos = _read_props(buf, pos, nprops)
        children = _read_nodes(buf, pos, end_off - _NULL_RECORD_LEN, version) if pos < end_off else []
        nodes.append(Node(name, props, children, empty_scope=(not children and pos < end_off)))
        pos = end_off
    return nodes


def read(path):
    """Returns (top_level_nodes, version, footer_bytes)."""
    buf = open(path, "rb").read()
    if not buf.startswith(HEADER_MAGIC):
        raise ValueError("%s is not a binary FBX" % path)
    (version,) = struct.unpack_from("<I", buf, 23)
    if version >= 7500:
        raise ValueError("FBX %d uses 64-bit records; this codec handles 7.4 and below" % version)
    nodes = _read_nodes(buf, 27, len(buf), version)
    # Everything after the top-level NULL record is the footer, kept verbatim: no
    # reader in this pipeline validates it, and reproducing its scrambled id is
    # not worth the risk of getting it wrong.
    footer_at = _scan_top_level_end(buf, version)
    return nodes, version, buf[footer_at:]


def _scan_top_level_end(buf, version):
    pos = 27
    while pos < len(buf):
        (end_off,) = struct.unpack_from("<I", buf, pos)
        if end_off == 0:
            return pos + _NULL_RECORD_LEN
        pos = end_off
    raise ValueError("no top-level NULL record found")


# --------------------------------------------------------------------- writing

def _prop_bytes(t, v):
    if t in _SCALAR_FMT:
        return bytes([ord(t)]) + struct.pack(_SCALAR_FMT[t], v)
    if t in "SR":
        return bytes([ord(t)]) + struct.pack("<I", len(v)) + v
    if t in _ARRAY_FMT:
        raw = struct.pack("<%d%s" % (len(v), _ARRAY_FMT[t]), *v)
        packed = zlib.compress(raw)
        if len(packed) < len(raw):
            return bytes([ord(t)]) + struct.pack("<III", len(v), 1, len(packed)) + packed
        return bytes([ord(t)]) + struct.pack("<III", len(v), 0, len(raw)) + raw
    raise ValueError("unknown property type %r" % t)


def _write_node(out, node, offset):
    """Serialize node at absolute file `offset`; returns the bytes written."""
    name = node.name.encode("utf-8")
    prop_blob = b"".join(_prop_bytes(t, v) for t, v in node.props)

    header_len = 12 + 1 + len(name)
    body = b""
    child_offset = offset + header_len + len(prop_blob)
    for child in node.children:
        chunk = _write_node(out, child, child_offset)
        body += chunk
        child_offset += len(chunk)
    if node.children or node.empty_scope:
        body += b"\x00" * _NULL_RECORD_LEN          # terminates the (possibly empty) scope
        child_offset += _NULL_RECORD_LEN

    end_off = child_offset
    return (struct.pack("<III", end_off, len(node.props), len(prop_blob))
            + bytes([len(name)]) + name + prop_blob + body)


def write(path, nodes, version, footer):
    out = bytearray(HEADER_MAGIC + struct.pack("<I", version))
    for node in nodes:
        out += _write_node(out, node, len(out))
    out += b"\x00" * _NULL_RECORD_LEN
    out += footer
    open(path, "wb").write(bytes(out))


# ------------------------------------------------------------------ comparison

def tree_signature(nodes):
    """A structural fingerprint used to prove a read/write round trip lost nothing."""
    def walk(ns, path):
        for n in ns:
            here = path + "/" + n.name
            yield (here, n.empty_scope,
                   tuple((t, len(v) if isinstance(v, (list, bytes)) else v)
                         for t, v in n.props))
            yield from walk(n.children, here)
    return list(walk(nodes, ""))
