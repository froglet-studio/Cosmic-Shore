#!/usr/bin/env python3
"""
The /ship gates: the ONE list of checks a branch passes before its pull request (/ship SKILL.md 1.5).

    python3 Tools/Build/ship_gates.py                    # every gate, one line each, then a verdict
    python3 Tools/Build/ship_gates.py --no-refcompile    # skip the headless C# compile (the Unity editor
                                                         # compiles for itself: Source Control passes this)
    python3 Tools/Build/ship_gates.py --list             # print the gates without running them
    python3 Tools/Build/ship_gates.py --jobs 1           # one gate at a time (default: one per CPU, at most 8)
    python3 Tools/Build/ship_gates.py --changed          # only the gates whose inputs this branch changed (a fast
                                                         # first pass while working; /ship still runs them all)
    bash .claude/skills/ship/gates.sh                    # the same thing; gates.sh only calls this file

Each gate prints `ok    <label>`, `FAIL  <label> :: <the last lines of its output>` or `SKIP  <label>: <why>`;
the last line is `GATES: ALL OK` or `GATES: FAILED`, and the exit code is 0 / 1.

WHY PYTHON: the list used to live in gates.sh, which a Windows machine without bash cannot run. The Unity
Source Control window (FrogletTools > Build > Source Control) runs these gates before a push, on every
collaborator's machine, so the list moved here and gates.sh became a one-line wrapper. There is one list:
add a gate HERE, never to a copy.

A READER: it runs the checks and writes nothing into the repo (the reference compile caches under the temp
directory, as before).
"""
import concurrent.futures
import glob
import os
import subprocess
import sys
import threading

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
GATE_TIMEOUT_S = 600

# (label, script, arguments). Run with this interpreter from the repo root.
STATIC = [
    ('validate_project', 'Tools/CI/validate_project.py', []),
    ('conditional_compilation', 'Tools/Build/check_conditional_compilation.py', ['--root', 'Assets']),
    ('console_logging', 'Tools/Build/check_console_logging.py', []),
    ('doc_summaries', 'Tools/Build/check_doc_summaries.py', []),
    ('doc_summaries_self_test', 'Tools/Build/check_doc_summaries.py', ['--self-test']),
    ('fairplay_paid_content', 'Tools/Build/check_fairplay_paid_content.py', []),
    ('fairplay_paid_self_test', 'Tools/Build/check_fairplay_paid_content.py', ['--self-test']),
    ('fairplay_device_reads', 'Tools/Build/check_fairplay_device_reads.py', []),
    ('fairplay_device_self_test', 'Tools/Build/check_fairplay_device_reads.py', ['--self-test']),
    ('economy_catalog', 'Tools/Build/author_economy_catalog.py', ['--check']),
    ('economy_catalog_self_test', 'Tools/Build/author_economy_catalog.py', ['--self-test']),
    ('colour_set_catalog', 'Tools/Build/author_colour_set_catalog.py', ['--check']),
    ('colour_set_catalog_self_test', 'Tools/Build/author_colour_set_catalog.py', ['--self-test']),
    ('gamelist_scenes', 'Tools/Build/check_gamelist_scenes.py', []),
    ('hud_onscreen', 'Tools/Build/check_hud_onscreen.py', []),
    ('junie_skills', 'Tools/Build/check_junie_skills.py', []),
    ('retire_call_to_action', 'Tools/Build/retire_call_to_action.py', ['--check']),
    ('graphic_canvas_renderer', 'Tools/Build/check_graphic_canvas_renderer.py', []),
    ('ai_no_state_writes', 'Tools/Build/check_ai_no_state_writes.py', ['--check']),
    ('arcade_mode_lib', 'Tools/Build/arcade_mode_lib.py', ['--self-test']),
    ('crew_vessels', 'Tools/Build/check_crew_vessels.py', []),
    ('crew_vessels_self_test', 'Tools/Build/check_crew_vessels.py', ['--self-test']),
    ('ship_gates_self_test', 'Tools/Build/ship_gates.py', ['--self-test']),
    ('push_checks_self_test', 'Tools/Build/push_checks.py', ['--self-test']),
    ('approvals_self_test', 'Tools/Build/approvals.py', ['--self-test']),
    # Swim lane hand-offs (Shombith, 2026-11-13): the tool, its generated /handoff-* skills, and the /ship triage.
    ('handoff_self_test', 'Tools/Build/handoff.py', ['--self-test']),
    ('handoff_skills', 'Tools/Build/handoff.py', ['skills', '--check']),
    ('ship_triage_self_test', 'Tools/Build/ship_triage.py', ['--self-test']),
    ('editor_themes', 'Tools/Build/author_editor_themes.py', ['--check']),
    ('editor_themes_self_test', 'Tools/Build/author_editor_themes.py', ['--self-test']),
    # Every colour comes from the colour set (Garrett, 2026-11-10, LOCKED; Docs/VesselModels/COLOR_TRACE.md): every
    # authored colour is traced, every set's values are kept apart, and every coloured texture is graded.
    ('colour_trace', 'Tools/Build/colour_trace.py', ['--check']),
    ('colour_trace_self_test', 'Tools/Build/colour_trace.py', ['--self-test']),
    ('colour_grade', 'Tools/Build/colour_grade.py', ['--check']),
    ('colour_grade_self_test', 'Tools/Build/colour_grade.py', ['--self-test']),
]
# Every asset generator's drift gate, plus the two generators whose names do not end in _assets.
GENERATOR_GLOB = 'Tools/Build/author_*_assets.py'
GENERATOR_EXTRA = ['Tools/Build/author_skimrace_ai_config.py', 'Tools/Build/author_lifeform_heart_sizes.py',
                   'Tools/Build/pilot_art/author.py']   # the pilot portraits: re-finish every source, compare to disk


