#!/usr/bin/env python3
"""team_context.py - a UserPromptSubmit hook (.claude/settings.json) that makes every session aware of the other
collaborators' work before it changes anything (Docs/TEAM.md; Yash, 2026-10-15).

  - On a session's FIRST prompt, it gives Claude the team (from Docs/TEAM.json) and the collaboration rules:
    run Tools/Build/who_works_where.py on the paths you will change, build on others' work, never override it
    silently, do not reinvent.
  - On the first prompt that is about Amoebius, it adds the tool's status (UNDER EVALUATION,
    Docs/TOOLING_DECISIONS.md). If Docs/TOOLING_FEEDBACK.md has no Amoebius row from the last three days, it also
    tells Claude to ask the person, at the end, whether Amoebius helped. Vessel Studio prompts get the same
    from vessel_studio_nudge.py.

Context for Claude only (no message to the person). Once per session each. It never blocks a prompt and always exits 0.

    python3 .claude/hooks/team_context.py --self-test
"""
import datetime
import json
import os
import re
import sys
import tempfile

AMOEBIUS = re.compile(r'\b(?:amoebius|prisma(?:\.exe)?|port/|launcher|cosmicshore\.(?:engine|player|launcher|mcp)|'
                      r'studio server|artifact library)\b', re.IGNORECASE)
FEEDBACK_DAYS = 3


def root():
    return os.environ.get('CLAUDE_PROJECT_DIR') or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def team_summary(base):
    try:
        with open(os.path.join(base, 'Docs', 'TEAM.json'), encoding='utf-8') as fh:
            team = json.load(fh)
    except (OSError, ValueError):
        return 'the team in Docs/TEAM.md'
    core = set(team.get('core', []))
    parts = []
    for p in team.get('people', []):
        if p.get('name') in core:
            role = (p.get('role') or '').split('. ')[0]
            if len(role) > 160:
                role = role[:157].rsplit(' ', 1)[0] + '...'
            parts.append(f"{p['name']} ({role.rstrip('.')})")
    return '; '.join(parts) or 'the team in Docs/TEAM.md'


def recent_feedback(base, tool, today=None):
    """True when Docs/TOOLING_FEEDBACK.md has a row for this tool from the last FEEDBACK_DAYS days."""
    today = today or datetime.date.today()
    try:
        with open(os.path.join(base, 'Docs', 'TOOLING_FEEDBACK.md'), encoding='utf-8') as fh:
            rows = fh.read().splitlines()
    except OSError:
        return False
    for row in rows:
        cells = [c.strip() for c in row.split('|')]
        if len(cells) < 4 or not re.match(r'\d{4}-\d{2}-\d{2}$', cells[1]):
            continue
        try:
            d = datetime.date.fromisoformat(cells[1])
        except ValueError:
            continue
        if tool.lower() in cells[3].lower() and (today - d).days <= FEEDBACK_DAYS:
            return True
    return False


def team_context(base):
    return ('COLLABORATION (Docs/TEAM.md): Cosmic Shore is built by the core four, ' + team_summary(base) +
            ', plus other collaborators, almost all through Claude sessions, so git rarely says which person drove a '
            'change. Before you change files: run `git fetch origin` then `python3 Tools/Build/who_works_where.py '
            'query <the paths you will change>` (about 10 s). If someone else has work in flight or recently landed '
            'there, tell the person in a line who and what, read it, and BUILD ON IT. Never replace, retire, rename or '
            're-tune another collaborator\'s work without the person confirming (name the owner to check with). Look '
            'for an existing tool, system, skill, lab or doc before building a new one. Check bleeding-edge AND '
            'in-flight branches before claiming a shared number (enum value, GameModes id, learnings id). Write '
            '"Affects: <person>\'s <area>" in the commit when you touch someone else\'s area.')


