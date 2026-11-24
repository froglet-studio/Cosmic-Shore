#!/usr/bin/env python3
"""Rev 129 - the Poster card, picked, in our font; Skim Race's picture made to look like the cell (2026-11-23).

Usage: python3 r129_poster_cell.py <root> 129
Shombith on Rev 128: "I like the poster style but use our font. But for the insides I think you can do touch ups in
the skim race cards its too blue and it does not look the cell in any of the card bg."
The card is Rev 128 E (its kit is lifted from r128_game_cards.py) with the title in Aldrich, the font the style guide
reads in (headings and body). The three pictures come from r129_card_art.py (r129_assets/), uploaded to the canvas.
Writes R<rev>Compare, R<rev>Row and R<rev>Notes into <root>/project.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r128_game_cards.py')).read()
_src = _src[:_src.index("T = 'Rev %s' % REV")]
sys.argv = [sys.argv[0], root, REV]
exec(_src)  # STYLE (with the cards' CSS), card_e, ART, MODES, GRID, page, BACKDROP, HUB, NOLOGIC, out

# Our font: the title in Aldrich (the style guide's reading face), capitals, a little tracked.
STYLE += ('\n.o5 .title{font-family:\'Aldrich\',sans-serif;font-weight:400;letter-spacing:.06em;line-height:1}\n')

PIC = {
    'shipped': ART['Skim Race'],
    'regrade': '/_blob/fb91848a9ee7a760d3c54dfa3df453fd',
    'inside': '/_blob/b8a161954256bda0544ad7a8b24eae79',
    'outside': '/_blob/6de9bb16c6448c465a8b19d728be3fad',
}
LOOKS = [
    ('shipped', 'As shipped', 'The render as it ships: a purple-blue ground, the glass nucleus a big blue ball.'),
    ('regrade', 'No blue wash', 'The ground goes to the game’s deep navy, the nucleus to faint glass. The rings and trails keep their colours.'),
    ('inside', 'Inside the cell (recommended)', 'No blue wash, and the cell’s wall behind the race: the net of many-sided panes and the white dust you fly through.'),
    ('outside', 'The whole cell', 'The race inside the cell seen from outside: a wire ball of panes in dusty space, as the game shows a cell from afar.'),
]


def poster(pic, m='Skim Race', st=''):
    saved = ART[m]
    ART[m] = PIC[pic]
    try:
        return card_e(m, st)
    finally:
        ART[m] = saved


def compare(title):
    cols = []
    for k, (key, name, line) in enumerate(LOOKS):
        x = 64 + k * 452
        cols.append('<div class="abs" style="left:%dpx;top:156px;width:416px">'
                    '<div style="display:flex;gap:12px;align-items:baseline"><span class="lab"%s>%s</span></div>'
                    '<div style="font-size:15px;line-height:1.5;color:#B8C0F0;margin-top:8px;height:48px">%s</div>'
                    '<span class="capt" style="display:block;margin:6px 0 12px">Rest</span>'
                    '<div style="height:362px;position:relative"><div class="zoom">%s</div></div>'
                    '<span class="capt" style="display:block;margin:4px 0 12px">Hover</span>'
                    '<div style="height:362px;position:relative"><div class="zoom">%s</div></div></div>'
                    % (x, ' style="color:#E6E9FF"' if key == 'inside' else '', name, line, poster(key), poster(key, st='hv')))
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:100px;display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">Skim Race, the Poster card</span>'
            '<span class="lab">Our font; four pictures, the one we ship and three that look like the cell</span></div>'
            + ''.join(cols))
    return page(title, body, NOLOGIC)


def row(title):
    def card(m, st=''):
        return poster('inside', m, st) if m == 'Skim Race' else card_e(m, st)
    grid = ''.join('<div class="abs" style="left:%dpx;top:212px">%s</div>' % (64 + k * 381, card(m)) for k, m in enumerate(GRID))
    states = [('Rest', ''), ('Hover (held)', 'hv'), ('Pad focus', 'fc'), ('Locked', 'lk')]
    strip = ''.join('<div class="abs" style="left:%dpx;top:610px"><span class="capt" style="display:block;margin-bottom:16px">%s</span>%s</div>'
                    % (64 + k * 381, lab, card('Skim Race', st)) for k, (lab, st) in enumerate(states))
    side = ('<div class="abs" style="left:1600px;top:212px;width:256px;display:flex;flex-direction:column;gap:14px">'
            '<span class="lab" style="color:#E6E9FF">Poster &#183; inside the cell</span>'
            '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Skim Race wears the new picture. Dog Fight, Headlong and Joust '
            'still wear the pictures they ship with: the same touch-up in the card renderer would reach every card.</span></div>')
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:108px;display:flex;align-items:baseline;gap:22px;z-index:4">'
            '<span style="font-size:28px">Arcade</span><span class="lab">All games</span></div>'
            + grid +
            '<div class="abs" style="left:64px;right:64px;top:572px;height:1px;background:#222645"></div>'
            '<span class="lab abs" style="left:64px;top:584px">Skim Race, every state</span>' + strip + side)
    return page(title, body, NOLOGIC)


def notes(title):
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>' % head
                + ''.join('<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>')
    cols = [
        block('Asked (Shombith, 2026-11-23)', [
            '&#8220;I like the poster style but use our font. But for the insides I think you can do touch ups in the skim race cards its too blue and it does not look the cell in any of the card bg.&#8221;',
            'The Poster (Rev 128 E) is the card. Its title is now Aldrich, the face the style guide reads in; the labels stay Chakra Petch.']),
        block('Why the pictures miss the cell', [
            'The card renderer (Tools/Build/render_card_backgrounds.py) draws the arena&#8217;s prisms, rings and trails and the nucleus as glass, on a purple-blue ground.',
            'It never draws the cell&#8217;s wall, the net of many-sided panes that is the first thing you see in the game, nor the white dust. And the glass nucleus fills a third of Skim Race&#8217;s picture in blue.']),
        block('The three touch-ups', [
            '<b>No blue wash:</b> the ground to the game&#8217;s deep navy, the nucleus to faint glass; rings, trails and arrows keep their colours.',
            '<b>Inside the cell (recommended):</b> that, plus the wall&#8217;s panes behind the race and the dust. It reads as being in the cell, the way the game looks in flight.',
            '<b>The whole cell:</b> the cell from outside, a wire ball in dusty space. Clear, but the race gets smaller.',
            'Made here as touch-ups of the one Skim Race picture (Docs/MenuRedesignB/rounds/r129_card_art.py).']),
        block('After a pick', [
            'The pick goes into the card renderer, not into one PNG, so every card in the Arcade and Arena gets it at once and --check keeps it true.',
            'The Poster card itself is built in the Arcade and Arena card prefabs through /frogma-to-unity.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:36px">The Poster card, and the cell in its picture</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">'
            % REV + ''.join(cols) + '</div>')
    return page(title, body, NOLOGIC, 1920, 900)


T = 'Rev %s' % REV
boards = {
    'Compare': compare('%s - Compare: the Poster card in our font, Skim Race in four pictures, at rest and hovered' % T),
    'Row': row('%s Row - Row: the Arcade with Skim Race inside the cell, and every state - interactive' % T),
    'Notes': notes('%s - notes: why the pictures miss the cell, the touch-ups, what a pick changes' % T),
}
for k, html in boards.items():
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, k)), 'w') as f:
        f.write(html)
print('written', out, sorted(boards))
