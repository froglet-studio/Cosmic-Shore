using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
using CosmicShore.Gameplay.Audio;
using FMODUnity;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The game half of the parity harness (<c>Port/parity/README.md</c> § Formats): writes
    /// <c>state.jsonl</c>, <c>events.jsonl</c> and <c>transforms.jsonl</c> in the formats Prisma's
    /// <c>ParityRun</c> writes, so <c>engine_parity</c> can diff the two runs.
    ///
    /// <list type="bullet">
    ///   <item><c>game</c> events: <c>scene:&lt;name&gt;</c> from <see cref="SceneManager.sceneLoaded"/>
    ///   and the eleven <see cref="GameDataSO"/> match events by field name.</item>
    ///   <item><c>contact</c>: a trigger contact with a <see cref="VesselController"/> on either
    ///   side, from a <see cref="ParityContactRelay"/> on each vessel collider; <c>collision</c>:
    ///   an OnCollisionEnter pair from a <see cref="ParityCollisionRelay"/> on each dynamic
    ///   Rigidbody. Both are buffered and written sorted at the end of the frame, because the
    ///   order inside a physics step is the engine's, not gameplay.</item>
    ///   <item><c>fmod</c> / <c>fmod-snapshot start:</c>: every event start or restart, through
    ///   <c>EventDescription.setCallback(STARTED | RESTARTED)</c> on each loaded description.
    ///   <c>fmod-stop</c>: the game's three explicit stop seams (<c>FmodSafe.StopAndRelease</c>,
    ///   <c>FMODOneShotVolumeHelper</c>'s looping-event refusal, and a
    ///   <see cref="ParityEmitterStopRelay"/> beside each StudioEventEmitter whose stop trigger is
    ///   Object Destroy). The STOPPED callback is deliberately not used: it also fires when a
    ///   one-shot ends by itself.</item>
    ///   <item>State every <c>checkpointEvery</c> frames; vessel transforms at those checkpoints
    ///   for the first 10 s of every scene.</item>
    /// </list>
    ///
    /// <para>The probe only reads and writes files. The relays it adds are empty MonoBehaviours
    /// that forward a callback; no gameplay value is written anywhere. It is inert until
    /// <see cref="Begin"/> runs (the <c>COSMIC_SHORE_PARITY_OUT</c> hook in
    /// <see cref="DeterministicSession"/>, or the editor capture tool).</para>
    /// </summary>
    public static class ParityProbe
    {
        public const int TransformWindowSeconds = 10;
        public const int RandomDraws = 1000;

        /// <summary>The GameDataSO events written as "game" events, by field name (Prisma's ParityRun hooks the same list).</summary>
        public static readonly string[] GameEvents =
        {
            "OnLaunchGame", "OnSessionStarted", "OnInitializeGame", "OnMiniGameRoundStarted", "OnMiniGameTurnStarted",
            "OnMiniGameTurnEnd", "OnMiniGameRoundEnd", "OnMiniGameEnd", "OnWinnerCalculated", "OnResetForReplay", "OnSessionEnded",
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly object s_lock = new();
        static readonly List<(string kind, string name)> s_pendingAudio = new();
        static readonly List<(ParityContactRelay relay, Collider other)> s_contacts = new();
        static readonly List<(ParityCollisionRelay relay, Collider other)> s_collisions = new();
        static readonly HashSet<GameDataSO> s_hookedGameData = new();
        static readonly List<(ScriptableEventNoParam evt, Action handler)> s_gameSubscriptions = new();
        static readonly HashSet<GameObject> s_relayed = new();
        static readonly List<ParityEmitterStopRelay> s_emitterRelays = new();
        static readonly HashSet<FMOD.GUID> s_hookedFmod = new();
        static readonly List<FMOD.Studio.EventDescription> s_hookedDescriptions = new();
        static readonly Dictionary<FMOD.GUID, string> s_guidNames = new();
        static readonly FMOD.Studio.EVENT_CALLBACK s_fmodCallback = OnFmodEvent;

        static StreamWriter s_state, s_events, s_transforms;
        static string s_dir;
        static int s_every = 30;
        static int s_frame;
        static string s_scene;
        static float s_sceneStart;
        static ParityProbeDriver s_driver;
        static bool s_exiting;

        /// <summary>True while the channels are open.</summary>
        public static bool Active => s_state != null;

        /// <summary>Frames completed since <see cref="Begin"/> (the "frame" of the next checkpoint line).</summary>
        public static int Frame => s_frame;

        public static int CheckpointEvery => s_every;

        public static string OutputDirectory => s_dir;

        /// <summary>Opens the three channels in <paramref name="dir"/> and hooks every source. Idempotent per directory.</summary>
        public static void Begin(string dir, int checkpointEvery = 30)
        {
            if (Active) End();
            Directory.CreateDirectory(dir);
            s_dir = dir;
            s_every = Math.Max(1, checkpointEvery);
            s_frame = 0;
            s_scene = null;
            s_state = Open(Path.Combine(dir, "state.jsonl"));
            s_events = Open(Path.Combine(dir, "events.jsonl"));
            s_transforms = Open(Path.Combine(dir, "transforms.jsonl"));
            LoadGuidNames();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += End;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            HookGameDataSources();
            HookLoadedFmodEvents();
            EnsureDriver();
            CSDebug.LogVerbose(CSLogChannel.Parity, $"[ParityProbe] writing channels to {dir} every {s_every} frame(s)");
        }

        /// <summary>Flushes what is pending, unhooks every source and closes the channels. Safe to call twice.</summary>
        public static void End()
        {
            if (!Active) return;
            FlushAudio();
            FlushContacts();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.quitting -= End;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            foreach (var (evt, handler) in s_gameSubscriptions)
                if (evt != null) evt.OnRaised -= handler;
            s_gameSubscriptions.Clear();
            s_hookedGameData.Clear();
            int hookedDescriptions = s_hookedFmod.Count;
            UnhookFmodEvents();
            s_relayed.Clear();
            s_emitterRelays.Clear();
            s_state.Dispose(); s_events.Dispose(); s_transforms.Dispose();
            s_state = s_events = s_transforms = null;
            File.WriteAllText(Path.Combine(s_dir, "run.json"), "{\"engine\":\"unity\",\"audio\":\"native\"}\n");
            var driver = s_driver;
            s_driver = null;
            // At process exit the scene may already be gone; the files are closed either way.
            if (driver != null && !s_exiting) UnityEngine.Object.Destroy(driver.gameObject);
            CSDebug.LogVerbose(CSLogChannel.Parity, $"[ParityProbe] closed after {s_frame} frame(s), {hookedDescriptions} FMOD description(s) were hooked");
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            s_exiting = true;
            End();
        }

        // ── Sources ────────────────────────────────────────────────────────────────────

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Active) return;
            WriteEvent("game", "scene:" + scene.name);
            HookGameDataSources();
            HookLoadedFmodEvents();
            EnsureDriver();
        }

        /// <summary>
        /// Every loaded GameDataSO plus the one the scene's controller holds. The asset is loaded
        /// with the Bootstrap container, before any controller exists, so the menu's OnLaunchGame
        /// is heard as well as the match's events.
        /// </summary>
        static void HookGameDataSources()
        {
            foreach (var gd in Resources.FindObjectsOfTypeAll<GameDataSO>()) HookGameData(gd);
            HookGameData(FindGameData());
        }

        static void HookGameData(GameDataSO gd)
        {
            if (gd == null || !s_hookedGameData.Add(gd)) return;
            foreach (var name in GameEvents)
            {
                if (typeof(GameDataSO).GetField(name, Any)?.GetValue(gd) is not ScriptableEventNoParam evt) continue;
                string captured = name;
                Action handler = () => { if (Active) WriteEvent("game", captured); };
                evt.OnRaised += handler;
                s_gameSubscriptions.Add((evt, handler));
            }
        }

        static GameDataSO FindGameData()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<MiniGameControllerBase>(FindObjectsSortMode.None);
            if (controllers.Length == 0) return null;
            return typeof(MiniGameControllerBase).GetField("gameData", Any)?.GetValue(controllers[0]) as GameDataSO;
        }

        /// <summary>A relay per vessel collider, per dynamic Rigidbody and per Object-Destroy emitter; new ones are picked up every frame.</summary>
        static void AttachRelays()
        {
            foreach (var vessel in UnityEngine.Object.FindObjectsByType<VesselController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var collider in vessel.GetComponentsInChildren<Collider>(true))
                    if (s_relayed.Add(collider.gameObject))
                        collider.gameObject.AddComponent<ParityContactRelay>();

            foreach (var body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!body.isKinematic && s_relayed.Add(body.gameObject))
                    body.gameObject.AddComponent<ParityCollisionRelay>();

            foreach (var emitter in UnityEngine.Object.FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (emitter.EventStopTrigger != EmitterGameEvent.ObjectDestroy) continue;
                if (!emitter.TryGetComponent<ParityEmitterStopRelay>(out _))
                {
                    var relay = emitter.gameObject.AddComponent<ParityEmitterStopRelay>();
                    relay.Bind(emitter, EmitterName(emitter));
                    s_emitterRelays.Add(relay);
                }
            }

            // Latch "was playing" a frame ahead of the destroy: by the time the relay's OnDestroy
            // runs the vendor's own OnDestroy may already have stopped and released the instance.
            for (int i = s_emitterRelays.Count - 1; i >= 0; i--)
            {
                var relay = s_emitterRelays[i];
                if (relay == null) { s_emitterRelays.RemoveAt(i); continue; }
                relay.Armed = relay.Emitter != null && relay.Emitter.IsPlaying();
            }
        }

        /// <summary>EventReference carries only a GUID in a player (its Path is editor-only), so the name comes from the runtime, else the GUID map.</summary>
        static string EmitterName(StudioEventEmitter emitter)
        {
            var reference = emitter.EventReference;
            if (FmodSafe.RuntimeAlive)
            {
                var desc = RuntimeManager.GetEventDescription(reference);
                if (desc.isValid()) return DescriptionName(desc);
            }
            return GuidName(reference.Guid);
        }

        internal static void NoteContact(ParityContactRelay relay, Collider other)
        {
            if (!Active || relay == null || other == null) return;
            s_contacts.Add((relay, other));
        }

        internal static void NoteCollision(ParityCollisionRelay relay, Collider other)
        {
            if (!Active || relay == null || other == null) return;
            s_collisions.Add((relay, other));
        }

        /// <summary>An explicit EventInstance.stop(): called by the game's stop seams before the instance is released.</summary>
        public static void NoteFmodStop(FMOD.Studio.EventInstance instance, FMOD.Studio.STOP_MODE mode)
        {
            if (!Active || !instance.isValid()) return;
            if (instance.getDescription(out var desc) != FMOD.RESULT.OK) return;
            NoteFmodStop(DescriptionName(desc), IsSnapshot(desc), mode);
        }

        internal static void NoteFmodStop(string name, bool snapshot, FMOD.Studio.STOP_MODE mode)
        {
            if (!Active) return;
            Enqueue(snapshot ? "fmod-snapshot" : "fmod-stop", snapshot ? "stop:" + name : name + "|" + mode);
        }

        // ── FMOD starts ────────────────────────────────────────────────────────────────

        /// <summary>Sets the start callback on every event description the loaded banks carry (once per GUID).</summary>
        static void HookLoadedFmodEvents()
        {
            if (!FmodSafe.RuntimeAlive) return;
            var system = RuntimeManager.StudioSystem;
            if (!system.isValid() || system.getBankList(out var banks) != FMOD.RESULT.OK || banks == null) return;
            foreach (var bank in banks)
            {
                if (bank.getEventList(out var descriptions) != FMOD.RESULT.OK || descriptions == null) continue;
                foreach (var desc in descriptions)
                {
                    if (desc.getID(out var id) != FMOD.RESULT.OK || !s_hookedFmod.Add(id)) continue;
                    if (desc.setCallback(s_fmodCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.STARTED | FMOD.Studio.EVENT_CALLBACK_TYPE.RESTARTED) == FMOD.RESULT.OK)
                        s_hookedDescriptions.Add(desc);
                }
            }
        }

        static void UnhookFmodEvents()
        {
            if (FmodSafe.RuntimeAlive)
                foreach (var desc in s_hookedDescriptions)
                    if (desc.isValid()) desc.setCallback(null, FMOD.Studio.EVENT_CALLBACK_TYPE.ALL);
            s_hookedDescriptions.Clear();
            s_hookedFmod.Clear();
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
        static FMOD.RESULT OnFmodEvent(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
        {
            // FMOD delivers this from its Studio update thread: resolve the name here (the
            // instance may be gone by frame end) and leave the file to the main thread.
            if (!Active) return FMOD.RESULT.OK;
            var instance = new FMOD.Studio.EventInstance(instancePtr);
            if (instance.getDescription(out var desc) != FMOD.RESULT.OK) return FMOD.RESULT.OK;
            string name = DescriptionName(desc);
            if (IsSnapshot(desc)) Enqueue("fmod-snapshot", "start:" + name);
            else Enqueue("fmod", name);
            return FMOD.RESULT.OK;
        }

        static bool IsSnapshot(FMOD.Studio.EventDescription desc)
            => desc.isSnapshot(out bool snapshot) == FMOD.RESULT.OK && snapshot;

        /// <summary>The event path, or for a GUID-only reference the name Cosmic Shore/Build/GUIDs.txt gives it.</summary>
        static string DescriptionName(FMOD.Studio.EventDescription desc)
        {
            if (desc.getPath(out string path) == FMOD.RESULT.OK && !string.IsNullOrEmpty(path)) return path;
            return desc.getID(out var id) == FMOD.RESULT.OK ? GuidName(id) : "(unresolved)";
        }

        static string GuidName(FMOD.GUID guid)
        {
            lock (s_lock)
                if (s_guidNames.TryGetValue(guid, out var name)) return name;
            return GuidString(guid);
        }

        /// <summary>FMOD.GUID shares System.Guid's byte layout (four ints), so the "B" format is the one GUIDs.txt uses.</summary>
        static string GuidString(FMOD.GUID guid)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(guid.Data1).CopyTo(bytes, 0);
            BitConverter.GetBytes(guid.Data2).CopyTo(bytes, 4);
            BitConverter.GetBytes(guid.Data3).CopyTo(bytes, 8);
            BitConverter.GetBytes(guid.Data4).CopyTo(bytes, 12);
            return new Guid(bytes).ToString("B");
        }

        static void LoadGuidNames()
        {
            lock (s_lock) s_guidNames.Clear();
            string file = Environment.GetEnvironmentVariable("COSMIC_SHORE_FMOD_GUIDS");
            if (string.IsNullOrEmpty(file))
                file = Path.Combine(Application.dataPath, "..", "Cosmic Shore", "Build", "GUIDs.txt");
            if (!File.Exists(file)) return;
            foreach (var line in File.ReadLines(file))
            {
                int space = line.IndexOf(' ');
                if (space <= 0 || !Guid.TryParse(line.Substring(0, space), out var g)) continue;
                var b = g.ToByteArray();
                var guid = new FMOD.GUID
                {
                    Data1 = BitConverter.ToInt32(b, 0), Data2 = BitConverter.ToInt32(b, 4),
                    Data3 = BitConverter.ToInt32(b, 8), Data4 = BitConverter.ToInt32(b, 12),
                };
                lock (s_lock) s_guidNames[guid] = line.Substring(space + 1).Trim();
            }
        }

        static void Enqueue(string kind, string name)
        {
            lock (s_lock) s_pendingAudio.Add((kind, name));
        }

        // ── Frame end ──────────────────────────────────────────────────────────────────

        /// <summary>After the frame's updates: flush the buffered events, re-scan sources, checkpoint.</summary>
        internal static void FrameEnd()
        {
            if (!Active) return;
            FlushAudio();
            FlushContacts();
            if (s_hookedGameData.Count == 0 || s_frame % s_every == 0) HookGameDataSources();
            HookLoadedFmodEvents();
            AttachRelays();

            // The transform clock restarts in every scene entered, so a match's opening is
            // compared as well as the boot's.
            string scene = SceneManager.GetActiveScene().name;
            if (scene != s_scene) { s_scene = scene; s_sceneStart = Time.time; }
            if (s_frame % s_every == 0)
            {
                WriteState(s_frame);
                if (Time.time - s_sceneStart <= TransformWindowSeconds) WriteTransforms(s_frame);
            }
            s_frame++;
        }

        static void FlushAudio()
        {
            List<(string kind, string name)> batch;
            lock (s_lock)
            {
                if (s_pendingAudio.Count == 0) return;
                batch = new List<(string, string)>(s_pendingAudio);
                s_pendingAudio.Clear();
            }
            foreach (var (kind, name) in batch) WriteEvent(kind, name);
        }

        static void FlushContacts()
        {
            if (s_contacts.Count > 0)
            {
                var names = new List<string>();
                var seen = new HashSet<(GameObject, GameObject)>();
                foreach (var (relay, other) in s_contacts)
                {
                    if (relay == null || other == null || IsChildReport(relay, other)) continue;
                    var pair = Pair(relay.gameObject, other.gameObject);
                    if (seen.Add(pair)) names.Add(pair.Item1.name + "|" + pair.Item2.name);
                }
                s_contacts.Clear();
                names.Sort(StringComparer.Ordinal);
                foreach (var n in names) WriteEvent("contact", n);
            }
            if (s_collisions.Count > 0)
            {
                var names = new List<string>();
                var seen = new HashSet<(GameObject, GameObject)>();
                foreach (var (relay, other) in s_collisions)
                {
                    if (relay == null || other == null) continue;
                    var pair = Pair(relay.gameObject, other.gameObject);
                    if (seen.Add(pair)) names.Add(pair.Item1.name + "|" + pair.Item2.name);
                }
                s_collisions.Clear();
                names.Sort(StringComparer.Ordinal);
                foreach (var n in names) WriteEvent("collision", n);
            }
        }

        /// <summary>
        /// Unity also delivers a child collider's OnTriggerEnter to the Rigidbody's GameObject.
        /// The engine names the collider's own object, so a report from a body object is dropped
        /// when a relay below it reported the same other collider this frame.
        /// </summary>
        static bool IsChildReport(ParityContactRelay relay, Collider other)
        {
            if (!relay.TryGetComponent<Rigidbody>(out _)) return false;
            foreach (var (candidate, candidateOther) in s_contacts)
                if (candidate != null && candidate != relay && candidateOther == other && candidate.transform.IsChildOf(relay.transform))
                    return true;
            return false;
        }

        static (GameObject, GameObject) Pair(GameObject a, GameObject b)
        {
            int c = string.CompareOrdinal(a.name, b.name);
            if (c == 0) c = a.GetInstanceID().CompareTo(b.GetInstanceID());
            return c <= 0 ? (a, b) : (b, a);
        }

        static void EnsureDriver()
        {
            if (s_driver != null) return;
            var go = new GameObject("[ParityProbe]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_driver = go.AddComponent<ParityProbeDriver>();
        }

        // ── Writers ────────────────────────────────────────────────────────────────────

        static StreamWriter Open(string path)
            => new(path, false, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };

        static void WriteEvent(string kind, string name)
        {
            if (!Active) return;
            s_events.WriteLine("{\"t\":" + F(Time.time) + ",\"kind\":\"" + kind + "\",\"name\":" + Json(name) + "}");
        }

        static void WriteState(int frame)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"frame\":").Append(frame.ToString(Inv))
              .Append(",\"t\":").Append(Math.Round((double)Time.time, 4).ToString("R", Inv))
              .Append(",\"scene\":").Append(Json(SceneManager.GetActiveScene().name));
            var gd = FindGameData();
            if (gd != null)
            {
                var stats = new List<(string name, string body)>();
                foreach (var s in gd.RoundStatsList)
                {
                    if (s == null) continue;
                    string name = s.Name ?? "?";
                    string body = "{\"domain\":" + Json(s.Domain.ToString()) + ",\"score\":" + Ff(s.Score)
                                + ",\"crystals\":" + s.CrystalsCollected.ToString(Inv) + "}";
                    int at = stats.FindIndex(e => e.name == name);
                    if (at >= 0) stats[at] = (name, body); else stats.Add((name, body));
                }
                sb.Append(",\"stats\":{");
                for (int i = 0; i < stats.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Json(stats[i].name)).Append(':').Append(stats[i].body);
                }
                sb.Append("},\"domainSums\":{");
                var domains = GameDataSO.ActiveDomains;
                for (int i = 0; i < domains.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Json(domains[i].ToString())).Append(':').Append(gd.GetDomainMetricSum(domains[i]).ToString(Inv));
                }
                sb.Append('}');
            }
            sb.Append('}');
            s_state.WriteLine(sb.ToString());
        }

        static void WriteTransforms(int frame)
        {
            var vessels = new List<VesselController>(UnityEngine.Object.FindObjectsByType<VesselController>(FindObjectsSortMode.None));
            vessels.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            var seen = new Dictionary<string, int>();
            string t = F(Math.Round((double)(Time.time - s_sceneStart), 4));
            foreach (var v in vessels)
            {
                string name = v.gameObject.name;
                seen[name] = seen.TryGetValue(name, out int n) ? n + 1 : 0;
                string id = seen[name] == 0 ? name : name + "#" + seen[name].ToString(Inv);
                var p = v.transform.position;
                var r = v.transform.rotation;
                s_transforms.WriteLine("{\"frame\":" + frame.ToString(Inv) + ",\"t\":" + t + ",\"scene\":" + Json(s_scene)
                    + ",\"id\":" + Json(id) + ",\"p\":[" + F(p.x) + "," + F(p.y) + "," + F(p.z) + "],\"r\":["
                    + F(r.x) + "," + F(r.y) + "," + F(r.z) + "," + F(r.w) + "]}");
            }
        }

        /// <summary>
        /// random_SEED.json: RandomDraws of value, then Range(0,1000), then onUnitSphere, all from
        /// one InitState(seed); the Random state in force before the call is restored after it.
        /// </summary>
        public static string WriteRandomGolden(string dir, int seed)
        {
            Directory.CreateDirectory(dir);
            var saved = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\"seed\":").Append(seed.ToString(Inv)).Append(",\"value\":[");
            for (int i = 0; i < RandomDraws; i++) { if (i > 0) sb.Append(','); sb.Append(Ff(UnityEngine.Random.value)); }
            sb.Append("],\"range\":[");
            for (int i = 0; i < RandomDraws; i++) { if (i > 0) sb.Append(','); sb.Append(UnityEngine.Random.Range(0, 1000).ToString(Inv)); }
            sb.Append("],\"onUnitSphere\":[");
            for (int i = 0; i < RandomDraws; i++)
            {
                if (i > 0) sb.Append(',');
                var u = UnityEngine.Random.onUnitSphere;
                sb.Append('[').Append(Ff(u.x)).Append(',').Append(Ff(u.y)).Append(',').Append(Ff(u.z)).Append(']');
            }
            sb.Append("]}");
            UnityEngine.Random.state = saved;
            string path = Path.Combine(dir, "random_" + seed.ToString(Inv) + ".json");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        /// <summary>A float widened to double, as the engine's F(double) prints it.</summary>
        static string F(double v) => v.ToString("R", Inv);

        /// <summary>A float as itself (the engine's JSON nodes keep a float a float).</summary>
        static string Ff(float v) => v.ToString("R", Inv);

        /// <summary>A JSON string literal, ASCII only.</summary>
        public static string Json(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }

    /// <summary>Ends each frame for the probe: last in LateUpdate so every system's writes of the frame precede the checkpoint.</summary>
    [DefaultExecutionOrder(32000)]
    [AddComponentMenu("")]
    public sealed class ParityProbeDriver : MonoBehaviour
    {
        void LateUpdate() => ParityProbe.FrameEnd();
    }

    /// <summary>Forwards a vessel collider's trigger contacts to the probe. Added at runtime; writes nothing itself.</summary>
    [AddComponentMenu("")]
    public sealed class ParityContactRelay : MonoBehaviour
    {
        void OnTriggerEnter(Collider other) => ParityProbe.NoteContact(this, other);
    }

    /// <summary>Forwards a dynamic Rigidbody's OnCollisionEnter pairs to the probe. Added at runtime; writes nothing itself.</summary>
    [AddComponentMenu("")]
    public sealed class ParityCollisionRelay : MonoBehaviour
    {
        void OnCollisionEnter(Collision collision) => ParityProbe.NoteCollision(this, collision.collider);
    }

    /// <summary>
    /// Beside a StudioEventEmitter whose stop trigger is Object Destroy: the vendor's OnDestroy
    /// calls Stop() on a playing instance, and this writes that stop. Armed from the probe a frame
    /// ahead because the handle may be released before this OnDestroy runs.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ParityEmitterStopRelay : MonoBehaviour
    {
        public StudioEventEmitter Emitter { get; private set; }
        public string EventName { get; private set; }
        public bool Armed { get; set; }

        public void Bind(StudioEventEmitter emitter, string eventName)
        {
            Emitter = emitter;
            EventName = eventName;
        }

        void OnDestroy()
        {
            if (!Armed || Emitter == null || !ParityProbe.Active) return;
            bool snapshot = EventName != null && EventName.StartsWith("snapshot:/", StringComparison.Ordinal);
            ParityProbe.NoteFmodStop(EventName, snapshot,
                Emitter.AllowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        }
    }
}
