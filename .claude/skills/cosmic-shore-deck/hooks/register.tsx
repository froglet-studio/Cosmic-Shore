// The Cosmic Shore deck: one-press buttons for the checks every session runs by hand
// (team activity, the C# gates, the reference compile, branch sync, ship), drawn in a
// pane on every surface and as a band above the prompt on the terminal and desktop,
// plus a branch status line. Each job runs on the host and shows its result without a
// model turn; "Fix with Claude", "Merge bleeding-edge" and "Ship" hand the model a prompt.
// Docs/claude/SESSION_DECK.md says what each button runs and how to add one.
import { atom, read, update } from 'claude-code'
import type { ProcessRunInit, ProcessRunResult, Register } from 'claude-code'

import type { DeckPanel, SyncInfo } from '../types'

const PANE = 'cosmic-shore-deck'
const TITLE = 'Cosmic Shore'
const panel = atom({ plugin: 'cosmic-shore-deck', key: 'panel' } as const, null)
const busy = atom({ plugin: 'cosmic-shore-deck', key: 'busy' } as const, '')
const sync = atom({ plugin: 'cosmic-shore-deck', key: 'sync' } as const, null)

const STATUS_EVERY_MS = 120_000
const BODY_LINES = 28

// The fast gates CLAUDE.md asks for before a C# commit (none needs Unity). Edit this list
// to add one; each runs from the repository root, all of them at once.
const GATES: ReadonlyArray<readonly [string, readonly string[]]> = [
  ['Console logging', ['python3', 'Tools/Build/check_console_logging.py']],
  ['Using directives', ['python3', 'Tools/Build/check_using_directives.py']],
  ['Conditional compilation', ['python3', 'Tools/Build/check_conditional_compilation.py']],
  ['AI writes no state', ['python3', 'Tools/Build/check_ai_no_state_writes.py']],
  ['Switch label collisions', ['python3', 'Tools/Build/check_switch_label_collisions.py']],
  ['Enum member references', ['python3', 'Tools/Build/check_enum_member_references.py']],
  ['Self-referential locals', ['python3', 'Tools/Build/check_self_referential_locals.py']],
  ['Team tasks tool', ['python3', 'Tools/Build/team_tasks.py', '--self-test']],
]

type Job = 'Team' | 'Gates' | 'Compile' | 'Sync'

// Everything the jobs need from the engine, as closures built once in session.start: the
// engine is spelled at each call site there, and the jobs below stay plain functions.
type Host = {
  run: (argv: readonly string[], init?: ProcessRunInit) => Promise<ProcessRunResult>
  now: () => Promise<number>
  getBusy: () => Promise<string>
  setBusy: (job: string) => Promise<unknown>
  setPanel: (p: DeckPanel) => Promise<unknown>
  getPanel: () => Promise<DeckPanel | null>
  setSync: (s: SyncInfo) => Promise<unknown>
  status: (text: string) => void
  toast: (text: string) => void
  open: () => Promise<unknown>
  ask: (text: string) => void
}

let host: Host | null = null
let root = ''

async function repoRoot(h: Host): Promise<string> {
  if (root) return root
  const r = await h.run(['git', 'rev-parse', '--show-toplevel'])
  root = r.exitCode === 0 ? r.stdout.trim() : ''
  return root
}

async function git(h: Host, args: string[], timeoutMs = 30_000): Promise<string> {
  const r = await h.run(['git', ...args], { cwd: await repoRoot(h), timeoutMs })
  return r.exitCode === 0 ? r.stdout.trim() : ''
}

const tail = (text: string, lines: number) => text.trimEnd().split('\n').slice(-lines).join('\n')
const count = (s: string | undefined) => Number.parseInt(s ?? '', 10) || 0

async function readSync(h: Host): Promise<SyncInfo> {
  const branch = (await git(h, ['rev-parse', '--abbrev-ref', 'HEAD'])) || '?'
  const [lr, dirty, be] = await Promise.all([
    git(h, ['rev-list', '--left-right', '--count', `HEAD...origin/${branch}`]),
    git(h, ['status', '--porcelain']),
    git(h, ['rev-list', '--count', 'HEAD..origin/bleeding-edge']),
  ])
  const [ahead, behind] = lr.split(/\s+/)
  return {
    branch,
    ahead: count(ahead),
    behind: count(behind),
    dirty: dirty ? dirty.split('\n').length : 0,
    bleedingEdgeAhead: count(be),
  }
}

function statusText(s: SyncInfo): string {
  const parts = [`${s.branch} ↑${s.ahead} ↓${s.behind}`]
  if (s.dirty) parts.push(`${s.dirty} changed`)
  if (s.bleedingEdgeAhead) parts.push(`bleeding-edge +${s.bleedingEdgeAhead}`)
  return parts.join(' · ')
}

async function refreshStatus(h: Host): Promise<void> {
  const s = await readSync(h)
  await h.setSync(s)
  h.status(statusText(s))
}

