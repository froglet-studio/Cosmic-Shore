using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's let-go gates (every peer; <see cref="BrittleStarTimeLine"/>): before every ring whose corner is
    /// tighter than the Speed the hull carries - the I3 hairpin apexes, the I4 return - two columns of posts stand either
    /// side of the line, where letting go of Speed Mode coasts the hull down to the corner's own speed. Laid from the
    /// broadcast course like Waystation's lanterns, as shielded Blue environment prisms (the prism Waystation's walls and
    /// Grizzly Time's kegs use), and regrown one post a tick.
    ///
    /// <para><b>Lit on a clean shift</b> (the LOCAL pilot only, nothing replicated): a let-go ring threaded carrying its
    /// corner's speed (<see cref="BrittleStarThreadGrade.CleanShift"/>, graded by <c>.Heat</c>) lights its gate in the
    /// pilot's domain colour (a <see cref="PrismLit"/> light; the posts stay Blue, their colour is the colour set's).</para>
    ///
    /// <para>The posts stand <see cref="BrittleStarTimeLine.ColumnOutset"/> past the mouth's rim, ~55 u off the line,
    /// outside the autopilot's 8-12 u rival-mass scan, and are SuperShielded (guns refuse them).</para>
    /// </summary>
    public partial class BrittleStarTimeController
    {
        [Header("Let-go gates")]
        [Tooltip("The environment prism the let-go gates are laid from (the SpawnablePrism Waystation's walls use). " +
                 "Unset = no gates; the race is unchanged.")]
        [SerializeField] Prism postPrefab;

        [Tooltip("Seconds between regrow ticks (one missing post per gate a tick).")]
        [SerializeField, Min(0.1f)] float postRegrowSeconds = 1.5f;

        [Tooltip("Seconds a clean shift's light stays up on its gate.")]
        [SerializeField, Min(0.1f)] float gateLightSeconds = 1.2f;

        const string PostOwnerId = "BrittleStarTimeLetGo";

        /// <summary>This mode's PrismLit source ids: one per ring's gate (Waystation owns 0x57A70000-0x57A7FFFF).</summary>
        const int GateLightIdBase = unchecked((int)0xB57A0000);

        sealed class LetGoGate
        {
            public int Ring;
            public Vector3 Centre;
            public readonly List<BrittleStarTimeLine.Post> Posts = new();
            public Prism[] Pieces;
            public readonly Trail Trail = new();
            public int NextPiece;
        }

        readonly List<LetGoGate> _gates = new();
        Transform _gateRoot;
        float _nextPostRegrowAt;
        int _litGateRing = -1;
        float _litGateUntil;

        void RaiseLetGoGates()
        {
            ClearLetGoGates();
            if (!postPrefab || _course.Count == 0) return;
            var marks = BrittleStarTimeLine.Marks(_course, TopSpeed);
            if (marks.Count == 0) return;
            // A root at the world origin: the course arrives in world positions.
            _gateRoot = new GameObject($"{ModeName}LetGoGates").transform;
            LetGoGate gate = null;
            foreach (var post in marks)
            {
                if (gate == null || gate.Ring != post.Ring)
                {
                    gate = new LetGoGate
                    {
                        Ring = post.Ring,
                        Centre = BrittleStarTimeLine.LetGoPoint(_course, post.Ring,
                            BrittleStarTimeLine.LetGoDistance(_course, post.Ring, TopSpeed)),
                    };
                    _gates.Add(gate);
                }
                gate.Posts.Add(post);
            }
            foreach (var g in _gates)
            {
                g.Pieces = new Prism[g.Posts.Count];
                for (int k = 0; k < g.Posts.Count; k++) LayPost(g, k, forReveal: true);
            }
        }

        void LayPost(LetGoGate gate, int index, bool forReveal)
        {
            var post = gate.Posts[index];
            float s = BrittleStarTimeLine.PostSize;
            var prism = PrismTrailBuilder.LayOne(postPrefab,
                new PrismLay(new SpawnPoint(post.Position, Quaternion.LookRotation(post.Forward, post.Up), new Vector3(s, s, s)),
                             Domains.Blue, PrismKind.SuperShielded),
                _gateRoot, gate.Trail, PostOwnerId, watchForReveal: forReveal);
            if (!prism) return;
            if (!forReveal) prism.CompletesAsLiveRibbon = true;   // a regrown post must not hold the arena-ready gate
            gate.Pieces[index] = prism;
        }

        /// <summary>A post needs laying again when it is gone, destroyed, or a pooled prism re-issued elsewhere (another
        /// owner, or moved off its spot). A standing post of ours whose domain was changed is still standing: no second
        /// post is laid on top of it.</summary>
        static bool PostNeedsRegrow(Prism piece, Vector3 at) =>
            !piece || piece.destroyed || piece.ownerID != PostOwnerId || (piece.transform.position - at).sqrMagnitude >= 1f;

        static bool IsOurPost(Prism prism) =>
            prism && !prism.destroyed && prism.ownerID == PostOwnerId && prism.Domain == Domains.Blue;

        void ClearLetGoGates()
        {
            foreach (var g in _gates)
            {
                if (g.Pieces == null) continue;
                foreach (var piece in g.Pieces)
                    if (IsOurPost(piece) && !EnvironmentPrismPool.TryRelease(piece))
                        Destroy(piece.gameObject);
            }
            _gates.Clear();
            if (_gateRoot) Destroy(_gateRoot.gameObject);
            _gateRoot = null;
            if (_litGateRing >= 0) PrismLit.ClearLight(GateLightIdBase + _litGateRing);
            _litGateRing = -1;
        }

        void TickLetGoGates()
        {
            if (_gates.Count == 0) return;
            TickGateLight();
            if (Time.time < _nextPostRegrowAt) return;
            _nextPostRegrowAt = Time.time + postRegrowSeconds;
            foreach (var g in _gates)
                for (int step = 0; step < g.Pieces.Length; step++)
                {
                    int k = (g.NextPiece + step) % g.Pieces.Length;
                    var piece = g.Pieces[k];
                    if (!PostNeedsRegrow(piece, g.Posts[k].Position)) continue;
                    LayPost(g, k, forReveal: false);
                    g.NextPiece = k + 1;
                    break;
                }
        }

        /// <summary>Light ring <paramref name="ring"/>'s let-go gate for the local pilot (one gate at a time: PrismLit
        /// shows few lights, shared with every explosion).</summary>
        void LightLetGoGate(int ring)
        {
            if (_litGateRing >= 0 && _litGateRing != ring) PrismLit.ClearLight(GateLightIdBase + _litGateRing);
            _litGateRing = ring;
            _litGateUntil = Time.time + gateLightSeconds;
        }

        void TickGateLight()
        {
            if (_litGateRing < 0) return;
            var local = gameData ? gameData.LocalPlayer : null;
            LetGoGate gate = null;
            foreach (var g in _gates) if (g.Ring == _litGateRing) { gate = g; break; }
            if (Time.time > _litGateUntil || gate == null || local == null)
            {
                PrismLit.ClearLight(GateLightIdBase + _litGateRing);
                _litGateRing = -1;
                return;
            }
            float reach = _course[gate.Ring].Radius + BrittleStarTimeLine.ColumnOutset + 2f * BrittleStarTimeLine.PostSize;
            PrismLit.PublishLight(GateLightIdBase + gate.Ring, LitVolume.Sphere(gate.Centre, reach), 1f, local.Domain,
                                  gateLightSeconds * 0.5f);
        }
    }
}
