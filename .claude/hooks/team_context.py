#!/usr/bin/env python3
"""team_context.py - a UserPromptSubmit hook (.claude/settings.json) that makes every session, and the PERSON in it,
aware of the other collaborators' work and of the open tooling decision (Docs/TEAM.md, Docs/TOOLING_DECISIONS.md;
Yash, 2026-10-15: "make sure to let them know of all these whenever they query of the engine or anything").

  - On a session's FIRST prompt, whatever it is about:
      * the person sees a one-line team notice (systemMessage): who builds Cosmic Shore, that Claude checks who
        else works where before changing anything, and that Amoebius / the Vessel Studio are an open decision;
      * Claude gets the team (from Docs/TEAM.json) and the collaboration rules (run who_works_where.py, build on
        others' work, never override it silently, do not reinvent), and is told to say who else works there in
        its first reply.
  - On the first prompt about the ENGINE or the TOOLS (Amoebius, Port/, the launcher, "engine", "which tool", ...):
      * the person sees the status line: under evaluation, who drives it, who decides and by when;
      * Claude is told to open its answer with the same line, and, if Docs/TOOLING_FEEDBACK.md has no Amoebius row
        from the last three days, to ask at the end whether Amoebius helped. Vessel prompts get the Vessel Studio
        equivalent from vessel_studio_nudge.py.
  - Once the team records a decision in Docs/TOOLING_DECISIONS.md section 6, both notices say so instead.

Once per session each. It never blocks a prompt and always exits 0.

    python3 .claude/hooks/team_context.py --self-test
"""
import datetime
import json
import os
import re
import sys
import tempfile

TOOLS = re.compile(r'\b(?:amoebius|prisma(?:\.exe)?|port/|launcher|cosmicshore\.(?:engine|player|launcher|mcp)|'
                   r'studio server|artifact library|(?:game )?engines?|unified tool|one tool|which tools?|'
                   r'tooling(?: decisions?)?)\b', re.IGNORECASE)
FEEDBACK_DAYS = 3
ROLE_CHARS = 160
DEFAULT_DEADLINE = '2026-11-05'


def root():
    return os.environ.get('CLAUDE_PROJECT_DIR') or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def read(base, *rel):
    try:
        with open(os.path.join(base, *rel), encoding='utf-8') as fh:
            return fh.read()
    except OSError:
        return ''


def short_role(role):
    """The role's first sentence, cut at a clause boundary with balanced brackets (never 'tests) and)')."""
    role = (role or '').split('. ')[0].rstrip('.').strip()
    if len(role) <= ROLE_CHARS:
        return role
    cut = role[:ROLE_CHARS]
    at = max(cut.rfind(', '), cut.rfind('; '))
    if at > ROLE_CHARS // 4:
        cut = cut[:at]
    while cut.count('(') > cut.count(')'):
        cut = cut[:cut.rfind('(')].rstrip(' ,;')
    return cut + ', ...'


def team(base):
    try:
        data = json.loads(read(base, 'Docs', 'TEAM.json'))
    except ValueError:
        return [], []
    core = data.get('core', [])
    roles = {p.get('name'): short_role(p.get('role')) for p in data.get('people', [])}
    return core, [f'{name} ({roles[name]})' if roles.get(name) else name for name in core]


def core_names(base):
    core = team(base)[0] or ['Garrett', 'Caleb', 'Shombith', 'Yash']
    return ', '.join(core[:-1]) + ' and ' + core[-1] if len(core) > 1 else core[0]


def decision(base):
    """(recorded, deadline): whether section 6 of TOOLING_DECISIONS.md holds a dated row, and the feedback deadline."""
    text = read(base, 'Docs', 'TOOLING_DECISIONS.md')
    m = re.search(r'feedback until (\d{4}-\d{2}-\d{2})', text, re.IGNORECASE)
    deadline = m.group(1) if m else DEFAULT_DEADLINE
    section = re.search(r'^## 6\..*?(?=^## |\Z)', text, re.MULTILINE | re.DOTALL)
    recorded = bool(section and re.search(r'^\|\s*\d{4}-\d{2}-\d{2}\s*\|', section.group(0), re.MULTILINE))
    return recorded, deadline


def recent_feedback(base, tool, today=None):
    """True when Docs/TOOLING_FEEDBACK.md has a row for this tool from the last FEEDBACK_DAYS days."""
    today = today or datetime.date.today()
    for row in read(base, 'Docs', 'TOOLING_FEEDBACK.md').splitlines():
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


