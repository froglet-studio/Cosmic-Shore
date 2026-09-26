#!/usr/bin/env python3
"""
One definition of "the prism vertex-morph chain", shared by every wirer that has to look PAST it.

THE PROBLEM THIS SOLVES. Docs/PRISM_ANIMATION.md §4.7 splices vertex-DEFORMING Custom Functions
onto the tail of both live-prism graphs' vertex chains — §4.7.2's cradle, §4.7.3's wake, and
whatever comes next. Each one retargets the feeders of whatever was last, so every wirer that
asserts "VertexDescription.Position is fed by MY node" stops being true the moment a morph is
spliced in front of the blocks.

The first morph was absorbed by teaching three wirers to walk through ONE NAMED node
("directly, or through PrismCradleDeform"). The second morph broke all three again, because a
name list needs one edit per wirer per morph, forever — and each edit is in a file whose author
has no reason to be thinking about morphs at all. That is a defect amplifier, not a fix.

So the chain is identified STRUCTURALLY instead. A vertex morph is a Custom Function with
exactly the four Vector3 slots the shape requires — Position, Normal in; OutPosition, OutNormal
out — which is not a convention the morphs happen to share but the signature the splice itself
depends on (every morph takes the vertex the previous stage produced and returns the vertex the
next stage consumes). Nothing else on these graphs has it. So a wirer asks to be walked past
"any morphs" and never learns a single morph's name.

Used by wire_prism_flight_clock.py, wire_prism_jiggle_clock.py and wire_prism_suction_clock.py.
A NEW morph wirer needs no edit here and no edit there.
"""

# The signature every §4.7 vertex morph has, and the mapping from each output back to the input
# it is a function of. Both are properties of the splice contract, not of any one effect.
MORPH_SLOT_NAMES = {"Position", "Normal", "OutPosition", "OutNormal"}
MORPH_OUTPUT_FOR = {"Position": "OutPosition", "Normal": "OutNormal"}


def morph_slots(node, idx):
    """The node's slots as {display name: integer slot id}, or None if it is not a vertex morph.

    Read by NAME rather than by integer id on purpose: a Custom Function's integer ids follow its
    HLSL parameter order, so they are stable for one signature and not across signatures, and the
    whole point of this module is to survive a signature it has never seen.
    """
    if "CustomFunctionNode" not in node.get("m_Type", ""):
        return None
    slots = [idx[s["m_Id"]] for s in node.get("m_Slots", [])]
    if len(slots) != 4:
        return None
    by_name = {sd["m_DisplayName"]: sd for sd in slots}
    if set(by_name) != MORPH_SLOT_NAMES:
        return None
    if not all("Vector3MaterialSlot" in sd["m_Type"] for sd in by_name.values()):
        return None
    if by_name["Position"]["m_SlotType"] != 0 or by_name["Normal"]["m_SlotType"] != 0:
        return None
    if by_name["OutPosition"]["m_SlotType"] != 1 or by_name["OutNormal"]["m_SlotType"] != 1:
        return None
    return {name: sd["m_Id"] for name, sd in by_name.items()}


def walk_past_morphs(src, channel, sources, idx, limit=16):
    """Follow (node id, slot id) back through any chain of vertex morphs on `channel`.

    `channel` is "Position" or "Normal". Returns the first source that is NOT a morph's matching
    output — i.e. what the caller's own node should be, if the caller is wired correctly. A
    source that is not a morph at all is returned untouched, so a graph with no morphs on it
    behaves exactly as it did before this module existed.

    `limit` is a cycle guard rather than a depth policy: a malformed graph in which two morphs
    feed each other would otherwise spin here instead of failing the caller's assertion.
    """
    assert channel in MORPH_OUTPUT_FOR, f"unknown vertex channel {channel!r}"
    out_name = MORPH_OUTPUT_FOR[channel]
    seen = set()
    for _ in range(limit):
        if src is None:
            return None
        node_id, slot_id = src
        if node_id in seen:
            return src                      # cycle: hand it back and let the caller's assert speak
        node = idx.get(node_id)
        if node is None:
            return src
        slots = morph_slots(node, idx)
        if slots is None or slots[out_name] != slot_id:
            return src                      # not a morph, or not its output on this channel
        seen.add(node_id)
        src = sources.get((node_id, slots[channel]))
    return src
