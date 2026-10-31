using System.Collections.Generic;
using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hive mode (Garrett, 2026-10-31; the Ratel Raid Lab, <c>Docs/Studios/HONEYGUIDE_RAID_LAB.md</c>). An omnicrystal the
    /// Honeyguide collects becomes a hive in the world, and the Ratel goes into hive mode: the current spin completes at once,
    /// the Dynamo's clock holds, and the board becomes the hive's comb. The Ratel drags chains of three or more touching cells
    /// of one other domain (<see cref="HiveCore"/>); a chain breaks the hive's real prisms. Shields take two passes, a chain
    /// that touches danger stings. Completing a colour earns 2 prisms of it on the Dynamo and 1 charge; both colours earn our
    /// own too. Hive mode ends when the hive is cleared, after <c>raidSeconds</c>, or on Leave; the rewards place themselves
    /// on the Dynamo's largest planar crystal, and a fresh spin starts. The hull is debuff-immune and the hive super-shielded
    /// until then; what is left of the hive stays as ordinary mass.
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        HiveSettings _hs;
        Hive _hive;
        double _hiveT;
        readonly HiveCore.AIPlan _hiveAi = new();
        int[] _hivePath;
        int _hiveAt;
        float _hiveCellT, _hiveThink;
        int _omniPending;
        Vector3 _omniAt;
        Prism[] _comb;   // the hive's real prisms, by cell (null where there is none, or it broke)
        bool _immune;
        readonly List<HiveCore.Landed> _landed = new();
        readonly StringBuilder _names = new();
        float _stripScanAt;

        internal Hive Hive => _hive;
        internal bool InHive => _hive != null;
        internal double HiveTime => _hiveT;
        internal HiveSettings HiveSettings => _hs;
        internal float HiveAt { get; private set; } = -99f;
        internal float StungAt { get; private set; } = -99f;
        internal int StungCell { get; private set; } = -1;
        /// <summary>The planar crystal the last rewards landed on, the cells they landed in and the cells it goes on to.</summary>
        internal float GlowAt { get; private set; } = -99f;
        internal readonly List<int> GlowCells = new(), GlowLanded = new();
        internal List<int[]> GlowNext = new();
        /// <summary>The nearest omnicrystal's distance from the hull (u), or -1 when none is out (the strip).</summary>
        internal float StripDistance { get; private set; } = -1f;

        /// <summary>The player's domain as a Dynamo colour (Jade when the hull has none yet).</summary>
        internal int Own
        {
            get
            {
                int c = _status != null ? HoneyguideConfigSO.ColourOf(_status.Domain) : 0;
                return c != 0 ? c : 2;
            }
        }

        void BindHive()
        {
            _hs = _cfg.ToHiveSettings();
            _hive = null;
            _hiveT = 0;
            _hiveAi.Plan = null;
            _hivePath = null;
            _hiveThink = 0f;
            _omniPending = 0;
            _comb = null;
            HiveAt = StungAt = GlowAt = -99f;
            GlowCells.Clear();
            GlowLanded.Clear();
            GlowNext.Clear();
            StripDistance = -1f;
        }

        void UnbindHive()
        {
            if (_hive != null && !_rider) ReleaseComb();
            SetImmune(false);
            _hive = null;
        }

        /// <summary>An omnicrystal the hull collected (<see cref="HoneyguideOmniCrystalEffectSO"/>): hive mode opens at its pose
        /// on the next step, once any pick of three in progress is done.</summary>
        public void OnOmniCrystal(Vector3 at)
        {
            if (_vessel == null || _rider || _cfg == null || !_cfg.HiveMode) return;
            if (_hive != null) return;   // one hive at a time: an omnicrystal taken in hive mode opens none
            _omniPending++;
            _omniAt = at;
        }

        // The hull's machine, every frame after the Ratel's input: the hive's clock and its end; an omnicrystal opens it.
        void StepHive(float dt)
        {
            if (_hive == null)
            {
                if (_omniPending > 0 && _core.RevealState == null) { _omniPending = 0; EnterHive(_omniAt); }
                return;
            }
            _hiveT += dt;
            HiveCore.Tick(_hive, dt);
            string why = HiveCore.EndReason(_hive, _hiveT, _hs, Own);
            if (why.Length > 0) LeaveHive(why);
        }

        void EnterHive(Vector3 at)
        {
            if (_cfg.HiveCompletesSpin) _core.SpinNow(_s);   // "the ratel ends their current timer"
            var hive = HiveCore.MakeHive(_aiDice, _hs, Own);
            if (hive == null)
            {
                CSDebug.LogWarning("[Honeyguide] no solvable hive in 60 tries with these hive settings; hive mode skipped");
                return;
            }
            _hive = hive;
            _hiveT = 0;
            _hiveAi.Plan = null;
            _hivePath = null;
            _hiveThink = 0f;
            HiveAt = Time.time;
            LayComb(at);
            SetImmune(true);
            Toast(_cfg.HiveEnterToast);
            Play(_cfg.HiveEnterSfx);
            _view?.CancelDrag();
            if (_remoteRatel) SendState();
        }

        /// <summary>One hive move from the board (0 start a chain at a cell, 1 drag on to a cell, 2 let go, 3 leave). On a
        /// rider's copy it is applied here at once and sent to the hull's machine, whose answer is the board.</summary>
        internal void HiveInput(int kind, int cell)
        {
            if (_hive == null) return;
            ApplyHiveInput(kind, cell);
            if (_rider)
            {
                _inputScratch[0] = HiveInputBase + kind; _inputScratch[1] = 0; _inputScratch[2] = 0;
                _inputScratch[3] = cell; _inputScratch[4] = 0; _inputScratch[5] = 0;
                CrewRelay.SendInput(this, 1, (int[])_inputScratch.Clone());
            }
        }

        // a rider's hive move travels as a board input whose kind is this plus the move
        const int HiveInputBase = 100;

        void ApplyHiveInput(int kind, int cell)
        {
            if (_hive == null) return;
            switch (kind)
            {
                case 0:
                    if (HiveCore.ChainStart(_hive, cell, _hs, Own) == HiveCore.Stung) OnStung(cell);
                    break;
                case 1:
                    if (HiveCore.ChainAdd(_hive, cell, _hs) == HiveCore.Stung) OnStung(cell);
                    break;
                case 2:
                {
                    var res = HiveCore.ChainEnd(_hive, _hs, Own);
                    if (res == null) break;
                    if (!_rider)
                        foreach (var step in res.Steps)
                            if (step[1] == 1) BreakComb(step[0]);
                    Play(_cfg.HiveChainSfx);
                    if (res.Completed)
                    {
                        string line = TemplateText.Fill(_cfg.HiveColourDoneTemplate, "colour", _cfg.ColourName(res.Colour),
                                                        "prisms", _hs.RewardDynamo.ToString(), "charges", _hs.RewardBattery.ToString());
                        if (_hive.Rewards.Count > _hive.Done.Count)
                        {
                            string both = TemplateText.Fill(_cfg.HiveBothDoneTemplate, "colour", _cfg.ColourName(Own));
                            if (both.Length > 0) line = line.Length > 0 ? line + "; " + both : both;
                        }
                        Toast(line);
                        Play(_cfg.HiveColourSfx);
                    }
                    break;
                }
                case 3:
                    if (!_rider) LeaveHive(HiveCore.EndLeave);
                    break;
            }
        }

        void OnStung(int cell)
        {
            StungAt = Time.time;
            StungCell = cell;
            Toast(_cfg.HiveStungToast);
            Play(_cfg.HiveStingSfx);
        }

        // The Ratel Raid Lab's aiStep in hive mode: the next chain (HiveCore.AINextChain), dragged a cell at a time.
        void StepHiveAI(float dt)
        {
            var sk = _cfg.HiveSkill;
            _hiveThink -= dt;
            if (_hivePath != null)
            {
                _hiveCellT -= dt;
                while (_hivePath != null && _hiveCellT <= 0f)
                {
                    ApplyHiveInput(_hiveAt == 0 ? 0 : 1, _hivePath[_hiveAt]);
                    _hiveAt++;
                    _hiveCellT += sk.cell;
                    if (_hive == null || _hive.Chain.Count == 0) { _hivePath = null; break; }   // a sting broke the chain
                    if (_hiveAt >= _hivePath.Length) { ApplyHiveInput(2, -1); _hivePath = null; _hiveThink = sk.cell * 2f; }
                }
                return;
            }
            if (_hiveThink > 0f || _hive.Stun > 0) return;
            var path = HiveCore.AINextChain(_hive, _hiveAi, sk.solver, sk.sloppy, _aiDice, _hs, Own);
            if (path == null) return;
            _hivePath = path;
            _hiveAt = 0;
            _hiveCellT = 0f;
        }

        // Hive mode ends: the rewards go back with no pick and place themselves on the largest planar crystal, the charges go
        // to the battery, a fresh spin starts, and what is left of the hive stays as ordinary mass.
        void LeaveHive(string why)
        {
            if (_hive.Chain.Count > 0) ApplyHiveInput(2, -1);
            var crystal = HiveCore.PlaceRewards(_core.Cells, _hive.Rewards, _hs.RewardDynamo, _landed);
            foreach (int c in _hive.Rewards)
                for (int k = 0; k < _hs.RewardBattery; k++) _core.BoardEffect(_s, DynamoBoardEffect.Charge, c);
            GlowAt = Time.time;
            GlowCells.Clear();
            GlowCells.AddRange(crystal.Cells);
            GlowLanded.Clear();
            foreach (var l in _landed) GlowLanded.Add(l.Cell);
            GlowNext = HiveCore.CrystalNext(_core.Cells, crystal);
            if (_cfg.HiveCompletesSpin) _core.Clock = 0;
            string rewards = _hive.Rewards.Count > 0
                ? TemplateText.Fill(_cfg.HiveRewardsTemplate, "colours", ColourList(_hive.Rewards))
                : _cfg.HiveNoRewardsLine;
            bool hadComb = _comb != null;
            ReleaseComb();
            if (hadComb && !_rider) SeedBees();
            SetImmune(false);
            _hive = null;
            _hivePath = null;
            Toast(TemplateText.Fill(_cfg.HiveEndTemplate, "rewards", rewards));
            Play(_cfg.HiveLeaveSfx);
            if (_remoteRatel) SendState();
        }

        internal string ColourList(IReadOnlyList<int> colours)
        {
            _names.Clear();
            for (int k = 0; k < colours.Count; k++)
            {
                if (k > 0) _names.Append(", ");
                _names.Append(_cfg.ColourName(colours[k]));
            }
            return _names.ToString();
        }

        void SetImmune(bool on)
        {
            if (_immune == on) return;
            var rs = _status?.ResourceSystem;
            if (rs) rs.SetElementalDebuffImmunity(this, on);
            _immune = on;
        }

        // ------------------------------------------------------------------ the hive in the world

        // The omnicrystal becomes the hive: a flat hex comb of real prisms where it was taken - hiveOffset behind it along the
        // hull's flight, square to it, so the hull that just took it is not inside it - each cell's domain, every one
        // super-shielded (invulnerable) while hive mode lasts. Laid by the hull's own spawn channel, as the Condenser's lane
        // is; ours belong to the pilot, the other domains' to nobody.
        Vector3 _combAt, _combNormal;

        void LayComb(Vector3 at)
        {
            _comb = new Prism[DynamoCore.CellCount];
            var spawner = _status?.VesselPrismController;
            var channel = spawner ? spawner.PrismSpawnChannel : null;
            if (!channel) return;
            var t = _vessel.Transform;
            var normal = t.forward;
            at -= normal * _cfg.HiveOffset;
            var right = Vector3.Cross(t.up, normal);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.Cross(Vector3.up, normal);
            right.Normalize();
            var up = Vector3.Cross(normal, right);
            var rot = Quaternion.LookRotation(normal, up);
            _combAt = at;
            _combNormal = normal;
            var scale = spawner.TrailBaseScale;
            float sp = _cfg.HiveCellSpacing, root3 = Mathf.Sqrt(3f);
            for (int i = 0; i < DynamoCore.CellCount; i++)
            {
                var x = _hive.Cells[i];
                if (x.Dom == 0) continue;
                var domain = HoneyguideConfigSO.DomainOf(x.Dom);
                var pos = at + right * (sp * root3 * (DynamoCore.Q[i] + DynamoCore.R[i] / 2f)) - up * (sp * 1.5f * DynamoCore.R[i]);
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = domain,
                    Rotation = rot,
                    SpawnPosition = pos,
                    Scale = scale,
                    PrismType = spawner.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;
                string owner = x.Dom == Own ? _status.PlayerName : string.Empty;
                prism.TargetScale = scale;
                prism.ownerID = owner;
                prism.ChangeTeam(domain);
                prism.Initialize(owner);
                prism.ActivateSuperShield();
                _comb[i] = prism;
            }
        }

        // A chain broke this cell: its prism comes off the hive (the super shield is dropped first, the sanctioned order),
        // scored to the hull. A silenced hull's hit deflects (VesselSilence): the prism keeps its super shield.
        void BreakComb(int cell)
        {
            if (_comb == null || cell < 0 || cell >= _comb.Length) return;
            var p = _comb[cell];
            _comb[cell] = null;
            if (!p || p.destroyed) return;
            var dir = p.transform.position - _vessel.Transform.position;
            p.DeactivateShields();
            p.Damage(dir.normalized * Mathf.Max(1f, _status.Speed), _status.Domain, _status.PlayerName, devastate: true);
            if (!p.destroyed) { p.ActivateSuperShield(); _comb[cell] = p; }
        }

        // Hive mode is over: the hive's super shields come off and what is left stays as ordinary mass - its shields where a
        // cell still had two hits, its danger where it was danger.
        void ReleaseComb()
        {
            if (_comb == null) return;
            for (int i = 0; i < _comb.Length; i++)
            {
                var p = _comb[i];
                if (!p || p.destroyed) continue;
                var x = _hive != null ? _hive.Cells[i] : null;
                p.DeactivateShields();
                if (x == null) continue;
                if (x.Danger) p.MakeDangerous();
                else if (x.Hits >= 2) p.ActivateShield();
            }
            _comb = null;
        }

        // Garrett 2026-10-31: "after the hive is left it should spawn bee fauna in the player's domain that protect it". A native
        // bee hive (BeeHiveFlora, Docs/ECOSYSTEM.md section 66) is planted in our domain behind the comb; its bees guard the comb
        // within their guard radius and grow their hive from mass stung off opposing fauna. The comb's prisms are trail
        // prisms, not a plant's health prisms, so they stay as our mass beside the hive rather than being adopted as its comb.
        // A client's hull plants nothing: a networked flora is planted by the simulation's authority (SpawnFlora's gate).
        void SeedBees()
        {
            var config = _cfg.LeftBeeHive;
            if (!config || _status == null) return;
            var at = _combAt - _combNormal * _cfg.LeftBeeHiveBehind;
            var cell = Cell.FindCellContaining(at) ?? Cell.FindNearestActiveCell(at);
            if (cell) BeeHiveFlora.SeedAt(cell, config, at, _status.Domain);
        }

        // ------------------------------------------------------------------ the strip

        // The nearest omnicrystal's distance from the hull, a few times a second, from the crystals' own registry.
        void ScanStrip()
        {
            if (Time.time < _stripScanAt) return;
            _stripScanAt = Time.time + 0.25f;
            var from = _vessel.Transform.position;
            float best = float.MaxValue;
            var active = Crystal.Active;
            for (int k = 0; k < active.Count; k++)
            {
                var c = active[k];
                if (!c || c.crystalProperties.Element != Element.Omni) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < best) best = d;
            }
            StripDistance = best < float.MaxValue ? Mathf.Sqrt(best) : -1f;
        }

        // ------------------------------------------------------------------ state copy (a Ratel on another machine)

        void WriteHiveState(List<int> into)
        {
            into.Add(_hive != null ? 1 : 0);
            if (_hive == null) return;
            HiveCore.WriteState(_hive, into);
            into.Add((int)(_hiveT * 1000));
        }

        void ReadHiveState(int[] data, int at)
        {
            if (at >= data.Length || data[at] != 1) { _hive = null; return; }
            int i = at + 1;
            var hive = HiveCore.ReadState(data, ref i);
            if (hive == null) return;
            if (_hive == null) HiveAt = Time.time;
            _hive = hive;
            _hiveT = i < data.Length ? data[i] / 1000.0 : 0;
        }
    }
}
