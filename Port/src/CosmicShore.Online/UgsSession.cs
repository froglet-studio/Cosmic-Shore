using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Online
{
    /// <summary>
    /// One Prisma instance's UGS identity. The first sign-in is anonymous; its session token is saved in
    /// the instance's data folder (<c>ugs-session.json</c>, next to its saves), and every later run signs the
    /// SAME player back in with it - so running Prisma does not mint a new anonymous player each time, which
    /// is what the Unity SDK does with its PlayerPrefs cache. A token UGS no longer accepts falls back to a
    /// fresh anonymous sign-in. The id token (the Bearer, valid an hour) is renewed shortly before it expires.
    ///
    /// The cache is per project + environment: switching either starts a new player. Tokens are never
    /// logged; on Linux/macOS the file is made readable by its owner only.
    /// </summary>
    public sealed class UgsSession
    {
        public const string CacheFileName = "ugs-session.json";
        static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

        readonly IUgsApi _api;
        readonly string _cachePath;
        readonly string _projectId, _environment;
        readonly SemaphoreSlim _gate = new(1, 1);
        UgsPlayer _player;

        /// <summary>How the last sign-in happened: "cached" (session token reused) or "anonymous" (a new player).</summary>
        public string LastSignIn { get; private set; } = "";
        public string PlayerId => _player?.PlayerId;

        internal sealed class CacheRecord
        {
            public string ProjectId { get; set; }
            public string Environment { get; set; }
            public string PlayerId { get; set; }
            public string SessionToken { get; set; }
            public DateTime SavedUtc { get; set; }
        }

        /// <param name="dataDir">The instance's data folder; null keeps nothing between runs.</param>
        public UgsSession(IUgsApi api, string projectId, string environment, string dataDir)
        {
            _api = api;
            _projectId = projectId;
            _environment = environment;
            _cachePath = string.IsNullOrEmpty(dataDir) ? null : Path.Combine(dataDir, CacheFileName);
        }

        /// <summary>A player with an id token good for at least a few minutes.</summary>
        public async Task<UgsPlayer> GetPlayerAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_player != null && _player.ExpiresAt - DateTimeOffset.UtcNow > RenewBefore) return _player;
                // Renewing an expiring id token is a session-token sign-in too (the same player).
                string token = _player?.SessionToken ?? Load()?.SessionToken;
                UgsPlayer p = null;
                if (!string.IsNullOrEmpty(token))
                {
                    try { p = await _api.SignInWithSessionTokenAsync(token, ct).ConfigureAwait(false); LastSignIn = "cached"; }
                    catch (UgsException e) when (e.Status is 400 or 401 or 403 or 404)
                    {
                        Console.WriteLine($"[ugs] saved session no longer accepted ({e.Status}); signing in as a new anonymous player");
                    }
                }
                if (p == null) { p = await _api.SignInAnonymouslyAsync(ct).ConfigureAwait(false); LastSignIn = "anonymous"; }
                // A refresh may not hand back a new session token: keep the one that worked.
                if (string.IsNullOrEmpty(p.SessionToken) && !string.IsNullOrEmpty(token)) p = p with { SessionToken = token };
                _player = p;
                Save(p);
                return p;
            }
            finally { _gate.Release(); }
        }

        internal CacheRecord Load()
        {
            if (_cachePath == null || !File.Exists(_cachePath)) return null;
            try
            {
                var rec = JsonSerializer.Deserialize<CacheRecord>(File.ReadAllText(_cachePath));
                if (rec == null || rec.ProjectId != _projectId || rec.Environment != _environment) return null;
                return rec;
            }
            catch (Exception) { return null; } // unreadable: sign in fresh
        }

        void Save(UgsPlayer p)
        {
            if (_cachePath == null || string.IsNullOrEmpty(p.SessionToken)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
                var tmp = _cachePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(new CacheRecord
                {
                    ProjectId = _projectId,
                    Environment = _environment,
                    PlayerId = p.PlayerId,
                    SessionToken = p.SessionToken,
                    SavedUtc = DateTime.UtcNow,
                }));
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(tmp, _cachePath, overwrite: true);
            }
            catch (Exception e) { Console.WriteLine($"[ugs] could not save the session cache: {e.Message}"); }
        }
    }
}
