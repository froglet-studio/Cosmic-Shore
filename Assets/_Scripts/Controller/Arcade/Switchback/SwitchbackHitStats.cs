using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SERVER ONLY. Counts every pilot's hull-to-prism contacts in a Switchback race (SWITCHBACK.md "AI: the hit counter"),
    /// split by what was hit: a COURSE block (Blast Run's still doors and fields), a MOVER, a RIVAL's trail, or the
    /// pilot's OWN trail. Listens to <see cref="VesselImpactor.PrismHullContact"/>, so it counts exactly what the game
    /// treats as a hit (the self-trail grace and the shell guard already applied). One prism touched again within
    /// <see cref="RepeatSeconds"/> is the same hit. A reader: it changes nothing in the race.
    /// <para>Read it in the AI Diagnose panel (F3 / <c>aidiag panel</c>, "Hits"), with <c>aidiag hits</c>, and in the
    /// log line <c>[Switchback AI] race summary ...</c> at the race's end.</para>
    /// </summary>
    public sealed class SwitchbackHitStats : IDisposable
    {
        public enum Source { Course, Mover, Rival, Own }

        public sealed class Row
        {
            public string Name;
            public bool AI;
            public int Difficulty;
            public readonly int[] Hits = new int[4];
            public int Avoids, Blasts, Emergencies, Threaded;
            public float FinishTime = -1f;
            public int Total => Hits[0] + Hits[1] + Hits[2] + Hits[3];
        }

        public const float RepeatSeconds = 0.5f;
        /// <summary>Optional per-hit context (the AI fills it): logged on every counted hit as <c>[Switchback AI] hit ...</c>.</summary>
        public static Func<IPlayer, Prism, string> Context;

        /// <summary>The last finished race's rows (the soak runner and <c>aidiag hits</c> read it after the scene goes).</summary>
        public static IReadOnlyList<Row> LastRace => s_last;
        public static string LastSummary { get; private set; } = "";
        /// <summary>The live race's counter, or null.</summary>
        public static SwitchbackHitStats Current { get; private set; }
        /// <summary>Raised once per race at its end with the summary rows.</summary>
        public static event Action<IReadOnlyList<Row>, float> RaceEnded;

        static readonly List<Row> s_last = new();

        readonly Func<Prism, bool> _isMover;
        readonly Dictionary<IPlayer, Row> _rows = new();
        readonly Dictionary<(IPlayer, Prism), float> _lastTouch = new();
        float _start = -1f;
        bool _ended;

        public SwitchbackHitStats(Func<Prism, bool> isMover)
        {
            _isMover = isMover;
            VesselImpactor.PrismHullContact += OnContact;
            Current = this;
        }

        public void Dispose()
        {
            VesselImpactor.PrismHullContact -= OnContact;
            if (Current == this) Current = null;
        }

        public bool Running => _start >= 0f && !_ended;
        public bool Ended => _ended;

        public void BeginRace()
        {
            _rows.Clear();
            _lastTouch.Clear();
            _start = Time.time;
            _ended = false;
        }

        Row RowFor(IPlayer p)
        {
            if (!_rows.TryGetValue(p, out var r)) _rows[p] = r = new Row { Name = p.Name };
            return r;
        }

        void OnContact(IVesselStatus vessel, Prism prism)
        {
            if (!Running || vessel == null || prism == null) return;
            var player = vessel.Player;
            if (player == null) return;
            float now = Time.time;
            var key = (player, prism);
            if (_lastTouch.TryGetValue(key, out float last) && now - last < RepeatSeconds) { _lastTouch[key] = now; return; }
            _lastTouch[key] = now;
            if (_lastTouch.Count > 4096) _lastTouch.Clear();
            var src = Classify(prism, vessel.PlayerName);
            RowFor(player).Hits[(int)src]++;
            if (Context != null)
                CSDebug.Log($"[Switchback AI] hit pilot={player.Name} src={src} t={now - _start:F1} {Context(player, prism)}");
        }

        public Source Classify(Prism prism, string pilotName)
        {
            if (_isMover != null && _isMover(prism)) return Source.Mover;
            if (prism.IsEnvironmentOwned) return Source.Course;
            return prism.PlayerName == pilotName ? Source.Own : Source.Rival;
        }

        /// <summary>Fill the per-pilot fields the race knows (AI, difficulty, sense tallies, gates, finish) - called by the controller.</summary>
        public void Note(IPlayer p, bool ai, int difficulty, int avoids, int blasts, int emergencies, int threaded, float finishTime)
        {
            var r = RowFor(p);
            r.AI = ai; r.Difficulty = difficulty; r.Avoids = avoids; r.Blasts = blasts; r.Emergencies = emergencies; r.Threaded = threaded;
            if (finishTime >= 0f && r.FinishTime < 0f) r.FinishTime = finishTime;
        }

        public float Elapsed => _start < 0f ? 0f : Time.time - _start;

        public IEnumerable<Row> Rows => _rows.Values;

        /// <summary>End the race: freeze the rows into <see cref="LastRace"/>, log the summary, raise <see cref="RaceEnded"/>.</summary>
        public void EndRace(int intensity, int seed)
        {
            if (_ended || _start < 0f) return;
            _ended = true;
            float time = Time.time - _start;
            s_last.Clear();
            s_last.AddRange(_rows.Values);
            LastSummary = Format(s_last, time, $"I{intensity} seed {seed}");
            CSDebug.Log("[Switchback AI] race summary " + LastSummary);
            RaceEnded?.Invoke(s_last, time);
        }

        public static string Format(IEnumerable<Row> rows, float time, string head)
        {
            var sb = new StringBuilder(256);
            sb.Append(head).Append(", ").Append(time.ToString("F1")).Append(" s");
            foreach (var r in rows)
            {
                sb.Append(" | ").Append(r.Name).Append(r.AI ? $" (AI D{r.Difficulty})" : " (human)")
                  .Append(": hits ").Append(r.Total)
                  .Append(" [course ").Append(r.Hits[0]).Append(", mover ").Append(r.Hits[1])
                  .Append(", rival ").Append(r.Hits[2]).Append(", own ").Append(r.Hits[3]).Append(']')
                  .Append(", dodges ").Append(r.Emergencies).Append(", avoids ").Append(r.Avoids)
                  .Append(", blasts ").Append(r.Blasts).Append(", gates ").Append(r.Threaded);
                if (r.FinishTime >= 0f) sb.Append(", finish ").Append(r.FinishTime.ToString("F1")).Append(" s");
            }
            return sb.ToString();
        }

        /// <summary>The panel / <c>aidiag hits</c> text: the live race, else the last one.</summary>
        public static string Readout()
        {
            var c = Current;
            if (c != null && c.Running) return Format(c.Rows, c.Elapsed, "live").Replace(" | ", "\n");
            return string.IsNullOrEmpty(LastSummary) ? "no Switchback race counted yet" : ("last: " + LastSummary).Replace(" | ", "\n");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_last.Clear(); LastSummary = ""; Current = null; RaceEnded = null; Context = null; }
    }
}
