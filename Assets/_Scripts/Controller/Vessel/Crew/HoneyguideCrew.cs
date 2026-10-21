using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide and Ratel: two seats, one hull (Honeyguide Lab round 6, <c>Docs/Studios/HONEYGUIDE_LAB.md</c>;
    /// <c>HONEYGUIDE.md</c> beside this file). The <b>Honeyguide</b> flies the hull (the Sparrow's airframe) with two triggers:
    /// RT Peck and LT Flit, each taken over for a while by the upgrade cards (<c>HoneyguideCrew.Triggers.cs</c>). The
    /// <b>Ratel</b> plays the <b>Dynamo</b>, the hex puzzle in <see cref="DynamoCore"/>: a made pattern sends one of its prisms,
    /// drawn at random, to the battery, where its domain is a <b>slip charge</b>, and fires that tabletop card's own effect.
    /// The <b>Slip</b>: touch a 1-D trail of a domain the battery holds with the nose within <c>aimCone</c> of the prism's long
    /// axis, and the hull rides it to the end of the trail it points at (<c>HoneyguideCrew.Zip.cs</c>).
    ///
    /// <para><b>Seats.</b> The swap is the arena's gesture (<see cref="PilotSwapGesture"/>: D-pad left / right, keys 1 / 2) or
    /// a double tap on the small board. Whichever seat is empty is played by an AI: the hull's own <c>AIPilot</c>, or the
    /// Ratel AI (<see cref="DynamoCore.AIPlan"/>, input-only, the lab's own planner).</para>
    ///
    /// <para><b>Local only.</b> As the Wrasse's seat: the Dynamo, the AI and the effects run on the machine that flies the
    /// hull. Networking the Ratel seat is a follow-up (HONEYGUIDE.md).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class HoneyguideCrew : MonoBehaviour, IHullParkAware
    {
        [Header("Config")]
        [Tooltip("Every number, card and word of the Honeyguide and Ratel (HoneyguideConfig.asset, written from the lab by " +
                 "Tools/Build/author_honeyguide_assets.py). The Honeyguide prefab wires it.")]
        [SerializeField] HoneyguideConfigSO config;

        static HoneyguideCrew _active;

        /// <summary>The crew riding the local hull, or null.</summary>
        public static HoneyguideCrew Active => _active;

        HoneyguideConfigSO _cfg;
        IPlayer _player;
        IVessel _vessel;
        IVesselStatus _status;
        DynamoSettings _s;
        DynamoCore _core;
        readonly DynamoCore.AIMemo _memo = new();
        System.Func<double> _aiDice;
        float _think;
        bool _humanEngineers, _weTookHull, _aiCrew;
        float _swapReadyAt;
        HoneyguideDynamoView _view;
        VesselPrismController _laidBy;
        readonly List<DynamoEvent> _events = new();

        // match-clock stamps the view animates from (unscaled-free: Time.time)
        internal float SpinAt { get; private set; } = -99f;
        internal float PortsAt { get; private set; } = -99f;
        internal float AbilityAt { get; private set; } = -99f;
        internal readonly float[] RetiredAt = { -99f, -99f, -99f };

        internal HoneyguideConfigSO Config => _cfg;
        internal DynamoSettings Settings => _s;
        internal DynamoCore Core => _core;
        /// <summary>True while the local human plays the Ratel and the hull's AI flies.</summary>
        public bool HumanEngineers => _humanEngineers;
        /// <summary>True when both seats are AI (a hull flown by its AIPilot on this machine).</summary>
        public bool IsAICrew => _aiCrew;
        /// <summary>The battery the two seats share: slip charges, colours 1 Ruby, 2 Jade, 3 Gold, oldest first.</summary>
        public IReadOnlyList<int> Battery => _core != null ? _core.Battery : System.Array.Empty<int>();

        // ------------------------------------------------------------------ binding

        // The local pilot's own hull, flown by a human - or handed to the AI by this component's own swap - or an AI crew: a
        // hull that is not the local pilot's, flown by its AIPilot on THIS machine (the host owns every AI). The Wrasse's rule.
        bool ShouldRun(out IVessel vessel, out bool aiCrew)
        {
            vessel = null;
            aiCrew = false;
            if (!config || !TryGetComponent(out vessel)) return false;
            var status = vessel.VesselStatus;
            if (VesselVisionShading.LocalVessel == transform)
                return _weTookHull || status == null || !status.AutoPilotEnabled;
            aiCrew = true;
            return config.AICrewEngineers && status != null && status.AutoPilotEnabled
                   && status.Player != null && status.Player.IsNetworkOwner && !status.Player.IsLocalUser;
        }

        void Bind(IVessel vessel)
        {
            _cfg = config;
            _vessel = vessel;
            _status = vessel.VesselStatus;
            _player = _status?.Player;
            _s = _cfg.ToSettings();
            int seed = System.Environment.TickCount;
            _core = new DynamoCore(_s, seed);
            _core.Events.Clear();
            _aiDice = DynamoCore.Mulberry32(seed ^ 0x51ed27);
            _memo.Key = "";
            _think = 0f;
            _humanEngineers = _weTookHull = false;
            ResetPilotSide();
            _execs = null;
            _laidBy = _status?.VesselPrismController;
            if (_laidBy) _laidBy.OnBlockSpawned += OnTrailPrismLaid;
            SpinAt = PortsAt = AbilityAt = -99f;
            for (int k = 0; k < RetiredAt.Length; k++) RetiredAt[k] = -99f;
            // the view, the swap and the static handle belong to the local pilot's seat; an AI crew draws nothing
            if (_aiCrew) return;
            if (_active && _active != this) Destroy(_active);
            _active = this;
            _view = new HoneyguideDynamoView(this);
            var ab = _cfg.AbilityText(_core.Ability);   // the ability the Ratel starts with, for both seats
            if (ab != null) Toast(TemplateText.Fill(_cfg.AbilityToastTemplate, "name", ab.name, "text", ab.text));
        }

        void Unbind()
        {
            if (_vessel == null) return;
            if (_humanEngineers) SetHumanEngineers(false);
            EndPilotSide();
            if (_laidBy) _laidBy.OnBlockSpawned -= OnTrailPrismLaid;
            _laidBy = null;
            _view?.Dispose();
            _view = null;
            _vessel = null; _status = null; _player = null; _core = null;
            if (_active == this) _active = null;
        }

        void OnDisable() => Unbind();

        void OnDestroy() => Unbind();

        void Update()
        {
            if (!ShouldRun(out var vessel, out bool aiCrew)) { Unbind(); return; }
            if (_vessel != null && aiCrew != _aiCrew) Unbind();   // the hull changed hands: seat it afresh
            if (_vessel == null) { _aiCrew = aiCrew; Bind(vessel); }
            if (!UnityLiveness.Alive(_vessel.Transform)) return;

            if (!_aiCrew) PollSwap();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_humanEngineers && _view != null) _view.ReadEngineerInput(_core, _s);
            else StepEngineerAI(dt);
            _core.Advance(_s, dt);
            DrainEvents();

            StepPilotSide(dt);
            _view?.Frame(dt);
        }

        // ------------------------------------------------------------------ seats

        void PollSwap()
        {
            int dir = PilotSwapGesture.DirectionThisFrame();
            if (dir == 0 && _view != null && _view.SmallViewDoubleTapped()) dir = 1;
            if (dir == 0 || Time.unscaledTime < _swapReadyAt) return;
            _swapReadyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            SetHumanEngineers(!_humanEngineers);
        }

        /// <inheritdoc cref="IHullParkAware.ReturnHumanToHull"/>
        public void ReturnHumanToHull()
        {
            if (!_humanEngineers) return;
            _weTookHull = false;   // the autopilot is the parker's now: SetHumanEngineers(false) must not stop it
            SetHumanEngineers(false);
        }

        void SetHumanEngineers(bool engineer)
        {
            if (_humanEngineers == engineer) return;
            _humanEngineers = engineer;
            _memo.Key = "";
            EndTriggers();   // a press held across the swap belongs to neither seat

            // The hull's own AI flies while the human plays the Ratel; its flight input is paused so the pointer and the
            // sticks belong to the board - the Wrasse's order. Coming back restores both, in the opposite order. Only an
            // autopilot this component started is stopped again.
            if (engineer)
            {
                SetFlightInputPaused(true);
                _weTookHull = !_status.AutoPilotEnabled;
                if (_weTookHull) _vessel.ToggleAIPilot(true);
            }
            else
            {
                if (_weTookHull && UnityLiveness.Alive(_vessel.Transform)) _vessel.ToggleAIPilot(false);
                _weTookHull = false;
                SetFlightInputPaused(false);
            }
            _view?.CancelDrag();
        }

        // the input is the PLAYER's, which outlives this hull (a swap re-homes it), so a destroyed one is skipped
        void SetFlightInputPaused(bool paused)
        {
            if (_player == null) return;
            if (UnityLiveness.Alive(_player.InputController)) _player.InputController.SetPause(paused);
            else if (_player.InputStatus != null) _player.InputStatus.Paused = paused;
        }

        // ------------------------------------------------------------------ the Ratel

        /// <summary>One Engineer input from the human's board (the view calls this). False when it was illegal.</summary>
        internal bool EngineerInput(DynamoInput inp) => _core != null && _humanEngineers && _core.Apply(_s, inp);

        void StepEngineerAI(float dt)
        {
            _think -= dt;
            if (_think > 0f) return;
            var skill = _cfg.EngineerSkill;
            _think = skill.think;
            var inp = _core.AIPlan(_s, new DynamoAISkill { Top = skill.top, Blunder = skill.blunder }, _aiDice, _memo);
            if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) _memo.Key = "";
            if (inp != null) _core.Apply(_s, inp.Value);
        }

        // The core's events, in order. An effect can change the Dynamo (and so raise more events) while they are read, so
        // they are copied out and drained until none are left.
        void DrainEvents()
        {
            for (int guard = 0; guard < 8 && _core.Events.Count > 0; guard++)
            {
                _events.Clear();
                _events.AddRange(_core.Events);
                _core.Events.Clear();
                foreach (var e in _events) OnDynamoEvent(e);
            }
        }

        void OnDynamoEvent(DynamoEvent e)
        {
            switch (e.Kind)
            {
                case DynamoEventKind.Pick:   // the battery prism is taken: the pattern's effect happens now
                    ApplyEffect(e.Pid, e.Colour, 0);
                    _lastMade = e.Pid;
                    Play(_cfg.PatternMadeSfx);
                    break;
                case DynamoEventKind.Tick:
                    SpinAt = Time.time;
                    Play(_cfg.SpinSfx);
                    break;
                case DynamoEventKind.Shuffle:
                    AbilityAt = Time.time;
                    Play(_cfg.RevealSfx);
                    var ab = _cfg.AbilityText(e.Ability);   // both seats read what the Ratel's new ability does
                    if (ab != null) Toast(TemplateText.Fill(_cfg.AbilityToastTemplate, "name", ab.name, "text", ab.text));
                    break;
                case DynamoEventKind.Ports:
                    PortsAt = Time.time;
                    break;
                case DynamoEventKind.Retire:
                    _lastRetired = e.Old;
                    if (e.Slot >= 0 && e.Slot < RetiredAt.Length) RetiredAt[e.Slot] = Time.time;
                    break;
            }
        }

        void Play(EventReference evt)
        {
            if (evt.IsNull || _aiCrew || _vessel == null) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(evt, _vessel.Transform.position);
        }
    }
}