type Result = Omit<DeckPanel, 'at'>

async function runTeam(h: Host): Promise<Result> {
  const cwd = await repoRoot(h)
  await git(h, ['fetch', '-q', '--prune', 'origin'], 60_000)
  const r = await h.run(['python3', '.claude/hooks/team_activity.py', '--peek', '12'], {
    cwd,
    env: { CLAUDE_PROJECT_DIR: cwd },
  })
  const body = (r.stdout || r.stderr).trim()
  return { title: 'Team (last 12 h)', body: tail(body, BODY_LINES + 4), ok: null, failure: '' }
}

async function runGates(h: Host): Promise<Result> {
  const cwd = await repoRoot(h)
  const results = await Promise.all(
    GATES.map(async ([name, argv]) => {
      const startedAt = await h.now()
      try {
        const r = await h.run(argv, { cwd, timeoutMs: 180_000 })
        const out = `${r.stdout}\n${r.stderr}`.trim()
        return { name, ok: r.exitCode === 0, seconds: ((await h.now()) - startedAt) / 1000, out }
      } catch (err) {
        return { name, ok: false, seconds: 0, out: String(err) }
      }
    }),
  )
  const failed = results.filter(r => !r.ok)
  const body = results
    .map(r => `${r.ok ? 'PASS' : 'FAIL'}  ${r.name} (${r.seconds.toFixed(1)} s)\n      ${tail(r.out, 1)}`)
    .join('\n')
  const failure = failed.map(r => `## ${r.name}\n${tail(r.out, 30)}`).join('\n\n')
  return { title: `Gates: ${results.length - failed.length}/${results.length} pass`, body, ok: failed.length === 0, failure }
}

async function runCompile(h: Host): Promise<Result> {
  const r = await h.run(['bash', 'Tools/Build/unity_refcompile/run.sh', '--config', 'player-dev'], {
    cwd: await repoRoot(h),
    timeoutMs: 600_000,
  })
  const out = `${r.stdout}\n${r.stderr}`.trim()
  return {
    title: `Reference compile (player-dev): ${r.exitCode === 0 ? 'clean' : `exit ${r.exitCode}`}`,
    body: tail(out, BODY_LINES),
    ok: r.exitCode === 0,
    failure: r.exitCode === 0 ? '' : tail(out, 80),
  }
}

async function runSync(h: Host): Promise<Result> {
  await git(h, ['fetch', '-q', '--prune', 'origin'], 60_000)
  const s = await readSync(h)
  await h.setSync(s)
  h.status(statusText(s))
  const lines = [
    `Branch ${s.branch}: ${s.ahead} to push, ${s.behind} to pull from origin/${s.branch}.`,
    `${s.dirty} changed file(s) not committed.`,
    `origin/bleeding-edge has ${s.bleedingEdgeAhead} commit(s) this branch does not.`,
  ]
  return { title: 'Sync', body: lines.join('\n'), ok: null, failure: '' }
}

const JOBS: Record<Job, (h: Host) => Promise<Result>> = {
  Team: runTeam,
  Gates: runGates,
  Compile: runCompile,
  Sync: runSync,
}

/** Runs one job at a time and puts its result in the pane; null when another job is running. */
async function runJob(job: Job): Promise<DeckPanel | null> {
  const h = host
  if (!h) return null
  const running = await h.getBusy()
  if (running) {
    h.toast(`${running} is still running.`)
    return null
  }
  await h.setBusy(job)
  let result: Result
  try {
    result = await JOBS[job](h)
  } catch (err) {
    result = { title: `${job} could not run`, body: String(err), ok: false, failure: '' }
  } finally {
    await h.setBusy('')
  }
  await h.setPanel({ ...result, at: await h.now() })
  return h.getPanel()
}

async function openAndRun(job: Job): Promise<void> {
  await host?.open()
  const result = await runJob(job)
  if (result) host?.toast(result.title)
}

const ask = (text: string) => host?.ask(text)

const fixPrompt = (p: DeckPanel) =>
  `The Cosmic Shore deck's "${p.title}" failed. Find the root cause in this branch's changes and fix it, ` +
  `then re-run the same check. Output:\n\n${p.failure}`

const mergePrompt = (s: SyncInfo) =>
  `Merge origin/bleeding-edge into ${s.branch} with a merge commit (never rebase), ` +
  `resolve any conflicts by building on both sides, run the gates, and push.`

