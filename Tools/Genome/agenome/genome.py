"""Genome: a typed operator DAG with a compact byte form (amoebius-genome/0).

Byte form (what the compactness objective measures):
    0xA6 0x00 seed            header (magic, format version, global seed)
    per node:  opcode  ref*  param*
        ref   = distance back to the input node (1..255), 0 = no input   (one byte per input slot of the op)
        param = one byte per parameter of the op                         (count fixed by the op table)
Nodes are in topological order (inputs always point backwards), exactly like a werkkzeug op stack.
The root is the last `out` node; if there is none, an implicit root wires the last node of each type.

Compactness = len(zlib.compress(bytes, 9)) (a stand-in for kkrunchy).
"""
import json
import random
import zlib
from dataclasses import dataclass, field

from .ops import OPS, BY_CODE, OPS_LIST, ops_of_type

MAGIC = 0xA6
VERSION = 0


@dataclass
class Node:
    op: str
    inputs: list  # absolute node indices or None, one per input slot
    params: list  # bytes 0..255, one per param

    def copy(self):
        return Node(self.op, list(self.inputs), list(self.params))


@dataclass
class Genome:
    nodes: list = field(default_factory=list)
    seed: int = 0
    meta: dict = field(default_factory=dict)

    # ------------------------------------------------------------------ construction helpers
    def add(self, op, *inputs, **params):
        """Append a node; params by name (bytes), missing params take the op default. Returns the index."""
        o = OPS[op]
        ps = [p.default for p in o.params]
        names = [p.name for p in o.params]
        for k, v in params.items():
            if k not in names:
                raise KeyError(f"{op} has no param {k}; has {names}")
            ps[names.index(k)] = max(0, min(255, int(v)))
        ins = list(inputs) + [None] * (len(o.inputs) - len(inputs))
        if len(ins) > len(o.inputs):
            raise ValueError(f"{op} takes {len(o.inputs)} inputs")
        self.nodes.append(Node(op, ins, ps))
        return len(self.nodes) - 1

    def addv(self, op, *inputs, **values):
        """Like add, but params given as decoded VALUES (nearest byte is chosen)."""
        o = OPS[op]
        byname = {p.name: p for p in o.params}
        return self.add(op, *inputs, **{k: byname[k].encode(v) for k, v in values.items()})

    def copy(self):
        return Genome([n.copy() for n in self.nodes], self.seed, dict(self.meta))

    # ------------------------------------------------------------------ typing / repair
    def type_of(self, i):
        return OPS[self.nodes[i].op].out

    def repair(self):
        """Make every input point backwards to a node of the right type (nearest one), or None if optional/absent.
        Returns self. Mutation can produce anything; repair makes it a valid program."""
        for i, n in enumerate(self.nodes):
            o = OPS[n.op]
            n.params = [max(0, min(255, int(b))) for b in (list(n.params) + [p.default for p in o.params])[:len(o.params)]]
            n.inputs = (list(n.inputs) + [None] * len(o.inputs))[:len(o.inputs)]
            for s, want in enumerate(o.inputs):
                t = want.upper()
                j = n.inputs[s]
                ok = j is not None and 0 <= j < i and i - j <= 255 and self.type_of(j) == t
                if not ok:
                    j2 = None
                    lo = j if (j is not None and 0 <= j < i) else i - 1
                    for k in range(min(lo, i - 1), max(-1, i - 256), -1):
                        if self.type_of(k) == t:
                            j2 = k
                            break
                    if j2 is None and j is not None:
                        for k in range(i - 1, max(-1, i - 256), -1):
                            if self.type_of(k) == t:
                                j2 = k
                                break
                    n.inputs[s] = j2
        return self

    def root(self):
        for i in range(len(self.nodes) - 1, -1, -1):
            if self.nodes[i].op == "out":
                return i
        return None

    def ensure_root(self):
        if self.root() is None:
            self.add("out")
            self.repair()
        return self

    def live(self):
        """Indices reachable from the root (dead code is not compiled, but still costs bytes)."""
        r = self.root()
        if r is None:
            return set()
        seen, stack = set(), [r]
        while stack:
            i = stack.pop()
            if i in seen or i is None:
                continue
            seen.add(i)
            stack.extend(j for j in self.nodes[i].inputs if j is not None)
        return seen

    def prune(self):
        """Drop dead nodes (keeps the phenotype, shrinks the genome)."""
        keep = sorted(self.live())
        remap = {old: new for new, old in enumerate(keep)}
        nodes = []
        for old in keep:
            n = self.nodes[old].copy()
            n.inputs = [remap.get(j) if j is not None else None for j in n.inputs]
            nodes.append(n)
        self.nodes = nodes
        return self

    # ------------------------------------------------------------------ codecs
    def to_bytes(self):
        out = bytearray([MAGIC, VERSION, self.seed & 255])
        for i, n in enumerate(self.nodes):
            o = OPS[n.op]
            out.append(o.code)
            for j in n.inputs:
                out.append(0 if j is None else max(1, min(255, i - j)))
            out.extend(max(0, min(255, int(b))) for b in n.params)
        return bytes(out)

    @classmethod
    def from_bytes(cls, data):
        data = bytes(data)
        assert data[0] == MAGIC, "not an amoebius genome"
        g = cls(seed=data[2])
        k = 3
        while k < len(data):
            o = BY_CODE.get(data[k])
            k += 1
            if o is None:
                continue  # unknown opcode: skip one byte (forward compatible-ish)
            i = len(g.nodes)
            refs = list(data[k:k + len(o.inputs)])
            k += len(o.inputs)
            ps = list(data[k:k + len(o.params)])
            k += len(o.params)
            ps += [p.default for p in o.params[len(ps):]]
            g.nodes.append(Node(o.name, [None if r == 0 else i - r for r in refs], ps))
        return g.repair()

    def size(self):
        return len(zlib.compress(self.to_bytes(), 9))

    def raw_size(self):
        return len(self.to_bytes())

    def to_json(self, values=False):
        nodes = []
        for i, n in enumerate(self.nodes):
            o = OPS[n.op]
            d = {"id": i, "op": n.op, "in": n.inputs, "p": {p.name: b for p, b in zip(o.params, n.params)}}
            if values:
                d["v"] = {p.name: round(p.decode(b), 5) if isinstance(p.decode(b), float) else p.decode(b)
                          for p, b in zip(o.params, n.params)}
            nodes.append(d)
        return {"format": "amoebius-genome/0", "seed": self.seed, "meta": self.meta, "nodes": nodes,
                "bytes": self.raw_size(), "zbytes": self.size()}

    @classmethod
    def from_json(cls, d):
        if isinstance(d, str):
            d = json.loads(d)
        g = cls(seed=int(d.get("seed", 0)), meta=dict(d.get("meta", {})))
        ids = {}
        for k, nd in enumerate(d["nodes"]):
            o = OPS[nd["op"]]
            ids[nd.get("id", k)] = k
            p = nd.get("p", {})
            if isinstance(p, list):
                ps = (list(p) + [q.default for q in o.params])[:len(o.params)]
            else:
                ps = [p.get(q.name, q.default) for q in o.params]
            vals = nd.get("values")  # optional decoded values, translated to bytes
            if vals:
                for q_i, q in enumerate(o.params):
                    if q.name in vals:
                        ps[q_i] = q.encode(vals[q.name])
            ins = [None if j is None else ids.get(j, j) for j in nd.get("in", [])]
            g.nodes.append(Node(o.name, ins, [int(x) for x in ps]))
        return g.repair()

    def save(self, path):
        with open(path, "w") as f:
            json.dump(self.to_json(values=True), f, indent=1)

    @classmethod
    def load(cls, path):
        with open(path) as f:
            return cls.from_json(json.load(f))


