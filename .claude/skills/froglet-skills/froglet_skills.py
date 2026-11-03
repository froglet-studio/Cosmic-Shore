#!/usr/bin/env python3
"""List every Froglet skill in this repo: its name, when it was created, and what it is for.

A READER tool: it only reads `.claude/skills/*/SKILL.md` and git history, and writes nothing to the repo.

Everything comes from the repo, never typed by hand:
  name / what it is for   the SKILL.md frontmatter (`name:`, `description:`)
  created                 the date of the OLDEST commit that touched that SKILL.md (`--full-history`, not
                          `--diff-filter=A`: an add that arrived through a merge has no non-merge 'A' commit,
                          and /bug-closeout read as never created that way)
  by                      the person behind that commit (Docs/TEAM.json, via who_works_where.Team),
                          else the person its branch belongs to, else "a Claude session"
  landed                  the first "Merge pull request #N" on bleeding-edge that carried it
  last changed            the date of the newest commit that touched the skill's folder

A shallow clone cannot date anything older than its cut-off, so the script says so and
`--deepen` fetches the full commit history WITHOUT file contents (about 0.3 GB, a minute or two).

    python3 .claude/skills/froglet-skills/froglet_skills.py                 # table, oldest first
    python3 .claude/skills/froglet-skills/froglet_skills.py --sort newest   # newest first
    python3 .claude/skills/froglet-skills/froglet_skills.py --full          # one section per skill, full description
    python3 .claude/skills/froglet-skills/froglet_skills.py --skill ship    # one skill (or several, comma separated)
    python3 .claude/skills/froglet-skills/froglet_skills.py --find balance  # skills whose name/description mention a word
    python3 .claude/skills/froglet-skills/froglet_skills.py --json
    python3 .claude/skills/froglet-skills/froglet_skills.py --deepen        # unshallow (commits + trees only), then list
    python3 .claude/skills/froglet-skills/froglet_skills.py --self-test

Exit code 0 on success, 1 when a SKILL.md has no name or description, 2 on a usage/setup error.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SKILLS_REL = '.claude/skills'
BASE = 'origin/bleeding-edge'
REPO = 'froglet-studio/Cosmic-Shore'
PR_RE = re.compile(r'^Merge pull request #(\d+) from [^/\s]+/(\S+)')


def git(root, *args):
    r = subprocess.run(['git', *args], cwd=root, capture_output=True, text=True)
    return r.stdout.strip() if r.returncode == 0 else ''


def repo_root():
    out = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=HERE, capture_output=True, text=True)
    if out.returncode != 0:
        sys.exit('froglet_skills: not inside a git checkout')
    return out.stdout.strip()


# ---------------------------------------------------------------- frontmatter

def parse_frontmatter(text):
    """The `name` and `description` of a SKILL.md. Handles `key: value`, quoted values and `>` / `|` block scalars."""
    lines = text.splitlines()
    if not lines or lines[0].strip() != '---':
        return {}
    fm, i = {}, 1
    while i < len(lines) and lines[i].strip() != '---':
        m = re.match(r'^([A-Za-z_][\w-]*):\s*(.*)$', lines[i])
        if not m:
            i += 1
            continue
        key, val = m.group(1), m.group(2).strip()
        i += 1
        if val in ('>', '|', '>-', '|-', '>+', '|+') or val == '':
            block = []
            while i < len(lines) and lines[i].strip() != '---' and (lines[i].startswith((' ', '\t')) or not lines[i].strip()):
                block.append(lines[i].strip())
                i += 1
            val = ' '.join(b for b in block if b) if not val.startswith('|') else '\n'.join(block).strip()
        elif len(val) >= 2 and val[0] == val[-1] and val[0] in '"\'':
            val = val[1:-1]
        fm[key] = val
    return fm


def summary(desc, limit=180):
    """The first sentence of a description, cut on a word boundary."""
    desc = ' '.join(desc.split())
    first = desc
    for m in re.finditer(r'(?<=[\w)\]`\'"])\.\s', desc):
        if not re.search(r'(?:\be\.g|\bi\.e|\betc|\bvs|\bcf)$', desc[:m.start()], re.I):
            first = desc[:m.start() + 1]
            break
    if len(first) <= limit:
        return first
    cut = first[:limit].rsplit(' ', 1)[0].rstrip(',;:-–— ')
    return cut + '…'


# ---------------------------------------------------------------- git history

def load_team(root):
    sys.path.insert(0, os.path.join(root, 'Tools', 'Build'))
    try:
        from who_works_where import Team  # type: ignore
        return Team.load(root)
    except Exception:  # noqa: BLE001 - attribution is a nicety; the list still prints without it
        return None


def history(root, rel_dir, team, base):
    skill_md = f'{rel_dir}/SKILL.md'
    info = {'created': None, 'created_commit': None, 'by': None, 'last_changed': None,
            'pr': None, 'pr_date': None, 'merged_by': None, 'branch': None}
    first = git(root, 'log', '--full-history', '--format=%H|%ad|%an|%ae|%cn|%ce', '--date=short', 'HEAD', '--', skill_md)
    if not first:
        return info
    sha, date, an, ae, cn, ce = first.splitlines()[-1].split('|')
    info.update(created=date, created_commit=sha[:9])
    last = git(root, 'log', '-1', '--format=%ad', '--date=short', 'HEAD', '--', rel_dir)
    info['last_changed'] = last or None
    who = team.person(an, ae, cn, ce) if team else (an if an.lower() != 'claude' else None)

    if base and git(root, 'rev-parse', '--verify', '-q', base):
        if subprocess.run(['git', 'merge-base', '--is-ancestor', sha, base], cwd=root).returncode == 0:
            merges = git(root, 'log', '--ancestry-path', f'{sha}..{base}', '--merges',
                         '--grep=^Merge pull request #', '--format=%ad|%an|%ae|%s', '--date=short')
            if merges:
                mdate, man, mae, subj = merges.splitlines()[-1].split('|', 3)
                m = PR_RE.match(subj)
                if m:
                    info.update(pr=int(m.group(1)), branch=m.group(2), pr_date=mdate)
                    merger = team.person(man, mae) if team else man
                    info['merged_by'] = merger if isinstance(merger, str) else getattr(merger, 'name', man)
                    if not who and team:
                        who = team.branch_owner(m.group(2))
        else:
            info['pr'] = 'not on bleeding-edge yet'
    info['by'] = who if isinstance(who, str) or who is None else getattr(who, 'name', str(who))
    return info


def is_shallow(root):
    return git(root, 'rev-parse', '--is-shallow-repository') == 'true'


def deepen(root):
    print('Fetching the full commit history (commits and trees only, no file contents)…', file=sys.stderr)
    r = subprocess.run(['git', 'fetch', '--unshallow', '--filter=blob:none', 'origin', 'bleeding-edge'], cwd=root)
    if r.returncode != 0:
        print('froglet_skills: --deepen failed; dates older than the shallow cut-off will be wrong.', file=sys.stderr)


# ---------------------------------------------------------------- collect / render

def collect(root, base=BASE, with_history=True):
    skills_dir = os.path.join(root, SKILLS_REL)
    team = load_team(root) if with_history else None
    skills, not_skills, problems = [], [], []
    for d in sorted(os.listdir(skills_dir), key=str.lower):
        path = os.path.join(skills_dir, d)
        if not os.path.isdir(path):
            continue
        md = os.path.join(path, 'SKILL.md')
        if not os.path.isfile(md):
            not_skills.append(d)
            continue
        with open(md, encoding='utf-8') as fh:
            fm = parse_frontmatter(fh.read())
        name, desc = fm.get('name', '').strip(), fm.get('description', '').strip()
        if not name or not desc:
            problems.append(f'{SKILLS_REL}/{d}/SKILL.md: missing {"name" if not name else "description"}')
        entry = {'name': name or d, 'folder': d, 'description': desc, 'summary': summary(desc) if desc else '',
                 'user_invocable': fm.get('user-invocable', 'true').lower() != 'false'}
        if with_history:
            entry.update(history(root, f'{SKILLS_REL}/{d}', team, base))
        skills.append(entry)
    return skills, not_skills, problems


def sort_skills(skills, how):
    if how == 'name':
        return sorted(skills, key=lambda s: s['name'].lower())
    key = lambda s: (s.get('created') or '9999', s['name'].lower())  # noqa: E731
    return sorted(skills, key=key, reverse=(how == 'newest'))


def pr_text(s, link=True):
    pr = s.get('pr')
    if pr is None:
        return 'not landed'
    if isinstance(pr, str):
        return pr
    ref = f'[#{pr}](https://github.com/{REPO}/pull/{pr})' if link else f'#{pr}'
    extra = ', '.join(x for x in (s.get('pr_date'), s.get('merged_by') and f'merged by {s["merged_by"]}') if x)
    return f'{ref} ({extra})' if extra else ref


def esc(t):
    return t.replace('|', '\\|')


def render_table(skills, not_skills, shallow_cut):
    out = [f'**{len(skills)} Froglet skills** in `{SKILLS_REL}/` (invoke as `/<name>`).', '']
    out += ['| Skill | Created | By | Landed | What it is for |', '|---|---|---|---|---|']
    for s in skills:
        out.append(f'| `/{s["name"]}` | {s.get("created") or "not committed yet"} | {esc(s.get("by") or "a Claude session")} '
                   f'| {pr_text(s)} | {esc(s["summary"])} |')
    out += footer(not_skills, shallow_cut)
    return '\n'.join(out)


def render_full(skills, not_skills, shallow_cut):
    out = [f'# Froglet skills ({len(skills)})', '']
    for s in skills:
        out += [f'## /{s["name"]}', '',
                f'- **Created:** {s.get("created") or "not committed yet"} (commit `{s.get("created_commit") or "?"}`)'
                f' by {s.get("by") or "a Claude session"}',
                f'- **Landed on bleeding-edge:** {pr_text(s)}',
                f'- **Last changed:** {s.get("last_changed") or "?"}',
                f'- **File:** `{SKILLS_REL}/{s["folder"]}/SKILL.md`', '',
                s['description'] or '_(no description)_', '']
    out += footer(not_skills, shallow_cut)
    return '\n'.join(out)


def footer(not_skills, shallow_cut):
    out = []
    if not_skills:
        notes = {'cosmic-shore-deck': 'the session-deck plugin, `Docs/claude/SESSION_DECK.md`'}
        out += ['', 'Not skills (no SKILL.md): ' + ', '.join(
            f'`{n}/`' + (f' ({notes[n]})' if n in notes else '') for n in not_skills) + '.']
    if shallow_cut:
        out += ['', f'**This clone is shallow (history starts {shallow_cut}).** A skill whose Created date is '
                f'{shallow_cut} may be older; rerun with `--deepen`.']
    return out


# ---------------------------------------------------------------- self-test

def self_test():
    fails = []

    def check(label, got, want):
        if got != want:
            fails.append(f'{label}: got {got!r}, want {want!r}')

    fm = parse_frontmatter('---\nname: ship\ndescription: Ship it. Then more.\n---\nbody')
    check('plain', (fm.get('name'), fm.get('description')), ('ship', 'Ship it. Then more.'))
    fm = parse_frontmatter('---\nname: "q"\ndescription: \'quoted: yes\'\n---\n')
    check('quoted', (fm.get('name'), fm.get('description')), ('q', 'quoted: yes'))
    fm = parse_frontmatter('---\nname: b\ndescription: >\n  Folded line one\n  and two.\nother: x\n---\n')
    check('folded', (fm.get('description'), fm.get('other')), ('Folded line one and two.', 'x'))
    check('no frontmatter', parse_frontmatter('# just a heading'), {})
    check('summary first sentence', summary('Does A. Then B.'), 'Does A.')
    check('summary keeps file names', summary('Reads SKILL.md files. Then B.'), 'Reads SKILL.md files.')
    check('summary skips e.g.', summary('Lists things, e.g. skills. Then B.'), 'Lists things, e.g. skills.')
    long = 'word ' * 80
    s = summary(long, limit=40)
    check('summary cut', (len(s) <= 41, s.endswith('…')), (True, True))
    check('pr text none', pr_text({'pr': None}), 'not landed')
    check('pr text', pr_text({'pr': 7, 'pr_date': '2026-01-02', 'merged_by': 'Yash'}, link=False),
          '#7 (2026-01-02, merged by Yash)')

    # Negative control: the real repo must yield skills, and every one of them a name and a description.
    root = repo_root()
    skills, _, problems = collect(root, with_history=False)
    check('real repo has skills', len(skills) > 0, True)
    check('this skill lists itself', any(s['name'] == 'froglet-skills' for s in skills), True)
    check('no frontmatter problems', problems, [])

    if fails:
        print('SELF-TEST FAILED'); print('\n'.join('  ' + f for f in fails))
        return 1
    print(f'self-test ok ({len(skills)} skills parsed)')
    return 0


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--sort', choices=('oldest', 'newest', 'name'), default='oldest')
    ap.add_argument('--full', action='store_true', help='one section per skill with its full description')
    ap.add_argument('--json', action='store_true')
    ap.add_argument('--skill', help='only these skills (comma separated names)')
    ap.add_argument('--find', help='only skills whose name or description contains this text')
    ap.add_argument('--base', default=BASE, help=f'branch the "landed" column measures against (default {BASE})')
    ap.add_argument('--deepen', action='store_true', help='unshallow the clone (commits + trees only) first')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()

    if a.self_test:
        return self_test()
    root = repo_root()
    if a.deepen and is_shallow(root):
        deepen(root)
    shallow_cut = None
    if is_shallow(root):
        shallow_cut = git(root, 'log', '--reverse', '--format=%ad', '--date=short', 'HEAD').split('\n', 1)[0]

    skills, not_skills, problems = collect(root, base=a.base)
    if a.skill:
        want = {w.strip().lstrip('/').lower() for w in a.skill.split(',') if w.strip()}
        skills = [s for s in skills if s['name'].lower() in want or s['folder'].lower() in want]
        missing = want - {s['name'].lower() for s in skills} - {s['folder'].lower() for s in skills}
        if missing:
            print('No such skill: ' + ', '.join(sorted(missing)), file=sys.stderr)
    if a.find:
        q = a.find.lower()
        skills = [s for s in skills if q in s['name'].lower() or q in s['description'].lower()]
    skills = sort_skills(skills, a.sort)

    if a.json:
        print(json.dumps({'skills': skills, 'not_skills': not_skills, 'shallow_since': shallow_cut,
                          'problems': problems}, indent=2))
    elif a.full or a.skill:
        print(render_full(skills, not_skills if not (a.skill or a.find) else [], shallow_cut))
    else:
        print(render_table(skills, not_skills if not a.find else [], shallow_cut))
    for p in problems:
        print('PROBLEM: ' + p, file=sys.stderr)
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
