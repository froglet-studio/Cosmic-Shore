# CosmicShore.Compat

Package-API shims for compiling the real `Assets/_Scripts` against the port engine
(`src/CosmicShore.Live`). One folder per package, original namespace, behavior-level
implementation only (no vendored source). Unity-owned `UnityEngine.*` types belong in
`CosmicShore.Engine`, not here.