def generators(root=ROOT):
    out = sorted(os.path.relpath(p, root).replace(os.sep, '/') for p in glob.glob(os.path.join(root, GENERATOR_GLOB)))
    return out + [g for g in GENERATOR_EXTRA if os.path.isfile(os.path.join(root, g))]


def gate_list(root=ROOT):
    """Every (label, script, args) gate, in the order they run."""
    gates = [g for g in STATIC]
    gates += [(os.path.basename(g) + ' --check', g, ['--check']) for g in generators(root)]
    return gates


def tail(text, lines=3, width=220):
    """gates.sh's `tail -3 | tr '\\n' ' ' | cut -c1-220`: the last lines of a failure, on one line."""
    last = text.rstrip('\n').split('\n')[-lines:] if text.strip() else []
    return ' '.join(last)[:width]


def run_one(script, args, root=ROOT, env=None, timeout=GATE_TIMEOUT_S):
    """(exit code, combined output). A missing script or a timeout is a failure, never a pass."""
    try:
        p = subprocess.run([sys.executable, script] + list(args), cwd=root, env=env, capture_output=True,
                           text=True, encoding='utf-8', errors='replace', timeout=timeout)
        return p.returncode, (p.stdout or '') + (p.stderr or '')
    except subprocess.TimeoutExpired:
        return 124, f'timed out after {timeout}s'
    except OSError as e:
        return 127, str(e)


def emit(line):
    print(line, flush=True)


def dotnet_exe():
    root = os.environ.get('DOTNET_ROOT') or os.path.join(os.path.expanduser('~'), '.dotnet')
    for name in ('dotnet', 'dotnet.exe'):
        p = os.path.join(root, name)
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return root, p
    return root, None


def refcompile(dotnet_root, out=None):
    """The headless player + editor compile (Tools/Build/unity_refcompile), then the gate that needs its output.
    Returns the number of failures."""
    say = out.append if out is not None else emit
    env = dict(os.environ, DOTNET_ROOT=dotnet_root)
    env.setdefault('TMPDIR', os.environ.get('TMPDIR') or os.environ.get('TEMP') or '/tmp')
    here = 'Tools/Build/unity_refcompile'
    fails = 0
    rc, out = run_one(f'{here}/fetch.py', [], env=env)
    if rc != 0:
        say(f'FAIL  refcompile fetch :: {tail(out)}')
        return 1
    rc, out = run_one(f'{here}/build.py', ['--quiet-buckets'], env=env)
    if 'RESULT: OK' in out:
        say('ok    refcompile player')
    else:
        say('FAIL  refcompile player :: ' + ' '.join(l for l in out.splitlines() if 'RESULT' in l or 'error CS' in l)[:400])
        fails += 1
    # Editor config is approximate: only errors in files changed since bleeding-edge gate.
    rc, out = run_one(f'{here}/build.py', ['--config', 'editor', '--quiet-buckets'], env=env)
    if 'RESULT: OK' in out or '(0 in files changed since' in out:
        say('ok    refcompile editor (changed files)')
    else:
        say('FAIL  refcompile editor :: ' + ' '.join(l for l in out.splitlines() if 'RESULT' in l or 'CHANGED-TONIGHT' in l)[:400])
        fails += 1
    # check_generated_assets needs the player compile output, so it runs after it.
    rc, out = run_one('Tools/Build/check_generated_assets.py', [], env=env)
    say('ok    generated_assets' if rc == 0 else f'FAIL  generated_assets :: {tail(out)}')
    return fails + (rc != 0)


def default_jobs():
    return max(1, min(8, os.cpu_count() or 1))


def changed_paths(root=ROOT):
    """Files this branch changes from bleeding-edge, plus uncommitted ones (for --changed)."""
    def git(*a):
        r = subprocess.run(['git'] + list(a), cwd=root, capture_output=True, text=True)
        return r.stdout.split() if r.returncode == 0 else []
    return set(git('diff', '--name-only', 'origin/bleeding-edge...HEAD')) | set(git('diff', '--name-only', 'HEAD'))


