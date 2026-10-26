#!/usr/bin/env python3
"""team_tasks_nudge.py - a UserPromptSubmit hook (.claude/settings.json) that keeps the team's shared to-do board,
Docs/TEAM_TASKS.md, in front of every session and the person in it (Docs/TEAM.md rule 9; Yash, 2026-10-15: "ask them
during their work to make to-dos and tests for other collaborators"). Tasks go to the OWNER of the area, never to
whoever offered (Shombith, 2026-10-26: "that's not how froglet tasks work").

  - On a session's FIRST prompt:
      * the person sees how many tasks are open;
      * Claude is told to add a task with Tools/Build/team_tasks.py whenever the work leaves something another
        collaborator can do or test, to name the area's owner, to tell the person, and to close a task the work
        finishes.
  - On the first prompt about ACTIVATION / ONBOARDING (FTUE, tutorial, tooltips, first login, the guide, the
    Lesson / Mentor / ghost demo, funnels, retention, new players):
      * the person sees the open activation tasks count;
      * Claude gets those tasks and is asked to make to-dos and tests for the other collaborators as it works.
  - A prompt naming another area on the board ("Analytics", "Performance", ...) gets that area's open tasks, once.
  - TASKS WAITING ON A PERSON (Yash, 2026-10-15: "make sure to tell this to Garrett, this has to [reach him] next time
    he works"): the tasks whose "Who can take it" names a core collaborator FIRST (the owner, not the offer) are shown
    to that person - on the first prompt when git's user.email is theirs (Docs/TEAM.json), and once whenever a prompt
    names them or touches their area (OWNER_TOPICS). Cloud sessions commit as a shared address, so the topic match is
    what reaches them there.

It never blocks a prompt and always exits 0.

    python3 .claude/hooks/team_tasks_nudge.py --self-test
"""
import json
import os
import re
import sys
import tempfile

ACTIVATION = re.compile(
    r'\b(?:activation|on-?boarding|ftue|first[- ]time(?: user| player)?s?|first[- ]login|tutorials?|tool ?tips?|'
    r'new players?|quest ?(?:graph|track|map)|breadcrumbs?|spotlight|guided path|menu ?guide|lessons?|mentor|'
    r'drills?|ghost ?demo|coach ?marks?|retention|funnels?|d1|d7)\b', re.IGNORECASE)
MAX_LISTED = 8

# A prompt touching one of these areas is, most likely, that person's session (or work they must hear about).
OWNER_TOPICS = {
    'Garrett': re.compile(
        r'\b(?:garrett|crystals?|omni|charge arcs?|seam walk|depth prisms?|mass columns?|ecology|ecosystem|flora|'
        r'fauna|spindles?|lattice|arboretum|snow ?(?:shards?|changer)|projectiles?|explosions?|aoe|impactors?|'
        r'squirrel|regatta|bleeding-edge)\b', re.IGNORECASE),
}
OWNERS = ('Name the OWNER of the area (Docs/TEAM.md; who_works_where.py for the files), never whoever offered: '
          'Shombith for anything on UGS or the backend (Cloud Code, Cloud Save, Access Control, Moderation, '
          'authentication, dashboards, deploys), Menu_Main, UI prefabs and the Amoebius engine; Caleb and Shombith '
          'for FTUE / onboarding (no one else is on it); Garrett for '
          'bleeding-edge, vessels and product decisions; Yash for the party / multiplayer layer, diagnostics and the '
          'Vessel Studio; Caleb and Andrew for QA runs.')


def root():
    return os.environ.get('CLAUDE_PROJECT_DIR') or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def open_tasks(base):
    """The board's open tasks, read with Tools/Build/team_tasks.py's own parser. [] when there is no board."""
    path = os.path.join(base, 'Docs', 'TEAM_TASKS.md')
    try:
        with open(path, encoding='utf-8') as fh:
            text = fh.read()
        sys.path.insert(0, os.path.join(base, 'Tools', 'Build'))
        import team_tasks  # noqa: E402
        return [t for t in team_tasks.parse(text)[1] if t.is_open]
    except Exception:
        return []


def owner_of(t):
    """The first name in "Who can take it" - the person the task waits on (the area's owner)."""
    who = t.fields.get('Who can take it', '')
    m = re.match(r'\s*([A-Z][a-z]+)', who)
    return m.group(1) if m else ''


def waiting_on(tasks, name):
    # Performance first: the team asked for those to reach the owner before anything else (Yash, 2026-10-15).
    mine = [t for t in tasks if owner_of(t) == name]
    return sorted(mine, key=lambda t: t.area.lower() != 'performance')


