/** What the deck's pane shows: the last job's result. */
export type DeckPanel = {
  /** The job: Team, Gates, Compile, Sync. */
  title: string
  /** Its output, already trimmed to what the pane draws. */
  body: string
  /** true passed, false failed, null informational. */
  ok: boolean | null
  /** For a failed job: the text a "Fix with Claude" button hands the model. */
  failure: string
  /** When it finished, ms since the epoch. */
  at: number
}

/** The branch facts the status line and the Sync view draw. */
export type SyncInfo = {
  branch: string
  ahead: number
  behind: number
  dirty: number
  /** Commits on origin/bleeding-edge that this branch does not have. */
  bleedingEdgeAhead: number
}

declare module 'claude-code' {
  interface PluginState {
    'cosmic-shore-deck': {
      panel: DeckPanel | null
      /** The running job's name, '' when idle. */
      busy: string
      sync: SyncInfo | null
    }
  }
}
