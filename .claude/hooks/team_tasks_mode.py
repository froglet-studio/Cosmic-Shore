#!/usr/bin/env python3
"""team_tasks_mode.py - a UserPromptSubmit hook (.claude/settings.json) that has Claude ask the person, once per chat
and before the first task, how this chat should use Froglet Tasks (the team's hand-off tickets, Docs/TEAM.md rule 9):

  - Automatic: the session checks the tickets that touch the work, shows them, and offers the ticket moves the work
    calls for (take, a suggested hand-off, done with proof). Every move still waits for the person's yes.
  - Manual: the session leaves Froglet Tasks alone unless the person runs /team-tasks or asks about tickets.

Shombith asked for this on 2026-10-25, after the per-prompt team hooks were switched off for noise: it is one
question per chat, and nothing more is injected after it. It stays quiet where nobody can answer or the answer is
already given: the scheduled page sync, a session started from a ticket (that one is automatic), and a chat that
opens with /team-tasks. It never blocks a prompt and always exits 0.

    python3 .claude/hooks/team_tasks_mode.py --self-test
"""
import json
import os
import re
import sys
import tempfile

PAGE = 'https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f'

# Prompts that already answer the question, or come from a job no person is watching.
QUIET = re.compile(r'^\s*/team-tasks\b|Sync the Froglet Tasks page|This is a scheduled job|'
                   r'Take Froglet Tasks ticket T-\d{8}-[0-9a-f]{4}', re.IGNORECASE)

CONTEXT = (
    'FROGLET TASKS MODE (asked once per chat). Before starting on the prompt, ask the person ONE question with '
    'AskUserQuestion, then do the task: "How should this chat use Froglet Tasks, the team\'s hand-off tickets?" with '
    'two options, in this order: '
    '(1) "Automatic" - "Claude checks the tickets that touch this work, shows them in a few lines, and offers the '
    'moves the work calls for: take one, suggest a hand-off, mark one done with proof. Nothing changes without your '
    'yes." '
    '(2) "Manual" - "Claude leaves Froglet Tasks alone unless you run /team-tasks or ask about tickets." '
    'Do not recommend either. Keep the answer for the whole chat and do not ask again. '
    'If Automatic: load the /team-tasks skill; read `python3 Tools/Build/team_tasks.py list --json` (and `me`) and '
    'show only the open or live tickets whose area, files or branch match this work (none: say so in one line); '
    'when the work leaves something for someone, offer a suggestion (/team-tasks suggest) or, on a yes, add it; when '
    'the work finishes a ticket, offer to close it with its proof. Never file, take, close or re-rank anything '
    'without the person saying yes (Docs/TEAM.md rule 9). '
    'If Manual: do not read or change Froglet Tasks unless the person runs /team-tasks or asks; /ship still puts '
    '`team_tasks.py pr-section` in a pull request. The page is ' + PAGE + '. '
    'If the prompt is not a task (a greeting, a question about something else) still ask, briefly, and then answer.')


def wants_question(prompt):
    return bool(prompt and prompt.strip()) and not QUIET.search(prompt)


def marker(session):
    safe = re.sub(r'[^A-Za-z0-9_.-]', '_', session or 'nosession')[:120]
    return os.path.join(tempfile.gettempdir(), 'cs-team-tasks-mode-' + safe)


def respond(payload):
    flag = marker(payload.get('session_id'))
    if os.path.exists(flag):
        return None
    prompt = payload.get('prompt') or ''
    try:
        open(flag, 'w').close()   # the first prompt decides: asked now, or already answered by the prompt itself
    except OSError:
        pass
    if not wants_question(prompt):
        return None
    return {'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext': CONTEXT}}


def self_test():
    bad = []
    asks = ['fix the party invite lobby', 'hi', '/ship', 'what is open for me?']
    quiet = ['', '   ', '/team-tasks list', '/team-tasks suggest the menu test',
             'Sync the Froglet Tasks page with GitHub Issues for froglet-studio/Cosmic-Shore. This is a scheduled job',
             'Take Froglet Tasks ticket T-20261015-4354 (https://github.com/x/y/issues/1137) and do it.']
    bad += ['asks: ' + p for p in asks if not wants_question(p)]
    bad += ['quiet: ' + p[:40] for p in quiet if wants_question(p)]
    sid = 'selftest-%d' % os.getpid()
    sid2 = sid + '-sync'
    try:
        first = respond({'prompt': 'fix the menu', 'session_id': sid})
        second = respond({'prompt': 'and the lobby', 'session_id': sid})
        synced = respond({'prompt': quiet[4], 'session_id': sid2})
        after_sync = respond({'prompt': 'fix the menu', 'session_id': sid2})
    finally:
        for s in (sid, sid2):
            try:
                os.remove(marker(s))
            except OSError:
                pass
    if not first or 'AskUserQuestion' not in json.dumps(first) or second is not None:
        bad.append('asks once per chat, on the first prompt')
    if synced is not None or after_sync is not None:
        bad.append('a chat that opened quiet (the scheduled sync) was asked later')
    print('team_tasks_mode self-test: ' + ('ok' if not bad else 'FAILED on ' + repr(bad)))
    return 0 if not bad else 1


if __name__ == '__main__':
    if '--self-test' in sys.argv:
        sys.exit(self_test())
    try:
        out = respond(json.load(sys.stdin))
        if out:
            sys.stdout.write(json.dumps(out))
    except Exception:   # a broken hook must never block a prompt
        pass
    sys.exit(0)