def person_from_git(base):
    """The TEAM.json person whose email git is configured with, or None (cloud sessions use a shared address)."""
    try:
        import subprocess
        email = subprocess.run(['git', 'config', 'user.email'], cwd=base, capture_output=True, text=True,
                               timeout=3).stdout.strip().lower()
        with open(os.path.join(base, 'Docs', 'TEAM.json'), encoding='utf-8') as fh:
            for p in json.load(fh).get('people', []):
                if email and email in [e.lower() for e in p.get('emails', [])]:
                    return p.get('name')
    except Exception:
        pass
    return None


def waiting_context(name, mine):
    return (f'TASKS WAITING ON {name.upper()} (Docs/TEAM_TASKS.md, owner named first). Tell {name} about them in your '
            f'first reply, in plain words and one line each, before or beside the work asked for - teammates left '
            f'them for {name} to pick up "next time he works" (Yash, 2026-10-15). The performance ones carry their '
            f'measurements in Docs/PERFORMANCE_OPTIMIZATION.md (section 2, the 10-15 rows; levers L1, L13) and '
            f'Docs/Reports/PERFORMANCE.md "Waiting on Garrett". Do not do them unasked; offer.\n' + listing(mine))


def listing(tasks):
    lines = [f'- {t.id} [{t.kind}, {t.area}] {t.fields.get("Task", "")} (who: {t.fields.get("Who can take it", "")})'
             for t in tasks[:MAX_LISTED]]
    if len(tasks) > MAX_LISTED:
        lines.append(f'- ... and {len(tasks) - MAX_LISTED} more: python3 Tools/Build/team_tasks.py list')
    return '\n'.join(lines)


BOARD_RULE = (
    'TEAM TASKS (Docs/TEAM_TASKS.md, Docs/TEAM.md rule 9): the team hands work to each other on one board. While you '
    'work, and before you finish, ADD a task with `python3 Tools/Build/team_tasks.py add --area <area> --kind '
    '<test|dev|content|setup|decision|docs> --task "..." --done-when "..." --who "..." --see "..."` for anything '
    'the work leaves that another collaborator can do or test: an editor test you could not run, a playtest, a '
    'dashboard or service to set up, content to write, a decision for the owner. ' + OWNERS +
    ' Tell the person, in a line, which tasks you added and suggest who to hand each to. If your work '
    'finishes a task on the board, close it with `team_tasks.py done <id> --note "..."` (a failed test is not done: '
    'note the failure and add the fix as a dev task). Commit the board with the work; /ship puts `team_tasks.py '
    'pr-section` in the pull request body.')


def respond(payload, base=None):
    base = base or root()
    session, prompt = payload.get('session_id'), payload.get('prompt') or ''
    tasks = open_tasks(base)
    notices, context = [], []

    if once(session, 'board'):
        notices.append(f'Team to-dos: {len(tasks)} open on Docs/TEAM_TASKS.md, each named to the owner of its area. '
                       'Claude adds a to-do there when your work leaves a test or a task for someone else.')
        context.append(BOARD_RULE)

    if ACTIVATION.search(prompt) and once(session, 'activation'):
        act = [t for t in tasks if t.area.lower() == 'activation']
        notices.append(f'Activation / onboarding: {len(act)} open team tasks in this area (Docs/TEAM_TASKS.md, '
                       'Docs/ACTIVATION.md).')
        context.append(
            'This prompt is about ACTIVATION / ONBOARDING. Start from Docs/ACTIVATION.md (every plan, who is on what, '
            'the path in order). The open activation tasks on the board are below: build on them, take one if this '
            'work does it, and do not duplicate one. As you work, MAKE TO-DOS AND TESTS FOR THE OTHER COLLABORATORS: '
            'every editor test, playtest, first-timer observation, UGS / PostHog step or content gap this work '
            'leaves becomes a task on the board (team_tasks.py add --area Activation), named to its owner. ' + OWNERS +
            ' Tell the person which tasks you added.\n' + (listing(act) or '- (no open activation tasks)'))

    me = person_from_git(base)
    for name, topic in OWNER_TOPICS.items():
        mine = waiting_on(tasks, name)
        if not mine:
            continue
        if (me == name or topic.search(prompt)) and once(session, 'waiting-' + name.lower()):
            notices.append(f'{len(mine)} team task(s) wait on {name} (Docs/TEAM_TASKS.md): '
                           + ', '.join(t.id for t in mine[:6]) + ('...' if len(mine) > 6 else '') + '.')
            context.append(waiting_context(name, mine))

    low = prompt.lower()
    for area in sorted({t.area for t in tasks if t.area.lower() != 'activation'}):
        if re.search(r'\b' + re.escape(area.lower()) + r'\b', low) and once(session, 'area-' + area.lower()):
            mine = [t for t in tasks if t.area == area]
            context.append(f'Open team tasks in {area} (Docs/TEAM_TASKS.md); build on them, take one this work '
                           f'does, add what it leaves for others:\n' + listing(mine))

    if not context:
        return None
    out = {'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext': '\n\n'.join(context)}}
    if notices:
        out['systemMessage'] = '\n'.join(notices)
    return out


