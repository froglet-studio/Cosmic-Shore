using System.Runtime.CompilerServices;

// Grants the edit-mode test assembly access to `internal` members of gameplay code
// (Assembly-CSharp) - e.g. Prism.SpatialIndexId, which PrismSpatialIndexTests pokes
// directly rather than through PrismSpatialIndex's public query/lifecycle API. `internal`
// is assembly-scoped in C#; Assembly-CSharp-Editor implicitly REFERENCES Assembly-CSharp
// (see CLAUDE.md's Assembly Definitions section) but reference alone does not grant
// internal visibility - only this attribute does. This file itself must stay outside any
// asmdef-covered folder (and outside a folder named "Editor") so it compiles into
// Assembly-CSharp, the assembly whose internals are being exposed.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