const pullPrompt = (s: SyncInfo) =>
  `Pull origin/${s.branch} into the local branch (git pull --no-rebase), resolve any conflicts, and tell me what came in.`

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    await $.command.register({ name: 'deck', description: 'Open the Cosmic Shore deck (team, gates, compile, sync, ship)' })
    await $.command.register({ name: 'gates', description: 'Run the fast C# gates now and show which fail' })
    await $.command.register({ name: 'team', description: 'Show what every branch pushed in the last 12 hours' })
    await $.command.register({ name: 'sync', description: 'Fetch, then show ahead/behind against origin and bleeding-edge' })
    const h: Host = {
      run: (argv, init) => $.process.run(argv, init),
      now: () => $.clock.now(),
      getBusy: () => read($, busy),
      setBusy: job => update($, busy, () => job),
      setPanel: p => update($, panel, () => p),
      getPanel: () => read($, panel),
      setSync: s => update($, sync, () => s),
      status: text => $.ui.status(text),
      toast: text => $.ui.toast(text),
      open: () => $.ui.open({ id: PANE, title: TITLE }),
      ask: text => void $.prompt.submit({ text, asUser: true }),
    }
    host = h
    void refreshStatus(h)
    $.clock.every(STATUS_EVERY_MS, () => void refreshStatus(h))
    return next(e)
  })

  on('turn.complete', async ($, e, next) => {
    if (host) void refreshStatus(host)
    return next(e)
  })

  on('command.run', { command: 'deck' }, async $ => {
    await $.ui.open({ id: PANE, title: TITLE })
    return { text: 'Cosmic Shore deck opened.' }
  })

  // /gates, /team and /sync answer with the result as text, so the model reads it too.
  const answer = (result: DeckPanel | null, job: Job) => ({
    text: result ? `${result.title}\n${result.body}` : `${job} is already running.`,
  })
  on('command.run', { command: 'gates' }, async () => answer(await runJob('Gates'), 'Gates'))
  on('command.run', { command: 'team' }, async () => answer(await runJob('Team'), 'Team'))
  on('command.run', { command: 'sync' }, async () => answer(await runJob('Sync'), 'Sync'))

  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    if (e.props.hasSurvey) return next(e)
    const { Box, Button, Text } = $.ui.resolve(e)
    const running = await read($, busy)
    return (
      <Box flexWrap="wrap" columnGap={1}>
        <Text dimColor>{running ? `Deck: ${running}…` : 'Deck'}</Text>
        <Button key="open" plain label="Open" onPress={() => $.ui.open({ id: PANE, title: TITLE })} />
        <Button key="team" plain label="Team" onPress={() => openAndRun('Team')} />
        <Button key="gates" plain label="Gates" onPress={() => openAndRun('Gates')} />
        <Button key="sync" plain label="Sync" onPress={() => openAndRun('Sync')} />
      </Box>
    )
  })

  on('ui.render', { component: 'Pane', requestId: PANE }, async ($, e) => {
    const { Box, Button, Text, Code } = $.ui.resolve(e)
    const [last, running, branch] = await Promise.all([read($, panel), read($, busy), read($, sync)])
    const now = await $.clock.now()
    const age = last ? Math.max(0, Math.round((now - last.at) / 60_000)) : 0
    const verdict = last?.ok === true ? 'PASS' : last?.ok === false ? 'FAIL' : ''
    const color = last?.ok === true ? 'success' : last?.ok === false ? 'error' : undefined
    return (
      <Box flexDirection="column" rowGap={1}>
        <Box flexWrap="wrap" columnGap={1} rowGap={1}>
          <Button key="team" hotkey="t" label="Team" onPress={() => runJob('Team')} />
          <Button key="gates" hotkey="g" label="Gates" variant="primary" onPress={() => runJob('Gates')} />
          <Button key="compile" hotkey="c" label="Compile" onPress={() => runJob('Compile')} />
          <Button key="sync" hotkey="s" label="Sync" onPress={() => runJob('Sync')} />
          <Button key="ship" hotkey="p" label="Ship" onPress={() => ask('/ship-quick')} />
        </Box>
        {branch && <Text dimColor wrap="wrap">{statusText(branch)}</Text>}
        {running && <Text color="warning">{running} running… (Compile can take several minutes)</Text>}
        {!running && !last && (
          <Text dimColor wrap="wrap">
            Team: pushes on every branch. Gates: the fast C# checks. Compile: the reference compile. Sync: fetch and
            compare. Ship: /ship-quick.
          </Text>
        )}
        {last && (
          <Box flexDirection="column">
            <Text bold color={color} wrap="wrap">
              {verdict ? `${verdict}  ` : ''}
              {last.title}
              <Text dimColor>{`  ${age ? `${age} min ago` : 'just now'}`}</Text>
            </Text>
            <Code source={last.body || '(no output)'} />
          </Box>
        )}
        <Box flexWrap="wrap" columnGap={1} rowGap={1}>
          {last?.failure && <Button key="fix" label="Fix with Claude" onPress={() => ask(fixPrompt(last))} />}
          {last?.title === 'Sync' && branch && branch.behind > 0 && (
            <Button key="pull" label="Pull" onPress={() => ask(pullPrompt(branch))} />
          )}
          {last?.title === 'Sync' && branch && branch.bleedingEdgeAhead > 0 && (
            <Button key="merge" label="Merge bleeding-edge" onPress={() => ask(mergePrompt(branch))} />
          )}
        </Box>
      </Box>
    )
  })
}