def gate_touched(script, changed):
    """A gate is worth re-running when its own script changed, or when the branch touched Assets/, Docs/ or
    Tools/Build/ at all (most gates read those trees). A branch that only changed .claude/ or Port/ runs the self-tests."""
    if script in changed:
        return True
    return any(p.startswith(('Assets/', 'Docs/', 'Tools/Build/', 'Tools/CI/', 'ProjectSettings/', 'Packages/')) for p in changed)


def run_all(with_refcompile=True, jobs=None, only_changed=False):
    """Every gate, `jobs` at a time (they are independent READERS), printed in list order; the reference compile runs
    beside them on its own thread. The output is the same lines, in the same order, as one at a time."""
    jobs = jobs or default_jobs()
    gates = gate_list()
    if only_changed:
        changed = changed_paths()
        gates = [g for g in gates if gate_touched(g[1], changed) or g[0].endswith('self_test')]
    fails = 0
    ref_lines, ref_fails = [], [0]

    def ref_job():
        root, exe = dotnet_exe()
        if not exe:
            ref_lines.append(f'SKIP  refcompile + generated_assets: no .NET SDK at {root} (install: see SKILL.md step 4)')
            return
        ref_fails[0] = refcompile(root, out=ref_lines)

    ref_thread = None
    if with_refcompile:
        ref_thread = threading.Thread(target=ref_job, daemon=True)
        ref_thread.start()
    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as pool:
        futs = [pool.submit(run_one, script, args) for _, script, args in gates]
        for (label, _, _), fut in zip(gates, futs):
            rc, out = fut.result()
            if rc == 0:
                emit(f'ok    {label}')
            else:
                emit(f'FAIL  {label} :: {tail(out)}')
                fails += 1
    if ref_thread:
        ref_thread.join()
        for l in ref_lines:
            emit(l)
        fails += ref_fails[0]
    else:
        emit('SKIP  refcompile + generated_assets: --no-refcompile (the Unity editor compiles the project itself)')
    if only_changed:
        emit(f'NOTE  --changed ran {len(gates)} of {len(gate_list())} gates; /ship runs them all before the PR')
    emit('GATES: ALL OK' if fails == 0 else 'GATES: FAILED')
    return 0 if fails == 0 else 1


def self_test():
    errs = []
    gates = gate_list()
    labels = [g[0] for g in gates]
    if len(labels) != len(set(labels)):
        errs.append('two gates share a label')
    for label, script, _ in gates:
        if not os.path.isfile(os.path.join(ROOT, script)):
            errs.append(f'gate {label}: {script} does not exist')
    if not any(l.startswith('author_thresher_assets.py') for l in labels):
        errs.append('the generator glob found no author_thresher_assets.py')
    if tail('a\nb\nc\nd\n') != 'b c d' or tail('') != '' or len(tail('x' * 500)) != 220:
        errs.append('tail() does not match gates.sh')
    # Negative control: a failing script is reported as a failure, with its last lines.
    rc, out = run_one('-c', ['import sys; print("one"); print("boom"); sys.exit(3)'])
    if rc != 3 or tail(out) != 'one boom':
        errs.append(f'run_one did not report a failing gate (rc={rc}, out={out!r})')
    rc, out = run_one('Tools/Build/does_not_exist.py', [])
    if rc == 0:
        errs.append('a missing gate script passed')
    # Parallel runs keep list order and still report a failure (negative control through the pool).
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        res = list(pool.map(lambda a: run_one('-c', [a]), ['import time; time.sleep(.3)', 'import sys; sys.exit(2)', 'pass']))
    if [r[0] for r in res] != [0, 2, 0]:
        errs.append(f'the parallel pool lost order or a failure: {[r[0] for r in res]}')
    if not gate_touched('Tools/Build/x.py', {'Assets/a.cs'}) or gate_touched('Tools/Build/x.py', {'.claude/skills/a.md'}):
        errs.append('--changed picks the wrong gates')
    # gates.sh must stay a wrapper: a second list is how the two would drift.
    sh = open(os.path.join(ROOT, '.claude/skills/ship/gates.sh'), encoding='utf-8').read()
    if 'ship_gates.py' not in sh or 'author_' in sh:
        errs.append('gates.sh carries its own gate list again; it must only call Tools/Build/ship_gates.py')
    for e in errs:
        print('FAIL:', e)
    print('ship_gates self-test:', 'OK' if not errs else f'{len(errs)} failure(s)')
    return 1 if errs else 0


def main(argv):
    if '--self-test' in argv:
        return self_test()
    if '--list' in argv:
        for label, script, args in gate_list():
            print(f'{label}\t{script} {" ".join(args)}'.rstrip())
        return 0
    jobs = None
    if '--jobs' in argv:
        jobs = int(argv[argv.index('--jobs') + 1])
    return run_all(with_refcompile='--no-refcompile' not in argv, jobs=jobs, only_changed='--changed' in argv)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
