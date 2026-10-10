#!/usr/bin/env python3
"""vessel_studio_nudge.py - a UserPromptSubmit hook (.claude/settings.json) that points anyone starting
vessel work at the Vessel Studio, the team's one place to SEE and PLAY a vessel and its AI before the
game is changed (the /vessel-studio skill, section 0; the user, 2026-10-09).

When a prompt names a vessel, a hull, vessel AI, play styles, cameras, a studio and so on, it:
  - shows the person a one-line notice (systemMessage), and
  - gives Claude the context to recommend the studio and load /vessel-studio.
It speaks once per session, so a long vessel session is not nagged on every prompt. It never blocks a
prompt and always exits 0: a broken nudge must not stop anyone working.

    python3 .claude/hooks/vessel_studio_nudge.py --self-test
"""
import json
import os
import re
import sys
import tempfile

STUDIO = 'https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa'

# The fleet (Docs/claude/ABOUT_PROJECT.md, the /vessel skill) plus the studio's own words.
VESSELS = ('manta', 'dolphin', 'rhino', 'serpent', 'sparrow', 'squirrel', 'urchin', 'grizzly', 'termite',
           'falcon', 'shrike', 'stoat', 'butterfly')
TERMS = (r'vessels?', r'hulls?', r'vessel ?studio', r'studios?', r'ai ?pilots?', r'autopilot', r'play ?styles?',
         r'ai (?:difficulty|difficulties|levels?|config|opponents?|rivals?)', r'skim ?race', r'flight feel',
         r'cameras?')
PATTERN = re.compile(r'\b(?:' + '|'.join(VESSELS + TERMS) + r')\b', re.IGNORECASE)

NOTICE = ('Vessel work: the team tests vessels and their AI visually in the Vessel Studio first (' + STUDIO +
          '). The /vessel-studio skill has the rules for building its panels.')
CONTEXT = (
    'This prompt is about vessel work. Before changing a vessel, its AI, its play styles or how it flies, '
    'RECOMMEND the Vessel Studio to the person in one line: the single claude.ai artifact ' + STUDIO + ' where '
    'every vessel and its AI are flown, watched and decided on (phone or PC) before the game changes, and say '
    'which studio page fits (squirrel, stoat, or a new one for this vessel). Load the /vessel-studio skill for '
    'anything about a studio page, its panels (Scene / Game / AI / Play Style Config, Input, Vessel Config), the '
    'universal AI race panel, cameras, difficulties, intensity ladders, domains or publishing; load /vessel '
    'for the Unity side. Never publish a second studio artifact. If the work has nothing to do with vessels, '
    'ignore this note.')


def wants_nudge(prompt):
    return bool(prompt) and bool(PATTERN.search(prompt))


def marker(session):
    safe = re.sub(r'[^A-Za-z0-9_.-]', '_', session or 'nosession')[:120]
    return os.path.join(tempfile.gettempdir(), 'cs-vessel-studio-nudge-' + safe)


def respond(payload):
    if not wants_nudge(payload.get('prompt') or ''):
        return None
    flag = marker(payload.get('session_id'))
    if os.path.exists(flag):
        return None
    try:
        open(flag, 'w').close()
    except OSError:
        pass   # cannot remember: nudge anyway
    return {'systemMessage': NOTICE,
            'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext': CONTEXT}}


def self_test():
    hits = ['tune the Squirrel AI', 'add a HULL morph', 'new studio for the Rhino', 'Skim Race difficulty',
            'Easy/Medium/Hard AI difficulty for Manta', 'make the play styles clearer', 'fix the chase camera']
    misses = ['', 'fix the party invite lobby', 'flora growth law', 'update the QA backlog', 'rhinoceros art']
    bad = [p for p in hits if not wants_nudge(p)] + [p for p in misses if wants_nudge(p)]
    sid = 'selftest-%d' % os.getpid()
    try:
        first = respond({'prompt': 'squirrel AI', 'session_id': sid})
        second = respond({'prompt': 'squirrel AI again', 'session_id': sid})
    finally:
        try:
            os.remove(marker(sid))
        except OSError:
            pass
    if not first or second is not None:
        bad.append('once-per-session')
    print('vessel_studio_nudge self-test: ' + ('ok' if not bad else 'FAILED on ' + repr(bad)))
    return 0 if not bad else 1


if __name__ == '__main__':
    if '--self-test' in sys.argv:
        sys.exit(self_test())
    try:
        out = respond(json.load(sys.stdin))
        if out:
            sys.stdout.write(json.dumps(out))
    except Exception:   # a broken nudge must never block a prompt
        pass
    sys.exit(0)
