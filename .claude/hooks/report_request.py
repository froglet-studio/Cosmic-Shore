#!/usr/bin/env python3
"""report_request.py - a UserPromptSubmit hook (.claude/settings.json): when someone asks a session for a REPORT on a
shared area or on another person's work, it tells Claude how to build one (Docs/TEAM.md rule 10; Yash, 2026-10-15:
"if someone asks you about a report of some other person working on some shared field, you should be able to give
those reports to them").

It fires on a prompt that asks for a report, a status, progress or an update ("report", "status of", "progress on",
"catch me up", "what has <person> done", "what is <person> working on"), every time, and adds context naming:
  - the registered areas the prompt mentions (Docs/Reports/areas.json aliases) -> `team_report.py area <key>`;
  - the people it mentions (Docs/TEAM.json)                                     -> `team_report.py person <name>`;
  - otherwise `team_report.py areas` to pick one, or `team_report.py paths <paths>` for an unregistered area.
It never blocks a prompt and always exits 0.

    python3 .claude/hooks/report_request.py --self-test
"""
import json
import os
import re
import sys

ASK = re.compile(r"\b(?:reports?|status|progress|updates?|catch (?:me|him|her|them) up|summar(?:y|ise|ize)|"
                 r"what (?:has|have|did|is|are) \w+ (?:done|made|built|doing|working on)|"
                 r"where (?:does|do|is|are) \w+ stand)\b", re.IGNORECASE)


def root():
    return os.environ.get('CLAUDE_PROJECT_DIR') or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def load(path, default):
    try:
        with open(path, encoding='utf-8') as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return default


def context(prompt, base):
    if not prompt or not ASK.search(prompt):
        return ''
    low = prompt.lower()
    areas = load(os.path.join(base, 'Docs/Reports/areas.json'), {}).get('areas', {})
    hit_areas = [k for k, a in areas.items()
                 if any(re.search(r'\b' + re.escape(w.lower()) + r'\b', low) for w in [k.replace('-', ' '), a['title'], *a.get('aliases', [])])]
    people = load(os.path.join(base, 'Docs/TEAM.json'), {}).get('people', [])
    hit_people = [p['name'] for p in people
                  if any(re.search(r'\b' + re.escape(n.lower()) + r'\b', low) for n in [p['name'], *p.get('names', [])])]
    lines = ['REPORT REQUEST (Docs/TEAM.md rule 10): build the report from the team report system, not from memory. '
             'Run `git fetch origin` first.']
    for k in hit_areas:
        a = areas[k]
        doc = f" and read {a['report']} (the hand-written report)" if a.get('report') else ''
        lines.append(f"- Area '{a['title']}': run `python3 Tools/Build/team_report.py area {k} --write`{doc}.")
    for n in hit_people:
        lines.append(f"- Person {n}: run `python3 Tools/Build/team_report.py person {n}` for their areas, branches and merges.")
    if not hit_areas:
        lines.append('- No registered area named: `python3 Tools/Build/team_report.py areas` lists them; for anything else '
                     '`team_report.py paths <paths>` reports on any paths (find them with who_works_where.py).')
    lines.append('Answer in plain words for the person who asked: what was built and why, where it is (branch, '
                 'bleeding-edge or not), how to try it (artifact links, Unity steps), what is untested, open tasks and '
                 'decisions waiting on them. If the area has no report doc yet, write Docs/Reports/<AREA>.md from '
                 'Docs/Reports/REGATTA_AI.md\'s shape, register it in Docs/Reports/areas.json, and commit both.')
    return '\n'.join(lines)


def self_test():
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        os.makedirs(os.path.join(tmp, 'Docs/Reports'))
        json.dump({'areas': {'regatta-ai': {'title': 'Regatta AI', 'aliases': ['regatta'], 'report': 'Docs/Reports/REGATTA_AI.md'}}},
                  open(os.path.join(tmp, 'Docs/Reports/areas.json'), 'w'))
        json.dump({'people': [{'name': 'Garrett', 'names': ['Garrett Milliron']}, {'name': 'Yash', 'names': []}]},
                  open(os.path.join(tmp, 'Docs/TEAM.json'), 'w'))
        out = context("Garrett here - give me Yash's report on the regatta AI works", tmp)
        assert 'team_report.py area regatta-ai' in out and 'REGATTA_AI.md' in out, out
        assert 'person Garrett' in out and 'person Yash' in out, out
        out = context('what is Garrett working on?', tmp)
        assert 'person Garrett' in out and 'team_report.py areas' in out, out
        assert context('make the Rhino turn faster', tmp) == '', 'a prompt that asks for no report adds nothing'
        assert context('fix the regatta ring', tmp) == '', 'naming an area without asking for a report adds nothing'
    print('report_request self-test: ok')
    return 0


def main():
    if '--self-test' in sys.argv:
        return self_test()
    try:
        payload = json.load(sys.stdin) if not sys.stdin.isatty() else {}
        text = context(str(payload.get('prompt') or ''), root())
    except Exception:  # a hook never breaks a prompt
        text = ''
    if text:
        print(text)
    return 0


if __name__ == '__main__':
    sys.exit(main())
