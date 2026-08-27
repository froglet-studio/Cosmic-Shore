"""Unity serialized-field parity checker: asset YAML keys vs C# declarations.
Strips attributes first (the trap: `[Min(1)] public int Foo` defeats a `^public` regex)."""
import re, os

# TRAP: a global `\[...\]` strip also eats the `[]` in `float[] foo`, welding the type
# to the name (`floatfoo`) so the declaration stops matching. Attributes only ever
# precede a declaration, so anchor the strip to the START of the line.
ATTR = re.compile(r'^(?:\[[^\]\n]*\]\s*)+')
DECL = re.compile(
    r'^(?P<mods>(?:public|private|protected|internal|static|const|readonly|new|virtual|override|abstract|extern|unsafe|volatile)\s+)*'
    r'(?P<type>[\w<>\[\],\.\?]+)\s+(?P<name>\w+)\s*(?:;|=(?!=)|\{\s*get)')

def serialized_fields(cs_path):
    """Names Unity would serialize: [SerializeField] any-access, or public non-static/const."""
    out = set()
    for raw in open(cs_path, encoding='utf-8-sig').read().splitlines():
        line = raw.strip()
        if not line or line.startswith('//') or line.startswith('*') or line.startswith('///'):
            continue
        had_sf = 'SerializeField' in line or 'FormerlySerializedAs' in line
        stripped = ATTR.sub('', line).strip()
        m = DECL.match(stripped)
        if not m:
            continue
        mods = (m.group('mods') or '')
        if '{ get' in stripped or '=>' in stripped:      # properties are never serialized
            continue
        if 'static' in mods or 'const' in mods:
            continue
        if had_sf or 'public' in mods:
            out.add(m.group('name'))
    return out

def asset_docs(path):
    """Yield (script_guid, [top-level keys]) per MonoBehaviour doc, skipping PrefabInstance mods."""
    src = open(path, encoding='utf-8-sig').read()
    for doc in src.split('--- !u!'):
        if not doc.startswith('114 &'):
            continue
        g = re.search(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})', doc)
        if not g:
            continue
        keys = [k for k in re.findall(r'^  (\w+):', doc, re.M)
                if not k.startswith('m_') and k != 'serializedVersion']
        yield g.group(1), keys
