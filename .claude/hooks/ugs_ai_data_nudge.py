#!/usr/bin/env python3
"""ugs_ai_data_nudge.py - a UserPromptSubmit hook (.claude/settings.json): when a session's prompt is about UGS, tell
the person about the AI training data and how the team wants it stored (Yash, 2026-10-17: "next time Shombith comes to
try UGS things, point him out: if you want to store these AI things in UGS, explain a little and tell him to consult me
or Garrett; and in UGS don't compress the data too much - it is needed for precise AI").

It shows the person a one-line notice (systemMessage), naming Shombith when the git identity is his (Docs/TEAM.json),
and gives Claude the brief (Docs/AI_SYSTEM/AI_DATA_IN_UGS.md) to explain in a few lines. Once per session; never blocks
a prompt; always exits 0.

    python3 .claude/hooks/ugs_ai_data_nudge.py --self-test
"""
import json
import os
import re
import subprocess
import sys
import tempfile

BRIEF = 'Docs/AI_SYSTEM/AI_DATA_IN_UGS.md'
TERMS = (r'ugs', r'unity gaming services', r'cloud ?save', r'cloud ?code', r'remote ?config', r'economy service',
         r'leaderboards?', r'player ?data', r'ugs ?keys', r'ugskeys', r'cloud data', r'data ?store', r'player files?',
         r'ugs analytics', r'analytics events?', r'game ?overrides', r'triggers? service', r'r2 bucket', r'blob store')
PATTERN = re.compile(r'\b(?:' + '|'.join(TERMS) + r')\b', re.IGNORECASE)


def wants_nudge(prompt):
    return bool(prompt) and bool(PATTERN.search(prompt))


def root():
    return os.environ.get('CLAUDE_PROJECT_DIR') or os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))


def person(base):
    """The core-team member whose git identity this checkout uses, or None (cloud sessions often commit as Claude)."""
    try:
        email = subprocess.run(['git', '-C', base, 'config', 'user.email'], capture_output=True, text=True,
                               timeout=3).stdout.strip().lower()
        name = subprocess.run(['git', '-C', base, 'config', 'user.name'], capture_output=True, text=True,
                              timeout=3).stdout.strip().lower()
        with open(os.path.join(base, 'Docs', 'TEAM.json'), encoding='utf-8') as fh:
            people = json.load(fh).get('people', [])
    except Exception:
        return None
    for p in people:
        if email and email in [e.lower() for e in p.get('emails', [])]:
            return p.get('name')
        if name and name in [n.lower() for n in p.get('names', [])]:
            return p.get('name')
    return None


def notice(who):
    lead = ('Shombith - before you store anything AI-related in UGS: ' if who == 'Shombith'
            else 'UGS work: if AI training data (flight recordings) is ever stored in UGS, ')
    return (lead + 'keep it whole (lossless compression only, never fewer decimals / samples / columns) and check '
            'with Yash or Garrett first. Brief: ' + BRIEF)


CONTEXT = (
    'This prompt is about UGS. Yash asked that anyone doing UGS work (Shombith owns the UGS layer) is told about the AI '
    'training data, in a few lines, then left to decide: read ' + BRIEF + ' and say: (1) every hand-flown editor race is '
    'recorded by HumanFlightRecorder as a ~0.5 MB flight JSON (10 Hz: pose, sticks, state, camera, rivals, crystals; '
    'once per race: rings, rails, buttons), used for the racing line, skim choices, reaction lag and the vessel x mode '
    'chart; today they live on bugledger/<user> git branches (67 flights, ~33 MB); (2) if they go to UGS: small '
    'per-race summaries in Cloud Save (like REPLAY_INDEX on claude/match-replays-5t3dbr), the full flight unchanged in a '
    'file store; (3) THE RULE: keep the data whole - lossless compression (gzip/brotli/zstd) is fine, anything lossy '
    '(fewer decimals than 0.01, under 10 Hz, dropped columns, trimmed stretches, summaries only) is not, without asking; '
    '(4) players need their own opt-in and their data never goes in git; (5) consult Yash (recorder, tools) or Garrett '
    '(AI training owner); the storage choice is the open team decision T-20261015-0064. Say it once, briefly, then do '
    'what the person asked. If the UGS work has nothing to do with AI or flight data, one line is enough.')


def marker(session):
    safe = re.sub(r'[^A-Za-z0-9_.-]', '_', session or 'nosession')[:120]
    return os.path.join(tempfile.gettempdir(), 'cs-ugs-ai-data-nudge-' + safe)


def respond(payload, who=None):
    if not wants_nudge(payload.get('prompt') or ''):
        return None
    flag = marker(payload.get('session_id'))
    if os.path.exists(flag):
        return None
    try:
        open(flag, 'w').close()
    except OSError:
        pass
    who = who if who is not None else person(root())
    return {'systemMessage': notice(who),
            'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext': CONTEXT}}


def self_test():
    hits = ['add a Cloud Save key for replays', 'why does UGS sign-in fail', 'Remote Config for the AI levels',
            'store flights in a blob store', 'edit UGSKeys', 'leaderboard for Regatta', 'cloud code script']
    misses = ['', 'tune the Squirrel AI', 'flora growth law', 'fix the chase camera', 'bugs in the drug store']
    bad = [p for p in hits if not wants_nudge(p)] + [p for p in misses if wants_nudge(p)]
    if 'Shombith' not in notice('Shombith') or 'Shombith' in notice(None) or BRIEF not in notice(None):
        bad.append('notice wording')
    sid = 'selftest-%d' % os.getpid()
    try:
        first = respond({'prompt': 'UGS cloud save', 'session_id': sid}, who='Shombith')
        second = respond({'prompt': 'more cloud save', 'session_id': sid}, who='Shombith')
    finally:
        try:
            os.remove(marker(sid))
        except OSError:
            pass
    if not first or 'Shombith' not in first['systemMessage'] or second is not None:
        bad.append('once-per-session / names Shombith')
    if not os.path.exists(os.path.join(root(), BRIEF)):
        bad.append('brief missing: ' + BRIEF)
    print('ugs_ai_data_nudge self-test: ' + ('ok' if not bad else 'FAILED on ' + repr(bad)))
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
