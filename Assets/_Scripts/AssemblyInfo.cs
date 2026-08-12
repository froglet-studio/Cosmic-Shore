using System.Runtime.CompilerServices;

// Grants the editor assembly access to Assembly-CSharp's `internal` members.
//
// WHY THIS FILE EXISTS
//
// Every first-party test lives under a folder named `Editor`, which compiles it
// into Assembly-CSharp-Editor. That is deliberate and load-bearing: that
// assembly is never included in a player build, so NUnit never reaches the
// IL2CPP linker. Before that move the tests sat in Assembly-CSharp and shipped
// inside the game, and the linker died trying to resolve nunit.framework.
//
// The move has one consequence. `internal` means "visible within this
// assembly", so members the tests reached for free while they shared
// Assembly-CSharp became invisible the moment they left it:
//
//   Prism.SpatialIndexId
//   PaintingDefinitionSO.SetRuntimeData
//   ArcadeGameConfigureModal.ShouldLocalPlayerLaunch
//
// The alternative was making each of those `public`, which would widen the
// shipping API surface permanently so that a test could see it. This attribute
// grants exactly the access needed and nothing more, and covers any future
// internal a test needs without another edit.
//
// WHERE THIS FILE MUST LIVE
//
// In Assets, NOT under an `Editor` folder. Assembly-level attributes apply to
// the assembly the file compiles into, and the grant has to be made BY
// Assembly-CSharp. Move this under an Editor folder and it silently becomes a
// no-op: Assembly-CSharp-Editor would be granting access to itself.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
