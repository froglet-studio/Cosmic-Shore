using System.Runtime.CompilerServices;

// CosmicShore.SOAP is its own assembly (Docs/ASSEMBLY_SPLIT.md, phase 2 step 3), so the grant in
// Assets/_Scripts/AssemblyInfo.cs does not reach it. NetworkMonitorData's `internal set` accessors
// are written by System/NetworkMonitor.cs, which stays in Assembly-CSharp; the editor grant keeps the
// edit-mode tests' access the same as before the split.
[assembly: InternalsVisibleTo("Assembly-CSharp")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
