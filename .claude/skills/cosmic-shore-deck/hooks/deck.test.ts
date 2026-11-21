import type { On } from 'claude-code'
import { expect, mock, test } from 'claude-code/testing'

const PANE = { plugin: 'cosmic-shore-deck', component: 'Pane', requestId: 'cosmic-shore-deck' } as const
const paneProps = {
  title: 'Cosmic Shore',
  isFocused: false,
  bodyColumns: 80,
  placement: 'dock',
  scroll: { offset: 0, bodyRows: 40, contentRows: 0 },
  view: {},
} as const

// A host where git answers a branch two behind its origin and five behind bleeding-edge,
// and the using-directives gate fails while every other gate passes.
function fakeHost(argv: readonly string[]) {
  const ok = (stdout: string, exitCode = 0) => ({
    value: { exitCode, stdout, stderr: '', isStdoutTruncated: false, isStderrTruncated: false },
  })
  const line = argv.join(' ')
  if (line === 'git rev-parse --show-toplevel') return ok('/repo\n')
  if (line === 'git rev-parse --abbrev-ref HEAD') return ok('Ys-bleeding-edge\n')
  if (line.startsWith('git rev-list --left-right')) return ok('1\t2\n')
  if (line === 'git status --porcelain') return ok(' M a.cs\n M b.cs\n')
  if (line === 'git rev-list --count HEAD..origin/bleeding-edge') return ok('5\n')
  if (line.includes('check_using_directives')) return ok('using-directive check: 1 PROBLEM\nFoo.cs: missing using', 1)
  if (line.startsWith('python3 Tools/Build/check_')) return ok('check: OK')
  return ok('')
}

// The engine beneath the deck: the fake host, a mocked clock, and the calls whose answer it ignores.
function engine(on: On) {
  mock.clock(on, { now: 1_000_000 })
  on('process.run', ($, e) => fakeHost(e.argv))
  on('session.start', ($, e) => ({ cwd: e.cwd }))
  on('command.register', ($, e) => ({ value: { command: e.name } }))
  on('ui.status', () => ({ value: undefined }))
  on('ui.toast', () => ({ value: undefined }))
  on('ui.open', () => ({ value: { isPlaced: true } }))
}

test('Gates shows which gate failed and offers Fix with Claude', async ($, on) => {
  engine(on)
  await $.session.start({ cwd: '/repo', surface: 'terminal', isInteractive: true })
  for (const surface of ['terminal', 'desktop', 'mobile'] as const) {
    const ui = await $.ui.mount({ ...PANE, surface, props: paneProps })
    await ui.press({ key: 'gates' })
    expect(await ui.find({ text: /Gates: 6\/7 pass/ })).toBeDefined()
    expect(await ui.find({ text: /FAIL {2}Using directives/ })).toBeDefined()
    expect(await ui.find({ key: 'fix' })).toBeDefined()
    await ui.unmount()
  }
})

test('Sync reports ahead/behind and offers Pull and Merge bleeding-edge', async ($, on) => {
  engine(on)
  await $.session.start({ cwd: '/repo', surface: 'terminal', isInteractive: true })
  const ui = await $.ui.mount({ ...PANE, surface: 'desktop', props: paneProps })
  await ui.press({ key: 'sync' })
  expect(await ui.find({ text: /1 to push, 2 to pull/ })).toBeDefined()
  expect(await ui.find({ key: 'pull' })).toBeDefined()
  expect(await ui.find({ key: 'merge' })).toBeDefined()
  expect(await ui.find({ key: 'fix' })).toBeUndefined()
  await ui.unmount()
})
