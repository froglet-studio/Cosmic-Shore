using System.Runtime.CompilerServices;

// The transport's socket layer is internal to the engine; the test suite drives it directly.
[assembly: InternalsVisibleTo("CosmicShore.Tests")]