def marker(session, kind):
    safe = re.sub(r'[^A-Za-z0-9_.-]', '_', session or 'nosession')[:120]
    return os.path.join(tempfile.gettempdir(), f'cs-team-tasks-{kind}-' + safe)


def once(session, kind):
    flag = marker(session, kind)
    if os.path.exists(flag):
        return False
    try:
        open(flag, 'w').close()
    except OSError:
        pass   # cannot remember: say it anyway
    return True


def self_test():
    bad = []
    base = root()
    sid = 'selftest-%d' % os.getpid()
    kinds = ('board', 'activation', 'area-analytics', 'area-performance', 'waiting-garrett')
    try:
        first = respond({'prompt': 'fix the party invite toast', 'session_id': sid}, base)
        again = respond({'prompt': 'and the lobby', 'session_id': sid}, base)
        act = respond({'prompt': 'improve onboarding for new players', 'session_id': sid}, base)
        act2 = respond({'prompt': 'more FTUE work', 'session_id': sid}, base)
        area = respond({'prompt': 'the analytics funnel next', 'session_id': sid}, base)
        garrett = respond({'prompt': 'tune the omni crystal flux arcs', 'session_id': sid}, base)
        garrett2 = respond({'prompt': 'more crystal work', 'session_id': sid}, base)
    finally:
        for k in kinds:
            try:
                os.remove(marker(sid, k))
            except OSError:
                pass
    tasks = open_tasks(base)
    if not tasks:
        bad.append('no open tasks read from the shipped board')
    if not first or 'team_tasks.py add' not in first['hookSpecificOutput']['additionalContext'] or \
            'Shombith for anything on UGS' not in first['hookSpecificOutput']['additionalContext'] or \
            'offered to take' in first.get('systemMessage', ''):
        bad.append('the first prompt did not carry the board rule and the owners, or still routes by offer')
    if again is not None:
        bad.append('the board notice repeated in one session')
    if not act or 'MAKE TO-DOS AND TESTS' not in act['hookSpecificOutput']['additionalContext'] or \
            'Shombith' not in act['hookSpecificOutput']['additionalContext']:
        bad.append('an onboarding prompt did not ask for to-dos and tests for others')
    elif not any(t.id in act['hookSpecificOutput']['additionalContext'] for t in tasks if t.area == 'Activation'):
        bad.append('an onboarding prompt did not list the open activation tasks')
    if act2 is not None and 'MAKE TO-DOS' in act2['hookSpecificOutput']['additionalContext']:
        bad.append('the activation context repeated in one session')
    if any(t.area == 'Analytics' for t in tasks) and (not area or 'Analytics' not in
                                                      area['hookSpecificOutput']['additionalContext']):
        bad.append('a prompt naming a board area did not get that area\'s tasks')
    if waiting_on(tasks, 'Garrett'):
        if not garrett or 'TASKS WAITING ON GARRETT' not in garrett['hookSpecificOutput']['additionalContext'] or \
                'Garrett' not in garrett.get('systemMessage', ''):
            bad.append('a prompt in Garrett\'s area did not show the tasks waiting on him')
        if garrett2 and 'TASKS WAITING ON GARRETT' in garrett2['hookSpecificOutput']['additionalContext']:
            bad.append('the tasks waiting on Garrett repeated in one session')
    if OWNER_TOPICS['Garrett'].search('fix the party invite toast'):
        bad.append('a party prompt matched Garrett\'s topics')
    for yes in ('onboarding', 'FTUE', 'the first-login guide', 'tooltips', 'tool tip', 'new player retention',
                'the Mentor tips', 'ghost demo', 'D1 retention', 'quest graph'):
        if not ACTIVATION.search(yes):
            bad.append(f'"{yes}" did not match the activation pattern')
    for no in ('flora growth law', 'rhino plough', 'style guide for docs', 'fix the party lobby'):
        if ACTIVATION.search(no):
            bad.append(f'"{no}" matched the activation pattern')
    with tempfile.TemporaryDirectory() as d:
        if open_tasks(d) != []:
            bad.append('a missing board was not read as empty')
    print('team_tasks_nudge self-test: ' + ('ok' if not bad else 'FAILED: ' + '; '.join(bad)))
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
