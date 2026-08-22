# Roslyn analyzers

Third-party analyzer binaries applied at compile time. **Vendored — do not edit.**

| DLL | Version | Source | License |
|---|---|---|---|
| `Microsoft.Unity.Analyzers.dll` | 1.21.0 | [microsoft/Microsoft.Unity.Analyzers](https://github.com/microsoft/Microsoft.Unity.Analyzers) (NuGet `Microsoft.Unity.Analyzers`) | MIT |

## How this is wired

- The `.meta` carries the asset label **`RoslynAnalyzer`**, which is what makes Unity pass the
  DLL to the C# compiler. Without that label it is an inert binary.
- Every platform in the `.meta` is **disabled, Editor included**. An analyzer is a compiler
  input, never a runtime reference, so it must never be included in a player build.
- Severities are set in the repo-root **`.editorconfig`**, not here. Nothing is set to
  `error` — a build must not fail on a lint rule.

## Scope

Unity applies an analyzer to the assemblies in its folder scope. This folder sits at the
`Assets/` level with no `.asmdef` above it, so it covers the predefined assemblies
(`Assembly-CSharp`, `Assembly-CSharp-Editor`) — which is where essentially all the code is.
As layers are extracted into their own assemblies (see `Docs/ASSEMBLY_SPLIT.md`), confirm in
the editor that new assemblies still pick this up; if not, the fix is a copy of the DLL +
`.meta` beside that `.asmdef`.

## Upgrading

Download the NuGet package, take `analyzers/dotnet/cs/*.dll`, and replace the binary **keeping
the existing `.meta`** so the GUID and the label survive. Check the new build's
`Microsoft.CodeAnalysis` reference is not newer than the Roslyn that Unity ships, or the
compiler declines to load it. 1.21.0 references Roslyn 3.7, comfortably below Unity 6's.

## Removing

Delete this folder. The `.editorconfig` entries become no-ops — Roslyn ignores unknown
diagnostic IDs — so nothing else needs touching.