# ---------------------------------------------------------------------- variation operators
def random_node(rng, t, g):
    o = rng.choice(ops_of_type(t))
    return Node(o.name, [None] * len(o.inputs), [rng.randrange(256) for _ in o.params])


def mutate(g, rng, rate=None, structural=0.35):
    """One mutation event, werkkzeug-editor-like: tweak params, swap an op, insert a filter, delete a node,
    rewire an input, duplicate a subgraph. Always returns a repaired copy."""
    g = g.copy()
    n = len(g.nodes)
    if n == 0:
        return g
    r = rng.random()
    if r > structural or n < 2:
        # parameter mutation: a few bytes, gaussian in byte space, occasionally uniform
        k = 1 + int(rng.expovariate(1.0))
        for _ in range(k):
            i = rng.randrange(n)
            if not g.nodes[i].params:
                continue
            j = rng.randrange(len(g.nodes[i].params))
            if rng.random() < 0.15:
                g.nodes[i].params[j] = rng.randrange(256)
            else:
                g.nodes[i].params[j] = int(max(0, min(255, round(g.nodes[i].params[j] + rng.gauss(0, 12)))))
        if rng.random() < 0.05:
            g.seed = rng.randrange(256)
        return g.repair()
    kind = rng.choice(["swap", "insert", "delete", "rewire", "dup", "insert"])
    i = rng.randrange(n)
    node = g.nodes[i]
    t = OPS[node.op].out
    if kind == "swap" and t != "O":
        cands = [o for o in ops_of_type(t) if o.name != node.op]
        if cands:
            o = rng.choice(cands)
            same = {p.name: b for p, b in zip(OPS[node.op].params, node.params)}
            g.nodes[i] = Node(o.name, (node.inputs + [None] * 3)[:len(o.inputs)],
                              [same.get(p.name, p.default if rng.random() < 0.5 else rng.randrange(256))
                               for p in o.params])
    elif kind == "insert":
        # insert a unary/binary filter of the same type right after node i, and point i's consumers at it
        t2 = t if t in "TSA" else rng.choice("TSA")
        filters = [o for o in ops_of_type(t2) if o.inputs and o.inputs[0] == t2]
        if t2 == t and filters:
            o = rng.choice(filters)
            new = Node(o.name, [i] + [None] * (len(o.inputs) - 1), [rng.randrange(256) for _ in o.params])
            _insert(g, i + 1, new)
            for k in range(i + 2, len(g.nodes)):
                g.nodes[k].inputs = [i + 1 if j == i else j for j in g.nodes[k].inputs]
            g.nodes[i + 1].inputs[0] = i
        else:
            o = rng.choice([o for o in ops_of_type(t2)])
            _insert(g, i, Node(o.name, [None] * len(o.inputs), [rng.randrange(256) for _ in o.params]))
    elif kind == "delete" and t != "O":
        _delete(g, i)
    elif kind == "rewire":
        if node.inputs:
            s = rng.randrange(len(node.inputs))
            node.inputs[s] = rng.randrange(i) if i > 0 else None
    elif kind == "dup" and t != "O":
        _insert(g, i + 1, node.copy())
    return g.repair()


