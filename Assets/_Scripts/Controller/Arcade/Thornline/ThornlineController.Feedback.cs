using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What Thornline shows and says to the LOCAL pilot (THORNLINE.md §5). Nothing here is replicated or changes the race:
    /// <list type="bullet">
    /// <item><b>The sight</b>: a faceted dome on the next thorn's face, sized to the part of the thorn the sling actually
    /// catches (its centre's 12 u, seen from the hull), grey and growing while the sling reloads, Lime (the call to act)
    /// when it is ready. Aim inside it and the sling anchors; the rest of the drawn face does not.</item>
    /// <item><b>Rivals in the world</b>: the thorn a rival sits on glows faintly in that rival's colour (a PrismLit light
    /// on the Blue thorns only), so the pack's spread along the chain reads without a leaderboard.</item>
    /// <item><b>Beats</b>: a snipe, a crystal reset, a crystal a rival took first, a dock caught short on a trail, passing
    /// and being passed - each an authored toast (420-426) and an empty FMOD slot.</item>
    /// </list>
    /// The vessel is untouched: the sight reads the Shrike's public <c>Sling01</c> and <c>Docked</c>.
    /// </summary>
    public partial class ThornlineController
    {
        const int RivalLightIdBase = 0x54480000;
        const int MaxRivalLights = 3;

        // Local state.
        Transform _localHull;
        ShrikeExecutor _localShrike;
        bool _hintPosted, _wasDocked, _wasReady, _wasSlinging;
        int _caughtShortToasts, _threadedAtSling = -1;
        float _nextPassToast, _snipeCheckUntil = -1f;
        // Per rival, the sign of (their rings - ours): a pass is called only when it goes from -1 to +1 or +1 to -1, so
        // drawing level (and the gun's tie) is never called.
        readonly Dictionary<IPlayer, int> _order = new();
        GameObject _sightRoot;
        Renderer _sightRenderer;
        Material _sightReady, _sightRest;

        void TickFeedback()
        {
            if (!gameData || _thorns.Count == 0) return;
            var local = gameData.LocalPlayer;
            if (local == null) return;

            if (!_hintPosted && gameData.IsTurnRunning)
            {
                _hintPosted = true;
                GameToastAPI.Post(hintToast, local.Domain);
            }

            var hull = local.Vessel?.Transform;
            if (hull != _localHull)
            {
                _localHull = hull;
                _localShrike = hull ? hull.GetComponentInChildren<ShrikeExecutor>(true) : null;
                _wasDocked = _wasReady = false;
            }
            if (!_localHull || local.RoundStats == null) { HideSight(); return; }

            int threaded = local.RoundStats.SwitchesThreaded;
            TickSight(local, threaded);
            TickDock(local);
            TickSnipe(local, threaded);
            TickCrystals(local);
            TickPasses(local, threaded);
            TickRivalLights(local);
        }

        // ── The sight ────────────────────────────────────────────────────

        void TickSight(IPlayer local, int threaded)
        {
            int ring = RingIndexFor(Mathf.Min(threaded, Mathf.Max(0, RaceLength - 1)));
            int target = ring + 1;
            // As the AI plan's Back: when the next thorn is not on the next ring's line from here (at the gun, where the
            // seats face the start thorn, or after a short dock), the sight sits on the leg's start thorn instead.
            if (target < _thorns.Count && ring < _course.Count && Holds(_thorns[ring].Prism)
                && !ThornlineAIPlan.Threads(_localHull.position, _thorns[target].Thorn.Position, _course[ring])
                && (_localHull.position - _thorns[ring].Thorn.Position).magnitude > _thorns[ring].Thorn.Size)
                target = ring;
            if (!_localShrike || threaded >= RaceLength || target >= _thorns.Count || !Holds(_thorns[target].Prism))
            {
                HideSight();
                return;
            }
            EnsureSight(local.Domain);
            var thorn = _thorns[target].Thorn;
            Vector3 hull = _localHull.position;
            Vector3 toHull = hull - thorn.Position;
            float dCentre = toHull.magnitude;
            if (dCentre < thorn.Size) { HideSight(); return; }
            Vector3 dir = toHull / dCentre;
            float toFace = FaceDistance(dir, 0.5f * thorn.Size);
            float dSurface = dCentre - toFace;
            // The disc the sling catches, as it looks from the hull: never drawn bigger than the truth.
            float radius = ThornlineCourse.AnchorReach * dSurface / dCentre;
            float sling = Mathf.Clamp01(_localShrike.Sling01);
            bool ready = sling >= 1f;
            _sightRoot.SetActive(true);
            _sightRoot.transform.position = thorn.Position + dir * toFace;
            _sightRoot.transform.localScale = Vector3.one * (radius * Mathf.Lerp(sightReloadingScale, 1f, sling));
            if (ready != _wasReady || !_sightRenderer || _sightRenderer.sharedMaterial != (ready ? _sightReady : _sightRest))
            {
                if (_sightRenderer) _sightRenderer.sharedMaterial = ready ? _sightReady : _sightRest;
                if (ready && !_wasReady && _localShrike.Docked) Play(sightReadyEvent);
            }
            _wasReady = ready;
        }

        /// <summary>How far from a cube's centre (half side <paramref name="half"/>) its surface lies along <paramref name="dir"/>.</summary>
        static float FaceDistance(Vector3 dir, float half)
        {
            float m = Mathf.Max(Mathf.Abs(dir.x), Mathf.Max(Mathf.Abs(dir.y), Mathf.Abs(dir.z)));
            return m > 1e-4f ? half / m : half;
        }

        void EnsureSight(Domains domain)
        {
            if (_sightRoot) return;
            var theme = gameData ? gameData.ThemeManagerData : null;
            _sightReady = ToyFactory.SwitchMaterial(theme, ToySwitchSignal.Next, domain);
            _sightRest = ToyFactory.SwitchMaterial(theme, ToySwitchSignal.Neutral, domain);
            _sightRoot = new GameObject("ThornlineSight");
            var body = ToyFactory.AddSphereBody(_sightRoot.transform, 1f, ToyFactory.CtaLime(theme), _sightRest);
            _sightRenderer = body ? body.GetComponent<Renderer>() : null;
            _sightRoot.SetActive(false);
        }

        void HideSight()
        {
            if (_sightRoot && _sightRoot.activeSelf) _sightRoot.SetActive(false);
        }

        // ── Docks, snipes, crystals, passes ──────────────────────────────

        void TickDock(IPlayer local)
        {
            bool docked = _localShrike && _localShrike.Docked;
            if (docked && !_wasDocked && !_localShrike.DockedInNest)
            {
                if (OnAThorn(_localHull.position)) Play(cleanDockEvent);
                else
                {
                    Play(caughtShortEvent);
                    if (_caughtShortToasts++ < caughtShortToastsPerRace) GameToastAPI.Post(caughtShortToast, local.Domain);
                }
            }
            _wasDocked = docked;
        }

        bool OnAThorn(Vector3 hull)
        {
            for (int i = 0; i < _thorns.Count; i++)
            {
                var t = _thorns[i].Thorn;
                if (ThornlineCourse.RegrowBlocked(t.Position, t.Size + 2f * (ThornlineCourse.HullRadius + 2f), hull)) return true;
            }
            return false;
        }

        /// <summary>One sling that threaded two rings is a snipe. The base credits at most one ring a frame, so it is read
        /// from the sling itself: the rings threaded from its start, counted until a second after it ends (a client's
        /// credit arrives a round trip late).</summary>
        void TickSnipe(IPlayer local, int threaded)
        {
            bool slinging = _localShrike && (_localShrike.Slinging || _localShrike.Tethering);
            if (slinging && !_wasSlinging) { _threadedAtSling = threaded; _snipeCheckUntil = -1f; }
            if (!slinging && _wasSlinging && _threadedAtSling >= 0) _snipeCheckUntil = Time.time + 1f;
            _wasSlinging = slinging;
            if (_snipeCheckUntil < 0f) return;
            if (threaded - _threadedAtSling >= 2 && gameData.IsTurnRunning)
            {
                Play(snipeEvent);
                GameToastAPI.Post(snipeToast, local.Domain);
                _snipeCheckUntil = -1f;
            }
            else if (Time.time > _snipeCheckUntil) _snipeCheckUntil = -1f;
        }

        /// <summary>Every frame (so the taker's hull is still within a few units of it): a crystal that went from live to
        /// taken is called for whoever's hull is nearest.</summary>
        void TickCrystals(IPlayer local)
        {
            for (int i = 0; i < _crystals.Count; i++)
            {
                var slot = _crystals[i];
                bool live = IsLive(slot);
                if (slot.SeenLive && !live) OnCrystalTaken(local, slot);
                slot.SeenLive = live;
            }
        }

        void OnCrystalTaken(IPlayer local, CrystalSlot slot)
        {
            var players = gameData.Players;
            if (players == null) return;
            IPlayer taker = null;
            float best = 30f * 30f;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (!tf) continue;
                float d = (tf.position - slot.Position).sqrMagnitude;
                if (d < best) { best = d; taker = players[i]; }
            }
            if (taker == null) return;
            if (taker == local)
            {
                Play(crystalResetEvent);
                GameToastAPI.Post(crystalResetToast, local.Domain);
            }
            else if (local.RoundStats != null && local.RoundStats.SwitchesThreaded == slot.Leg - 1)   // on that leg
            {
                Play(crystalTakenEvent);
                GameToastAPI.Post(crystalTakenToast, taker.Domain, taker.Name);
            }
        }

        void TickPasses(IPlayer local, int threaded)
        {
            var players = gameData.Players;
            if (players == null || !gameData.IsTurnRunning) return;
            for (int i = 0; i < players.Count; i++)
            {
                var rival = players[i];
                if (rival == null || rival == local || rival.RoundStats == null) continue;
                int sign = System.Math.Sign(rival.RoundStats.SwitchesThreaded - threaded);
                if (sign == 0) continue;                              // level: keep the last order
                bool known = _order.TryGetValue(rival, out int was);
                _order[rival] = sign;
                if (!known || was == sign || Time.time < _nextPassToast) continue;
                _nextPassToast = Time.time + passToastSeconds;
                // {0} is the rival either way, so it wears the rival's colour.
                if (sign > 0) { Play(overtakenEvent); GameToastAPI.Post(overtakenToast, rival.Domain, rival.Name); }
                else { Play(passedEvent); GameToastAPI.Post(passedToast, rival.Domain, rival.Name); }
            }
        }

        // ── Rivals in the world ──────────────────────────────────────────

        void TickRivalLights(IPlayer local)
        {
            var players = gameData.Players;
            if (players == null || rivalThornGlow <= 0f) return;
            int lit = 0;
            for (int i = 0; i < players.Count && lit < MaxRivalLights; i++)
            {
                var rival = players[i];
                var tf = rival?.Vessel?.Transform;
                if (rival == local || !tf) continue;
                for (int k = 0; k < _thorns.Count; k++)
                {
                    var t = _thorns[k].Thorn;
                    if (!Holds(_thorns[k].Prism)) continue;
                    if (!ThornlineCourse.RegrowBlocked(t.Position, t.Size + 2f * (ThornlineCourse.HullRadius + 3f), tf.position)) continue;
                    int id = RivalLightIdBase + lit;
                    // Blue is the course's sentinel and never a racer's domain: the light paints thorns only.
                    PrismLit.PublishLight(id, LitVolume.Sphere(t.Position, ThornlineCourse.HalfDiagonal(t.Size) + 2f),
                                          rivalThornGlow, PrismLit.DomainTint(rival.Domain), 0.5f, Domains.Blue);
                    lit++;
                    break;
                }
            }
            for (int i = lit; i < MaxRivalLights; i++) PrismLit.ClearLight(RivalLightIdBase + i);
        }

        void OnThornRegrown(Vector3 at)
        {
            if (!thornRegrowEvent.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(thornRegrowEvent, at);
        }

        static void Play(FMODUnity.EventReference sound)
        {
            if (!sound.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(sound);
        }

        void ClearFeedback()
        {
            if (_sightRoot) Destroy(_sightRoot);
            _sightRoot = null;
            _sightRenderer = null;
            for (int i = 0; i < MaxRivalLights; i++) PrismLit.ClearLight(RivalLightIdBase + i);
            _order.Clear();
            _wasSlinging = false;
            _snipeCheckUntil = -1f;
            _hintPosted = false;
            _caughtShortToasts = 0;
            _localHull = null;
            _localShrike = null;
        }
    }
}
