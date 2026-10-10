using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// The engine half of the parity harness (ROADMAP C1, format in Port/parity/README.md).
    ///
    ///   --replay FILE          seed, start scene, frame count, checkpoint interval and the input
    ///                          stream from a replay file, read with the game's own ReplayFile
    ///                          (Assets/_Scripts/Utility/Replay, compiled live). The "do" steps are
    ///                          InputScript verbs the engine plays; the file is then handed to the
    ///                          game (<see cref="BeginSession"/>): DeterministicSession.Begin(seed)
    ///                          always, and ReplayPlayer.Start(file) when it carries "status" frames,
    ///                          so those reach IInputStatus through InputController as in Unity
    ///   --parity-out DIR       write the run's channels: state.jsonl (gameplay state at each
    ///                          checkpoint), events.jsonl (in order: "fmod" event starts and
    ///                          restarts, "fmod-stop" stop() calls, "fmod-snapshot" mixer snapshots, "game"
    ///                          events - scene loads and the GameDataSO match events - and
    ///                          "contact" starts involving a vessel, sorted by name within a frame),
    ///                          transforms.jsonl (vessels during the first 10 s of each scene;
    ///                          "t" there is seconds since the scene was entered) and run.json
    ///                          (how the run was made: "audio" native or silent, the replay's
    ///                          seed and which player drove it)
    ///   --random-golden DIR    write random_SEED.json for every --seeds S1,S2,... and exit
    ///
    /// The Unity side writes the same files from the same replay (ParityCapture), and
    /// <c>engine_parity</c> diffs the two with the C9 tolerances. In a Prisma run THIS is the
    /// writer: the game's own ParityProbe stays inert unless COSMIC_SHORE_PARITY_OUT is set, and
    /// <see cref="EnvironmentClash"/> refuses a run where both would write one directory.
    /// </summary>
    public static class ParityRun
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Draws per kind in a random golden (value, Range(0,1000), onUnitSphere).</summary>
        public const int RandomDraws = 1000;
        public const double TransformWindowSeconds = 10;

        static StreamWriter s_state, s_events, s_transforms;
        static string s_dir;
        static int s_every = 30;
        static string s_scene;
        static float s_sceneStart;
        static readonly List<string> s_contacts = new();
        static readonly HashSet<CosmicShore.Utility.GameDataSO> s_hooked = new();

        /// <summary>The GameDataSO events written as "game" events (the Unity ParityProbe hooks the same list).</summary>
        static readonly string[] GameEvents =
        {
            "OnLaunchGame", "OnSessionStarted", "OnInitializeGame", "OnMiniGameRoundStarted", "OnMiniGameTurnStarted",
            "OnMiniGameTurnEnd", "OnMiniGameRoundEnd", "OnMiniGameEnd", "OnWinnerCalculated", "OnResetForReplay", "OnSessionEnded",
        };

        public static bool Active => s_state != null;

        /// <summary>The replay as the game reads it, or null when the run has none.</summary>
        public static CosmicShore.Utility.ReplayFile Replay { get; private set; }

        /// <summary>Full path of the loaded replay file, or null.</summary>
        public static string ReplayPath { get; private set; }

        /// <summary>
        /// Applies a replay file: its seed, scene, length and "do" input. Returns the record spec
        /// ("FROM-TO:EVERY") or null. Parsed by the game's ReplayFile so both engines read one
        /// format (a version other than 1 is loud here as it is in Unity); the status stream is
        /// kept for <see cref="BeginSession"/>.
        /// </summary>
        public static string LoadReplay(string path, InputScript script, ref string scene, ref int seed, ref int frames)
        {
            var r = CosmicShore.Utility.ReplayFile.Load(path);
            Replay = r;
            ReplayPath = Path.GetFullPath(path);
            if (!string.IsNullOrEmpty(r.scene)) scene = r.scene;
            seed = r.seed;
            if (r.frames > 0) frames = r.frames;
            s_every = Math.Max(1, r.checkpointEvery);
            foreach (var s in r.Do) script.Add(s);
            Console.WriteLine(r.Status.Length > 0
                ? $"[parity] replay {path}: scene {scene}, seed {r.seed}, {r.frames} frames, {r.Do.Length} do step(s), {r.Status.Length} status frame(s) for the game's ReplayPlayer"
                : $"[parity] replay {path}: scene {scene}, seed {r.seed}, {r.frames} frames, {r.Do.Length} do step(s), no status frames (the do stream drives the device strategies)");
            return r.HasRecordSpec ? r.record : null;
        }

        /// <summary>
        /// Hands the loaded replay to the game, exactly what a Unity player build's
        /// COSMIC_SHORE_REPLAY hook does: DeterministicSession.Begin(seed) (Random.InitState, the
        /// seven seeded System.Random sites, captureFramerate) and, when the file carries status
        /// frames, ReplayPlayer.Start(file), after which InputController hands the strategy slot to
        /// the replay. PlayerBoot calls this right before the game's BeforeSceneLoad hooks, the
        /// phase that hook runs in, so the seed lands at the same point in both engines. A run with
        /// no replay has nothing to do.
        /// </summary>
        public static void BeginSession()
        {
            if (Replay == null) return;
            CosmicShore.Utility.DeterministicSession.Begin(Replay.seed);
            if (Replay.Status.Length > 0) CosmicShore.Utility.ReplayPlayer.Start(Replay);
            Console.WriteLine($"[parity] session begun with seed {Replay.seed}; input: {(CosmicShore.Utility.ReplayPlayer.Active ? "the game's ReplayPlayer" : "device strategies (do stream)")}");
        }

        /// <summary>
        /// The game's own hooks must not double up on this run: COSMIC_SHORE_PARITY_OUT would start
        /// the ParityProbe (a second writer of the same channel files) and COSMIC_SHORE_REPLAY a
        /// second replay. Returns the clash to print, or null when the environment is consistent
        /// with the arguments: the probe may write a DIFFERENT directory (that is how the two
        /// writers are compared), and the variable may name the same replay file.
        /// </summary>
        public static string EnvironmentClash(string replayPath, string parityOut)
        {
            string probeOut = Environment.GetEnvironmentVariable(CosmicShore.Utility.DeterministicSession.ParityOutEnvironmentVariable);
            if (parityOut != null && !string.IsNullOrEmpty(probeOut) && SamePath(probeOut, parityOut))
                return $"{CosmicShore.Utility.DeterministicSession.ParityOutEnvironmentVariable} names the --parity-out directory '{parityOut}': the game's ParityProbe and the engine's ParityRun would write the same files; unset one or point them at different directories";
            string envReplay = Environment.GetEnvironmentVariable(CosmicShore.Utility.DeterministicSession.ReplayEnvironmentVariable);
            if (replayPath != null && !string.IsNullOrEmpty(envReplay) && !SamePath(envReplay, replayPath))
                return $"{CosmicShore.Utility.DeterministicSession.ReplayEnvironmentVariable} names '{envReplay}' but --replay is '{replayPath}': the game's hook would start a second replay; unset the variable";
            return null;
        }

        /// <summary>
        /// The returning-user profile a parity run plays as: a fresh copy of Port/parity/profile
        /// (prefs with the consent and age prompts answered, a player named "parity", nothing
        /// earned) under <paramref name="parityOut"/> (or the temp folder), made the persistent
        /// data path for this process. Two things make this necessary: a first-time profile stops
        /// in the Authentication scene at the username prompt and never reaches the menu the
        /// replays press, and the display name is the key of the state channel's stats, so every
        /// run, and the Unity capture, must play as the same name. Without the template the
        /// machine's profile (COSMIC_SHORE_PROFILE) stays in charge, with a line saying so.
        /// </summary>
        public static void PrepareProfile(string parityOut)
        {
            if (Replay == null) return;
            string root = CosmicShore.Content.AssetDatabase.FindProjectRoot();
            string template = root == null ? null : Path.Combine(root, "Port", "parity", "profile");
            if (template == null || !Directory.Exists(template))
            {
                Console.WriteLine("[parity] no Port/parity/profile template; the machine's profile plays (a first-time one stops at the username prompt)");
                return;
            }
            string dir = Path.Combine(parityOut ?? Path.Combine(Path.GetTempPath(), "cosmic-shore-parity"), "profile");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            foreach (var file in Directory.GetFiles(template))
                File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
            Application.persistentDataPathOverride = dir;
            Console.WriteLine($"[parity] profile: fresh copy of {Path.GetRelativePath(root, template)} at {dir}");
        }

        static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.Ordinal); }
            catch (Exception) { return string.Equals(a, b, StringComparison.Ordinal); }
        }

        public static void Begin(string dir)
        {
            Directory.CreateDirectory(dir);
            s_state = new StreamWriter(Path.Combine(dir, "state.jsonl")) { NewLine = "\n" };
            s_events = new StreamWriter(Path.Combine(dir, "events.jsonl")) { NewLine = "\n" };
            s_transforms = new StreamWriter(Path.Combine(dir, "transforms.jsonl")) { NewLine = "\n" };
            CosmicShore.Engine.Audio.Fmod.RuntimeManager.EventRecorded = WriteEvent;
            s_dir = dir;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TriggerPass.ContactStarted = OnContact;
            TriggerPass.CollisionStarted = OnCollision;
            HookLoadedGameData();
        }

        static void WriteEvent(string kind, string name)
            => s_events.WriteLine($"{{\"t\":{F(Time.time)},\"kind\":\"{kind}\",\"name\":{JsonSerializer.Serialize(name)}}}");

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            WriteEvent("game", "scene:" + scene.name);
            HookLoadedGameData();
        }

        /// <summary>
        /// A trigger contact that starts with a vessel on either side. Buffered and written sorted at
        /// the end of the tick: the order of contacts within one step is the physics engine's own
        /// and is not gameplay, so both engines write them sorted by name.
        /// </summary>
        static void OnContact(Collider a, Collider b)
        {
            if (a.GetComponentInParent<CosmicShore.Gameplay.VesselController>() == null &&
                b.GetComponentInParent<CosmicShore.Gameplay.VesselController>() == null) return;
            s_contacts.Add(PairName(a, b));
        }

        /// <summary>A solid contact that starts (OnCollisionEnter, the C3 contact pass): every pair, sorted like contacts.</summary>
        static void OnCollision(Collider a, Collider b) => s_collisions.Add(PairName(a, b));

        static string PairName(Collider a, Collider b)
        {
            string na = a.gameObject.name, nb = b.gameObject.name;
            return string.CompareOrdinal(na, nb) <= 0 ? na + "|" + nb : nb + "|" + na;
        }

        static readonly List<string> s_collisions = new();

        static void FlushContacts()
        {
            Flush(s_contacts, "contact");
            Flush(s_collisions, "collision");
        }

        static void Flush(List<string> pending, string kind)
        {
            if (pending.Count == 0) return;
            pending.Sort(StringComparer.Ordinal);
            foreach (var c in pending) WriteEvent(kind, c);
            pending.Clear();
        }

        /// <summary>
        /// Every loaded GameDataSO, the way the game's ParityProbe hooks it: the asset loads with the
        /// Bootstrap container before any controller exists, so the menu's OnLaunchGame is heard as
        /// well as the match's events (hooking only through a controller missed it, and the two
        /// channels disagreed). Run at Begin, on every scene load, every tick until something is
        /// hooked and then at each checkpoint for an asset loaded later; a controller's injected
        /// reference is that same asset, so the controller lookup only matters for the state channel.
        /// </summary>
        static void HookLoadedGameData()
        {
            foreach (var gd in Resources.FindObjectsOfTypeAll<CosmicShore.Utility.GameDataSO>()) HookGameData(gd);
            HookGameData(FindGameData());
        }

        /// <summary>Hooks the match events of a GameDataSO the first time it is seen.</summary>
        static void HookGameData(CosmicShore.Utility.GameDataSO gd)
        {
            if (gd == null || !s_hooked.Add(gd)) return;
            foreach (var name in GameEvents)
                if (typeof(CosmicShore.Utility.GameDataSO).GetField(name, Any)?.GetValue(gd) is CosmicShore.Engine.Soap.ScriptableEventNoParam e)
                    e.OnRaised += () => { if (Active) WriteEvent("game", name); };
        }

        /// <summary>After each tick: a checkpoint every N frames (state always, transforms in the first 10 s).</summary>
        public static void AfterTick(int frame)
        {
            if (!Active) return;
            FlushContacts();
            if (s_hooked.Count == 0 || frame % s_every == 0) HookLoadedGameData();
            // C9 compares paths for a replay's first 10 s; the clock restarts in every scene
            // entered (boot, menu, match), so a match's opening is compared, not just the boot's.
            var scene = SceneManager.GetActiveScene().name;
            if (scene != s_scene) { s_scene = scene; s_sceneStart = Time.time; }
            if (frame % s_every != 0) return;
            WriteState(frame);
            if (Time.time - s_sceneStart <= TransformWindowSeconds) WriteTransforms(frame);
        }

        public static void End()
        {
            if (!Active) return;
            CosmicShore.Engine.Audio.Fmod.RuntimeManager.EventRecorded = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            TriggerPass.ContactStarted = null;
            TriggerPass.CollisionStarted = null;
            FlushContacts();
            s_hooked.Clear();
            s_state.Dispose(); s_events.Dispose(); s_transforms.Dispose();
            s_state = s_events = s_transforms = null;
            // Written last: the audio runtime comes up after Begin, with the boot.
            var run = new JsonObject { ["engine"] = "prisma", ["audio"] = PlayerAudio.Mode };
            if (Replay != null)
                run["replay"] = new JsonObject
                {
                    ["file"] = Path.GetFileName(ReplayPath),
                    ["seed"] = Replay.seed,
                    ["statusFrames"] = Replay.Status.Length,
                    ["input"] = Replay.Status.Length > 0 ? "ReplayPlayer" : "do",
                };
            File.WriteAllText(Path.Combine(s_dir, "run.json"), run.ToJsonString() + "\n");
        }

        static string F(double v) => v.ToString("R", Inv);

        static void WriteState(int frame)
        {
            var o = new JsonObject
            {
                ["frame"] = frame,
                ["t"] = Math.Round(Time.time, 4),
                ["scene"] = SceneManager.GetActiveScene().name,
            };
            var gd = FindGameData();
            if (gd != null)
            {
                var stats = new JsonObject();
                foreach (var s in gd.RoundStatsList)
                    if (s != null) stats[s.Name ?? "?"] = new JsonObject { ["domain"] = s.Domain.ToString(), ["score"] = s.Score, ["crystals"] = s.CrystalsCollected };
                o["stats"] = stats;
                var sums = new JsonObject();
                foreach (var d in CosmicShore.Utility.GameDataSO.ActiveDomains) sums[d.ToString()] = gd.GetDomainMetricSum(d);
                o["domainSums"] = sums;
            }
            s_state.WriteLine(o.ToJsonString());
        }

        static CosmicShore.Utility.GameDataSO FindGameData()
        {
            var ctrl = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.MiniGameControllerBase>(FindObjectsSortMode.None).FirstOrDefault();
            return ctrl == null ? null : typeof(CosmicShore.Gameplay.MiniGameControllerBase).GetField("gameData", Any)?.GetValue(ctrl) as CosmicShore.Utility.GameDataSO;
        }

        static void WriteTransforms(int frame)
        {
            var seen = new Dictionary<string, int>();
            var vessels = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.VesselController>(FindObjectsSortMode.None)
                .OrderBy(v => v.gameObject.name, StringComparer.Ordinal);
            foreach (var v in vessels)
            {
                var name = v.gameObject.name;
                seen[name] = seen.TryGetValue(name, out var n) ? n + 1 : 0;
                var id = seen[name] == 0 ? name : $"{name}#{seen[name]}";
                var p = v.transform.position;
                var r = v.transform.rotation;
                s_transforms.WriteLine($"{{\"frame\":{frame},\"t\":{F(Math.Round(Time.time - s_sceneStart, 4))},\"scene\":{JsonSerializer.Serialize(s_scene)},\"id\":{JsonSerializer.Serialize(id)},\"p\":[{F(p.x)},{F(p.y)},{F(p.z)}],\"r\":[{F(r.x)},{F(r.y)},{F(r.z)},{F(r.w)}]}}");
            }
        }

        /// <summary>random_SEED.json per seed: RandomDraws of value, then of Range(0,1000), then of onUnitSphere, from one InitState.</summary>
        public static int WriteRandomGoldens(string dir, IEnumerable<int> seeds)
        {
            Directory.CreateDirectory(dir);
            var saved = CosmicShore.Engine.Random.state;
            foreach (var seed in seeds)
            {
                CosmicShore.Engine.Random.InitState(seed);
                var values = new JsonArray(); var ranges = new JsonArray(); var sphere = new JsonArray();
                for (int i = 0; i < RandomDraws; i++) values.Add(CosmicShore.Engine.Random.value);
                for (int i = 0; i < RandomDraws; i++) ranges.Add(CosmicShore.Engine.Random.Range(0, 1000));
                for (int i = 0; i < RandomDraws; i++) { var u = CosmicShore.Engine.Random.onUnitSphere; sphere.Add(new JsonArray(u.x, u.y, u.z)); }
                var o = new JsonObject { ["seed"] = seed, ["value"] = values, ["range"] = ranges, ["onUnitSphere"] = sphere };
                File.WriteAllText(Path.Combine(dir, $"random_{seed}.json"), o.ToJsonString());
            }
            CosmicShore.Engine.Random.state = saved;
            Console.WriteLine($"[parity] random goldens for {seeds.Count()} seed(s) -> {dir}");
            return 0;
        }
    }
}
