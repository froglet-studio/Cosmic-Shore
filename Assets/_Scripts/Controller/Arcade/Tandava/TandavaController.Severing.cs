using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SEVERING (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.11): a boss you cannot kill the same way twice. Cut clean
    /// through the body - kill every member across a band of it - and, the moment the cut lands, the part beyond it is
    /// a separate cloud: it crawls off as THE SEVERED, a second swarm with its own director, eating where it likes, and
    /// after a while it turns for home and grafts back on. Cut the body away to nothing while the Severed lives and the
    /// Severed IS the creature now - it regrows into the form the director remembers.
    ///
    /// Nothing dies in a sever and nothing is born: the piece's members MOVE (<see cref="SwarmFauna.ReleaseMembers"/> /
    /// <see cref="ISwarmSeedGraft"/>; <see cref="SwarmFauna.Graft"/> on the way home). The rules - when a body may part,
    /// how big a piece is, the stomach split by members, the rejoin's room and refund, the succession - are proven
    /// headless in Tools/Build/swarm_core_harness/TandavaSeverHarness.cs (T20-T26), which models this glue line for line.
    ///
    /// Every peer runs its own swarms (fauna are client-local). The SERVER finds the piece exactly, on its own body, and
    /// runs the piece's director; a client parts the same number of members nearest the server's piece centre from ITS
    /// body, and its Severed takes the replicated plan, levers and goal and is nudged toward the server's - the main
    /// body's pattern. Rejoin and succession are the server's call, sent to every peer.
    /// </summary>
    public partial class TandavaController : ISwarmSeedGraft
    {
        readonly NetworkVariable<bool> _pieceLive = new(false);
        readonly NetworkVariable<int> _piecePlan = new(-1);
        readonly NetworkVariable<int> _piecePhase = new((int)TandavaPhase.Roam);
        readonly NetworkVariable<int> _pieceMood = new((int)TandavaMood.Calm);
        readonly NetworkVariable<Vector3> _pieceGoal = new();
        readonly NetworkVariable<Vector3> _pieceAnchor = new();
        /// <summary>Seconds until the Severed turns for home; negative once it has.</summary>
        readonly NetworkVariable<float> _pieceHome = new(0f);
        /// <summary>How many members the Severed has: what a peer that joins while it lives parts from its own body.</summary>
        readonly NetworkVariable<int> _pieceMembers = new(0);

        TandavaDirectorSettings _ds;                 // server: the settings both directors run on (BuildCore's clone)
        TandavaDirectorCore _pieceCore;              // server: the Severed's director
        SwarmFauna _severed;                         // this peer's Severed, if one lives
        readonly TandavaSever _sever = new();
        readonly List<SVector3> _sevPos = new();
        readonly List<bool> _sevAlive = new();
        readonly List<int> _sevPiece = new();
        readonly List<SwarmGraft> _grafts = new();
        readonly float[] _seedStomach = new float[4];
        readonly float[] _bank = new float[4];
        Vector3 _seedAt, _seedHeading;
        bool _seedPending;
        int _lostSeen;
        float _pieceLastTick = -1f;
        float _pieceSeenAt = -1f;                    // client: when this peer first saw a Severed it does not have

        /// <summary>The Severed, as this peer has it (the HUD's arrow could point at it).</summary>
        public Transform SeveredTransform => _severed ? _severed.transform : null;
        public bool SeveredLive => _pieceLive.Value;

        bool IsSevered(SwarmFauna swarm) => swarm && swarm == _severed;

        int SeveredPlan => settings && settings.SeveredForm.Variants is { Count: > 0 } ? settings.SeveredForm.Variants[0].PlanIndex : -1;

        void OnPieceLiveChanged(bool previous, bool current) => GoalStack.RefreshAll();

        // ───────────────────────────────────────────────────────────── the server: the cut

        /// <summary>Server, each main tick after the director's: a member died since the last look - is the body in two?
        /// (Before the wound buds shut: the gap a cut leaves closes as the body re-sorts.)</summary>
        void CheckSever(SwarmFauna swarm, in TandavaSwarmState state)
        {
            if (swarm.MembersLost <= _lostSeen) return;
            _lostSeen = swarm.MembersLost;
            if (_ds == null || !_core.MaySever(state) || SeveredPlan < 0) return;
            var spawn = settings.SwarmSpawn;
            if (!spawn || !spawn.FaunaPrefab) return;
            int cap = swarm.CopyLiveMembers(_sevPos, _sevAlive);
            int n = _sever.FindPiece(_sevPos, _sevAlive, cap, _ds.SeverLink, _core.SeverMinMembers, _core.SeverMaxMembers, _sevPiece);
            if (n == 0) return;

            SVector3 pc = SVector3.Zero, rest = SVector3.Zero;
            int restCount = 0;
            for (int q = 0; q < _sevPiece.Count; q++) pc += _sevPos[_sevPiece[q]];
            pc /= n;
            for (int i = 0; i < cap; i++) if (_sevAlive[i]) { rest += _sevPos[i]; restCount++; }
            rest -= pc * n; restCount -= n;   // the body is everything else
            Vector3 at = V(pc);
            Vector3 body = restCount > 0 ? V(rest / restCount) : swarm.AnchorWorld;
            Vector3 away = at - body;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -swarm.BodyForward;

            if (!Sever(swarm, _sevPiece, at, away)) return;
            var spec = settings.SeveredForm;
            var v = spec.Variants[0];
            var form = new TandavaForm
            {
                Name = v.DisplayName, Role = TandavaFormRole.Severed, PlanIndex = v.PlanIndex, FeedPlanIndex = v.FeedPlanIndex,
                PlanCount = swarm.FormMemberCount(v.PlanIndex), FillToEvolve = spec.FillToEvolve, Bank = _ds.RejoinBank,
                MealVolume = spec.MealVolume, Mouth = S(v.Mouth), FeedMouth = S(v.FeedMouth),
            };
            _core.OnSevered(n);
            _pieceCore = new TandavaDirectorCore(new List<TandavaForm> { form }, _ds, System.Environment.TickCount ^ 0x5EED);
            _pieceCore.RememberFrom(_core);   // §3.12: the piece knows what the body knows
            _pieceCore.Home = S(swarm.AnchorWorld);
            _pieceLastTick = -1f;
            PublishPiece(at, n);
            Sever_ClientRpc(at, n, away);
        }

        /// <summary>Client, each main tick: a Severed lives on the server but not here - this peer joined after the cut
        /// (the sever's RPC went out before it was listening). Once its body has caught up with the server's, it parts
        /// the same number of members nearest the server's piece. A grace second first, so a peer that is only waiting
        /// for the sever's RPC (state can land before it) is never parted twice.</summary>
        void CatchUpSevered(SwarmFauna swarm)
        {
            if (IsServer || _severed || !_pieceLive.Value || _pieceMembers.Value <= 0 || _pieceAnchor.Value == Vector3.zero)
            {
                _pieceSeenAt = -1f;
                return;
            }
            if (_pieceSeenAt < 0f) { _pieceSeenAt = Time.time; return; }
            if (Time.time - _pieceSeenAt < LateSeverGraceSeconds) return;
            if (_anchor.Value == Vector3.zero || Vector3.Distance(_anchor.Value, swarm.AnchorWorld) > LateSeverCatchUp * settings.NudgeThreshold)
                return;   // still being nudged in from where it hatched: a piece cut from there would start in the wrong place
            Vector3 at = _pieceAnchor.Value;
            Vector3 away = at - swarm.AnchorWorld;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -swarm.BodyForward;
            swarm.NearestMembers(at, Mathf.Min(_pieceMembers.Value, swarm.MemberCount / 2), _sevPiece);
            Sever(swarm, _sevPiece, at, away);
            _pieceSeenAt = -1f;
        }

        const float LateSeverGraceSeconds = 1f;
        const float LateSeverCatchUp = 4f;   // x NudgeThreshold: how close its body must have come to the server's

        [ClientRpc]
        void Sever_ClientRpc(Vector3 at, int members, Vector3 away)
        {
            if (IsServer || !_swarm) return;   // the server parted its own body exactly
            _swarm.NearestMembers(at, members, _sevPiece);
            Sever(_swarm, _sevPiece, at, away);
        }

        /// <summary>Every peer: <paramref name="slots"/> leave the body and hatch, grown, as the Severed (its seed asks
        /// <see cref="ISwarmSeedGraft.TryGetSeedGraft"/>); the stomach splits by members.</summary>
        bool Sever(SwarmFauna swarm, List<int> slots, Vector3 at, Vector3 away)
        {
            var spawn = settings.SwarmSpawn;
            if (!spawn || !spawn.FaunaPrefab || _severed) return false;
            int before = swarm.MemberCount;
            _grafts.Clear();
            int moved = swarm.ReleaseMembers(slots, _grafts);
            if (moved == 0) return false;
            float share = moved / (float)Mathf.Max(1, before);
            for (int e = 0; e < 4; e++) _seedStomach[e] = swarm.StomachVolume(e) * share;
            swarm.ScaleStomach(1f - share);
            _seedAt = at;
            _seedHeading = away;
            _seedPending = true;
            _severed = CellLifeSpawnerBase.SpawnFaunaWithDomain(swarm.HostCell, spawn.FaunaPrefab, at, swarm.domain, at, spawn) as SwarmFauna;
            if (_severed) return true;
            // no second body to put them in: they go back where they were, and nothing was lost
            CSDebug.LogError("[Tandava] The Severed could not hatch (TandavaSettings.SwarmSpawn's FaunaPrefab is not a SwarmFauna?) - " +
                             "the cut piece grafts straight back on.");
            _seedPending = false;
            for (int q = 0; q < _grafts.Count; q++) swarm.Graft(_grafts[q]);
            for (int e = 0; e < 4; e++) _bank[e] = _seedStomach[e];
            swarm.BankAll(_bank);
            return false;
        }

        bool ISwarmSeedGraft.TryGetSeedGraft(SwarmFauna swarm, List<SwarmGraft> grafts, float[] stomach)
        {
            if (!_seedPending || !IsSevered(swarm)) return false;
            _seedPending = false;
            grafts.AddRange(_grafts);
            for (int e = 0; e < 4; e++) stomach[e] = _seedStomach[e];
            return true;
        }

        // ───────────────────────────────────────────────────────────── the Severed's tick (every peer)

        void OnSeveredTick(SwarmFauna piece)
        {
            if (IsServer && _pieceCore != null)
            {
                float now = Time.time;
                float dt = _pieceLastTick < 0f ? 0f : now - _pieceLastTick;
                _pieceLastTick = now;
                var st = new TandavaSwarmState
                {
                    Alive = piece.MemberCount, Lost = piece.MembersLost, Anchor = S(piece.AnchorWorld),
                    Forward = S(piece.BodyForward), Up = S(piece.BodyUp), Side = S(piece.BodySide),
                    Stomach0 = piece.StomachVolume(0), Stomach1 = piece.StomachVolume(1),
                    Stomach2 = piece.StomachVolume(2), Stomach3 = piece.StomachVolume(3),
                    StomachFill = piece.StomachFraction, SinceBite = piece.SecondsSinceBite,
                    EatenTotal = piece.EatenTotal, Starving = piece.IsStarving,
                };
                if (_swarm) _pieceCore.Home = S(_swarm.AnchorWorld);
                _pieceCore.Tick(dt, st, _food, _pilots);
                _pieceCore.Events.Clear();   // its own beats are the HUD row's; the narrator speaks of the body's
                if (_pieceCore.Outcome != TandavaOutcome.Running)
                {
                    EndPiece();
                    PieceGone();
                    PieceGone_ClientRpc();
                    return;
                }
                if (!_swarm || _swarm.MemberCount == 0)
                {
                    int heir = piece.MemberCount;
                    Succeed();
                    _core.OnSuccession(heir);
                    EndPiece();
                    Succeed_ClientRpc();
                    return;
                }
                if (_pieceCore.HomeBound && Vector3.Distance(piece.AnchorWorld, _swarm.AnchorWorld) <= _ds.RejoinReach)
                {
                    int home = Rejoin();
                    _core.OnRejoined(home);
                    EndPiece();
                    Rejoin_ClientRpc();
                    return;
                }
                PublishPiece(piece.AnchorWorld, piece.MemberCount);
            }
            ApplyToSevered(piece);
            if (!IsServer)
            {
                Vector3 gap = _pieceAnchor.Value - piece.AnchorWorld;
                float d = gap.magnitude;
                if (d > settings.NudgeThreshold && _pieceAnchor.Value != Vector3.zero)
                    piece.TryNudge(gap * (Mathf.Min(settings.MaxNudge, d * settings.NudgeFraction) / d));
            }
        }

        void PublishPiece(Vector3 anchor, int members)
        {
            _pieceLive.Value = true;
            _pieceMembers.Value = members;
            _piecePlan.Value = _pieceCore.WantPlan;
            _piecePhase.Value = (int)_pieceCore.Phase;
            _pieceMood.Value = (int)_pieceCore.Mood;
            _pieceGoal.Value = V(_pieceCore.Goal);
            _pieceAnchor.Value = anchor;
            _pieceHome.Value = _pieceCore.HomeBound ? -1f : _pieceCore.RejoinRemaining;
        }

        void EndPiece()
        {
            _pieceCore = null;
            _pieceLive.Value = false;
        }

        /// <summary>Every peer: the Severed wears the plan its director wants (one form: every change is a pose), swims at
        /// its phase and mood's speed and turn, and takes its goal straight in.</summary>
        void ApplyToSevered(SwarmFauna piece)
        {
            int want = _piecePlan.Value >= 0 ? _piecePlan.Value : SeveredPlan;
            if (IsServer && _pieceCore != null) want = _pieceCore.WantPlan;
            if (want >= 0 && piece.FormIndex != want) piece.RequestPose(want);
            var phase = IsServer && _pieceCore != null ? _pieceCore.Phase : (TandavaPhase)_piecePhase.Value;
            var mood = IsServer && _pieceCore != null ? _pieceCore.Mood : (TandavaMood)_pieceMood.Value;
            TandavaDirectorCore.LeversFor(phase, mood, settings.Director, out float cruise, out float turn, out bool hold);
            piece.SetLevers(cruise, turn, hold);
            piece.Goal = PieceGoal(piece);
        }

        Vector3 PieceGoal(SwarmFauna piece)
        {
            if (IsServer && _pieceCore != null) return V(_pieceCore.Goal);
            return _pieceGoal.Value != Vector3.zero ? _pieceGoal.Value : piece.AnchorWorld;
        }

        // ───────────────────────────────────────────────────────────── home, or heir (the server decides; every peer does)

        [ClientRpc]
        void Rejoin_ClientRpc()
        {
            if (!IsServer) Rejoin();
        }

        /// <summary>The Severed grafts back on where it touches: up to the room the body's form has, the rest paid into the
        /// stomach as the eggs they were, and its own stomach poured in (up to capacity). Returns how many came home.</summary>
        int Rejoin()
        {
            if (!_severed || !_swarm) return 0;
            for (int e = 0; e < 4; e++) _bank[e] = _severed.StomachVolume(e);
            _grafts.Clear();
            _severed.Dissolve(_grafts);
            _severed = null;
            int form = Mathf.Clamp(_form.Value, 0, settings.Forms.Count - 1);
            int room = _swarm.FormMemberCount(Variant(form).PlanIndex) - _swarm.MemberCount;
            int home = 0;
            for (int q = 0; q < _grafts.Count; q++)
            {
                var g = _grafts[q];
                if (home < room) { _swarm.Graft(g); home++; }
                else _bank[g.Element] += _swarm.EggVolume(g.Element);
            }
            _swarm.BankAll(_bank);
            return home;
        }

        [ClientRpc]
        void Succeed_ClientRpc()
        {
            if (!IsServer) Succeed();
        }

        /// <summary>The body is gone and the Severed lives: the Severed is the body now. The old body's anchor goes (on a
        /// peer whose own body still had stragglers they go with it - the server's call is the creature's); the heir's
        /// first applied tick asks for the form's plan, not a pose, so it regrows into the form.</summary>
        void Succeed()
        {
            if (!_severed) return;
            var old = _swarm;
            _swarm = _severed;
            _severed = null;
            _appliedForm = -1;
            _lostSeen = _swarm.MembersLost;
            if (old) old.Dissolve(null);
        }

        [ClientRpc]
        void PieceGone_ClientRpc()
        {
            if (!IsServer) PieceGone();
        }

        /// <summary>The Severed's director ended it (cut to nothing, or starved): whatever is left of this peer's goes.</summary>
        void PieceGone()
        {
            if (!_severed) return;
            _severed.Dissolve(null);
            _severed = null;
        }
    }
}