# --- what the PERSON sees (systemMessage) ---------------------------------------------------------------------

def team_notice(base):
    recorded, deadline = decision(base)
    tools = ('Amoebius and the Vessel Studio: the team recorded a decision (Docs/TOOLING_DECISIONS.md section 6).'
             if recorded else
             'Open decision: keep Amoebius and the Vessel Studio or not, and which one tool builds Froglet\'s games '
             f'(Docs/TOOLING_DECISIONS.md; feedback until {deadline}).')
    return (f'Cosmic Shore team: {core_names(base)}, plus others. Claude checks who else works where you are about '
            'to change, and builds on their work instead of replacing it (Docs/TEAM.md). ' + tools)


def tools_notice(base):
    recorded, deadline = decision(base)
    if recorded:
        return ('Amoebius (the engine in Port/ and the Amoebius.exe launcher): the team recorded a decision about it '
                'in Docs/TOOLING_DECISIONS.md section 6; Claude follows it.')
    return ('Amoebius (the engine in Port/ and the Amoebius.exe launcher) is UNDER EVALUATION. '
            f'{core_names(base)} decide after {deadline} whether to keep it, keep only the engine as a '
            'test harness, or stop (Docs/TOOLING_DECISIONS.md). Shombith drives the engine, Yash the launcher. '
            'Say whether it helps you: Docs/TOOLING_FEEDBACK.md.')


# --- what CLAUDE is told (additionalContext) ------------------------------------------------------------------

def team_context(base):
    people = '; '.join(team(base)[1]) or 'the team in Docs/TEAM.md'
    recorded, _ = decision(base)
    return ('COLLABORATION (Docs/TEAM.md): Cosmic Shore is built by the core four: ' + people +
            '. There are other collaborators too. Almost all work goes through Claude sessions, so git rarely says '
            'which person drove a change. The person has just been shown a one-line team notice. Before you change '
            'files: run `git fetch origin` then `python3 Tools/Build/who_works_where.py query <the paths you will '
            'change>` (about 10 s). In your FIRST reply, tell the person in a line or two who else works in that '
            'area (in flight or recently landed: who, which branch or PR, what it does), or that nobody does. Then '
            'read their work and BUILD ON IT. Never replace, retire, rename or re-tune another collaborator\'s work '
            'without the person confirming (name the owner to check with). Look for an existing tool, system, '
            'skill, lab or doc before building a new one. Check bleeding-edge AND in-flight branches before '
            'claiming a shared number (enum value, GameModes id, learnings id). Write "Affects: <person>\'s '
            '<area>" in the commit when you touch someone else\'s area. ' +
            ('The tooling decision about Amoebius and the Vessel Studio is recorded in Docs/TOOLING_DECISIONS.md '
             'section 6: follow it.' if recorded else
             'Amoebius and the Vessel Studio are UNDER EVALUATION (Docs/TOOLING_DECISIONS.md): if this task touches '
             'either, say so in one line in your first reply.'))


def tools_context(base):
    recorded, deadline = decision(base)
    if recorded:
        return ('This prompt is about the engine or the tools. The team recorded its decision about Amoebius in '
                'Docs/TOOLING_DECISIONS.md section 6: read it, follow it, and tell the person what it says in one '
                'line at the start of your answer.')
    text = ('This prompt is about the engine or the tools. AMOEBIUS IS UNDER EVALUATION (Docs/TOOLING_DECISIONS.md '
            f'section 3): the core four ({core_names(base)}) decide after {deadline} whether to keep it, narrow '
            'it to the engine as a headless verification harness, or stop. Section 5 holds Claude\'s '
            'recommendation, which the team has not accepted yet. Shombith drives the engine; Yash drives the '
            'launcher, studio server and artifact library. START your answer with one line telling the person '
            'this status, who drives the part they asked about, and where the decision lives. Do this even when '
            'they only asked a question. If they ask which tool to use for Cosmic Shore or Froglet\'s games, '
            'answer from Docs/TOOLING_DECISIONS.md and say the decision is open. Build what the person asks for, '
            'but do not grow Amoebius\'s scope (new launcher pages or features) on your own initiative. If they '
            'only mean Unity\'s engine, one line is enough.')
    if not recent_feedback(base, 'Amoebius'):
        text += (' If this task uses or touches Amoebius, at its END ask the person ONE short question: "Did Amoebius '
                 'help with this: keep, change or remove? Why, in a line?". Append their answer as a row of '
                 'Docs/TOOLING_FEEDBACK.md and commit it with the work. Record only what they said.')
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
    notices, context = [], []
    if once(session, 'team'):
        notices.append(team_notice(base))
        context.append(team_context(base))
    if TOOLS.search(prompt) and once(session, 'amoebius'):
        notices.append(tools_notice(base))
        context.append(tools_context(base))
    if not context:
        return None
    return {'systemMessage': '\n'.join(notices),
            'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext': '\n\n'.join(context)}}