def amoebius_context(base):
    text = ('AMOEBIUS IS UNDER EVALUATION (Docs/TOOLING_DECISIONS.md section 3): the team (Garrett, Caleb, Shombith, '
            'Yash) has not decided whether to keep it, narrow it to the engine as a headless verification harness, '
            'or stop. Shombith drives the engine; Yash drives the launcher. Build what the person asks for, but do '
            'not grow its scope (new launcher pages or features) on your own initiative.')
    if not recent_feedback(base, 'Amoebius'):
        text += (' At the END of this task, ask the person ONE short question: "Did Amoebius help with this: keep, '
                 'change or remove? Why, in a line?". Append their answer as a row of Docs/TOOLING_FEEDBACK.md and '
                 'commit it with the work. Record only what they said.')
    return text


def marker(session, kind):
    safe = re.sub(r'[^A-Za-z0-9_.-]', '_', session or 'nosession')[:120]
    return os.path.join(tempfile.gettempdir(), f'cs-team-context-{kind}-' + safe)


def once(session, kind):
    flag = marker(session, kind)
    if os.path.exists(flag):
        return False
    try:
        open(flag, 'w').close()
    except OSError:
        pass
    return True


def respond(payload, base=None):
    base = base or root()
    session, prompt = payload.get('session_id'), payload.get('prompt') or ''
    parts = []
    if once(session, 'team'):
        parts.append(team_context(base))
    if AMOEBIUS.search(prompt) and once(session, 'amoebius'):
        parts.append(amoebius_context(base))
    if not parts:
        return None
    return {'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext': '\n\n'.join(parts)}}


def self_test():
    bad = []
    base = root()
    sid = 'selftest-%d' % os.getpid()
    try:
        first = respond({'prompt': 'fix the party lobby', 'session_id': sid}, base)
        second = respond({'prompt': 'and the invite toast', 'session_id': sid}, base)
        amo = respond({'prompt': 'add a page to the Amoebius launcher', 'session_id': sid}, base)
        amo2 = respond({'prompt': 'Amoebius again', 'session_id': sid}, base)
    finally:
        for k in ('team', 'amoebius'):
            try:
                os.remove(marker(sid, k))
            except OSError:
                pass
    ctx = (first or {}).get('hookSpecificOutput', {}).get('additionalContext', '')
    if 'who_works_where.py' not in ctx or 'Garrett' not in ctx:
        bad.append('the first prompt did not get the team context')
    if second is not None:
        bad.append('the team context repeated in one session')
    if not amo or 'UNDER EVALUATION' not in amo['hookSpecificOutput']['additionalContext']:
        bad.append('an Amoebius prompt did not get its status')
    if amo2 is not None:
        bad.append('the Amoebius status repeated in one session')
    if AMOEBIUS.search('flora growth law') or AMOEBIUS.search('rhino plough'):
        bad.append('a non-Amoebius prompt matched')
    with tempfile.TemporaryDirectory() as d:
        os.makedirs(os.path.join(d, 'Docs'))
        with open(os.path.join(d, 'Docs', 'TOOLING_FEEDBACK.md'), 'w', encoding='utf-8') as fh:
            fh.write('| Date | Who | Tool | Task |\n|---|---|---|---|\n| 2026-10-14 | Yash | Amoebius | x |\n')
        if not recent_feedback(d, 'Amoebius', datetime.date(2026, 10, 15)):
            bad.append('a row from yesterday was not seen as recent')
        if recent_feedback(d, 'Amoebius', datetime.date(2026, 10, 30)):
            bad.append('a row from two weeks ago was seen as recent')
        if 'ask the person' in amoebius_context(d) and recent_feedback(d, 'Amoebius'):
            bad.append('asked although a recent row exists')
    print('team_context self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
    return 0 if not bad else 1


if __name__ == '__main__':
    if '--self-test' in sys.argv:
        sys.exit(self_test())
    try:
        out = respond(json.load(sys.stdin))
        if out:
            sys.stdout.write(json.dumps(out))
    except Exception:   # a broken reminder must never block a prompt
        pass
    sys.exit(0)
