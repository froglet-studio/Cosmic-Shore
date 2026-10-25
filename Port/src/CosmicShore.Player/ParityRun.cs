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
    ///                          stream ("do": InputScript verbs) from a replay file
    ///   --parity-out DIR       write the run's channels: state.jsonl (gameplay state at each
    ///                          checkpoint), events.jsonl (in order: "fmod" event starts and
    ///                          restarts, "fmod-stop" stop() calls, "fmod-snapshot" mixer snapshots, "game"
    ///                          events - scene loads and the GameDataSO match events - and
    ///                          "contact" starts involving a vessel, sorted by name within a frame),
    ///                          transforms.jsonl (vessels during the first 10 s of each scene;
    ///                          "t" there is seconds since the scene was entered) and run.json
    ///                          (how the run was made: "audio" native or silent)
    ///   --random-golden DIR    write random_SEED.json for every --seeds S1,S2,... and exit
    ///
    /// The Unity side writes the same files from the same replay (ParityCapture), and
    /// <c>engine_parity</c> diffs the two with the C9 tolerances.
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

        /// <summary>Applies a replay file: its seed, scene, length and input. Returns the record spec ("FROM-TO:EVERY") or null.</summary>
        public static string LoadReplay(string path, InputScript script, ref string scene, ref int seed, ref int frames)
        {
            var r = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (r["scene"] is JsonValue sc) scene = sc.GetValue<string>();
            if (r["seed"] is JsonValue sd) seed = sd.GetValue<int>();
            if (r["frames"] is JsonValue fr) frames = fr.GetValue<int>();
            if (r["checkpointEvery"] is JsonValue ce) s_every = Math.Max(1, ce.GetValue<int>());
            if (r["do"] is JsonArray steps)
                foreach (var s in steps) script.Add(s!.GetValue<string>());
            if (r["status"] is JsonArray { Count: > 0 })
                Console.WriteLine("[parity] replay carries IInputStatus snapshots: the game's ReplayPlayer (Assets/_Scripts/Utility/Replay) plays them; the engine plays only the 'do' stream");
            return r["record"] is JsonValue rec ? rec.GetValue<string>() : null;
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
        }

        static void WriteEvent(string kind, string name)
            => s_events.WriteLine($"{{\"t\":{F(Time.time)},\"kind\":\"{kind}\",\"name\":{JsonSerializer.Serialize(name)}}}");

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => WriteEvent("game", "scene:" + scene.name);

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

        /// <summary>Hooks the match events of a GameDataSO the first time a controller exposes it.</summary>
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
            // The controller can appear any tick after a scene load; search until hooked, then per checkpoint.
            if (s_hooked.Count == 0 || frame % s_every == 0) HookGameData(FindGameData());
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
            File.WriteAllText(Path.Combine(s_dir, "run.json"), new JsonObject { ["audio"] = PlayerAudio.Mode }.ToJsonString() + "\n");
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