def self_test():
    bad = []
    base = root()
    sid = 'selftest-%d' % os.getpid()
    try:
        first = respond({'prompt': 'fix the party lobby', 'session_id': sid}, base)
        second = respond({'prompt': 'and the invite toast', 'session_id': sid}, base)
        eng = respond({'prompt': 'how does the engine boot a scene?', 'session_id': sid}, base)
        eng2 = respond({'prompt': 'Amoebius again', 'session_id': sid}, base)
        both = respond({'prompt': 'which tool should we use?', 'session_id': sid + 'b'}, base)
    finally:
        for s in (sid, sid + 'b'):
            for k in ('team', 'amoebius'):
                try:
                    os.remove(marker(s, k))
                except OSError:
                    pass
    if not first or 'who_works_where.py' not in first['hookSpecificOutput']['additionalContext']:
        bad.append('the first prompt did not give Claude the team context')
    elif 'Garrett' not in first['systemMessage'] or 'Docs/TEAM.md' not in first['systemMessage']:
        bad.append('the first prompt did not show the person the team notice')
    if second is not None:
        bad.append('the team notice repeated in one session')
    if not eng or 'UNDER EVALUATION' not in eng['systemMessage'] or 'START your answer' not in \
            eng['hookSpecificOutput']['additionalContext']:
        bad.append('an engine prompt did not show and pass on the status')
    if eng2 is not None:
        bad.append('the engine status repeated in one session')
    if not both or 'Cosmic Shore team' not in both['systemMessage'] or 'UNDER EVALUATION' not in both['systemMessage']:
        bad.append('a first prompt about the tools did not get both notices')
    for yes in ('open Amoebius.exe', 'the game engine', 'Port/src/CosmicShore.Engine', 'which tool builds it',
                'the launcher crashed', 'tooling decision'):
        if not TOOLS.search(yes):
            bad.append(f'"{yes}" did not match the engine/tools pattern')
    for no in ('flora growth law', 'rhino plough', 'port the fix to bleeding-edge', 'FrogletTools menu'):
        if TOOLS.search(no):
            bad.append(f'"{no}" matched the engine/tools pattern')
    for p in json.loads(read(base, 'Docs', 'TEAM.json') or '{"people": []}').get('people', []):
        r = short_role(p.get('role'))
        if len(r) > ROLE_CHARS + 5 or r.count('(') != r.count(')'):
            bad.append(f'the role of {p.get("name")} is cut badly: {r!r}')
    with tempfile.TemporaryDirectory() as d:
        os.makedirs(os.path.join(d, 'Docs'))

        def write(name, text):
            with open(os.path.join(d, 'Docs', name), 'w', encoding='utf-8') as fh:
                fh.write(text)
        write('TOOLING_FEEDBACK.md', '| Date | Who | Tool | Task |\n|---|---|---|---|\n| 2026-10-14 | Yash | Amoebius | x |\n')
        if not recent_feedback(d, 'Amoebius', datetime.date(2026, 10, 15)):
            bad.append('a row from yesterday was not seen as recent')
        if recent_feedback(d, 'Amoebius', datetime.date(2026, 10, 30)):
            bad.append('a row from two weeks ago was seen as recent')
        if 'ask the person' in tools_context(d) and recent_feedback(d, 'Amoebius'):
            bad.append('asked although a recent row exists')
        open_doc = '## 6. Decision record\n\n| Date | Question |\n|---|---|\n| | |\n\n## 7. How\n\nCollect feedback until 2026-12-01.\n'
        write('TOOLING_DECISIONS.md', open_doc)
        if decision(d) != (False, '2026-12-01') or 'UNDER EVALUATION' not in tools_notice(d):
            bad.append('an empty decision record was not read as open, or its deadline was missed')
        write('TOOLING_DECISIONS.md', open_doc.replace('| | |', '| 2026-11-06 | Amoebius |'))
        if not decision(d)[0] or 'UNDER EVALUATION' in tools_notice(d) or 'recorded' not in team_notice(d):
            bad.append('a recorded decision still read as open')
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
