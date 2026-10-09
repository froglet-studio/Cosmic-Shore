using System.Runtime.CompilerServices;

// The transport's socket layer is internal to the engine; the test suite drives it directly.
[assembly: InternalsVisibleTo("CosmicShore.Tests")]
// Unity Relay + UGS (docs/RELAY.md) plugs a link and a transport factory into the same internals,
// from its own assembly so the engine itself takes no package dependency (BouncyCastle for DTLS).
[assembly: InternalsVisibleTo("CosmicShore.Online")]
