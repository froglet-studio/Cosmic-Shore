/** Options for LoopAtlas.mount(host, options). */
export interface LoopAtlasProps { layer?: 'now' | 'plan' | 'prop'; view?: 'map' | 'ladder' | 'drivers' | 'holes'; vessel?: string; select?: string; chrome?: boolean; card?: boolean }
export interface TimescaleLadderProps {}
export interface LoopRingProps { layer?: 'now' | 'plan' | 'prop'; vessel?: string; select?: string }
export interface DriverChipsProps {}
declare global { interface Window { LoopAtlas: { model: object; mount(host: HTMLElement, options?: LoopAtlasProps): object; buildTree(layer: string, vessel: string): object } } }