def _insert(g, at, node):
    for n in g.nodes:
        n.inputs = [j + 1 if (j is not None and j >= at) else j for j in n.inputs]
    node.inputs = [j + 1 if (j is not None and j >= at) else j for j in node.inputs]
    g.nodes.insert(at, node)


def _delete(g, at):
    dead = g.nodes[at]
    repl = dead.inputs[0] if dead.inputs else None
    if repl is not None and OPS[g.nodes[repl].op].out != OPS[dead.op].out:
        repl = None
    del g.nodes[at]
    for n in g.nodes:
        new = []
        for j in n.inputs:
            if j is None:
                new.append(None)
            elif j == at:
                new.append(repl)
            elif j > at:
                new.append(j - 1)
            else:
                new.append(j)
        n.inputs = new


def crossover(a, b, rng):
    """Graft: take a's program, replace the subgraph feeding one of its root slots with b's subgraph for the same
    slot (typed subtree crossover on DAGs)."""
    a = a.copy().ensure_root()
    b = b.copy().ensure_root()
    ra, rb = a.root(), b.root()
    slot = rng.randrange(len(OPS["out"].inputs))
    src = b.nodes[rb].inputs[slot]
    if src is None:
        return a.repair()
    # collect b's subgraph under src in topological order
    sub, stack = set(), [src]
    while stack:
        i = stack.pop()
        if i is None or i in sub:
            continue
        sub.add(i)
        stack.extend(j for j in b.nodes[i].inputs if j is not None)
    order = sorted(sub)
    base = ra  # insert before a's root
    remap = {}
    for k, old in enumerate(order):
        n = b.nodes[old].copy()
        n.inputs = [remap.get(j) if j is not None else None for j in n.inputs]
        _insert(a, base + k, n)
        remap[old] = base + k
    a.nodes[a.root()].inputs[slot] = remap[src]
    return a.repair()


def random_genome(rng, size=8):
    g = Genome(seed=rng.randrange(256))
    for t in rng.choices("TTSSSAACP", k=size):
        g.nodes.append(random_node(rng, t, g))
    g.add("out")
    return g.repair()
