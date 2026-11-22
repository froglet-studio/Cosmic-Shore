using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Counts every ability a pilot started during a Brood audit match, by hull and action name, and passes each report
    /// on to the observer that was there before it (the Theater's replay recorder), so recording is unchanged. Autopilots
    /// start abilities through <see cref="VesselActionTape"/> and a person's input through the handler, and both report
    /// here, so this sees what the round stats' ability timers miss (they count only handler-timed abilities).
    /// </summary>
    public sealed class BroodAuditActionCounter : IVesselActionObserver
    {
        IVesselActionObserver _inner;
        readonly Dictionary<IVesselStatus, Dictionary<string, int>> _starts = new();

        /// <summary>Puts the counter in front of whatever observes now; again if something replaced it since.</summary>
        public void Ensure()
        {
            if (VesselActionTape.Observer == this) return;
            _inner = VesselActionTape.Observer;
            VesselActionTape.Observer = this;
        }

        public void Remove()
        {
            if (VesselActionTape.Observer == this) VesselActionTape.Observer = _inner;
            _inner = null;
        }

        public void OnInputAction(IVesselStatus vessel, InputEvents input, byte device, bool down)
        {
            if (down) Bump(vessel, "input:" + input);
            _inner?.OnInputAction(vessel, input, device, down);
        }

        public void OnDirectAction(IVesselStatus vessel, ShipActionSO action, bool start)
        {
            if (start && action) Bump(vessel, action.name.Replace("(Clone)", "").Trim());
            _inner?.OnDirectAction(vessel, action, start);
        }

        void Bump(IVesselStatus vessel, string key)
        {
            if (vessel == null) return;
            if (!_starts.TryGetValue(vessel, out var d)) _starts[vessel] = d = new Dictionary<string, int>();
            d[key] = (d.TryGetValue(key, out var n) ? n : 0) + 1;
        }

        /// <summary>"name x count" for one vessel, most started first.</summary>
        public string For(IVesselStatus vessel) =>
            vessel != null && _starts.TryGetValue(vessel, out var d)
                ? string.Join(" ", d.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}x{kv.Value}"))
                : "";

        public int Total(IVesselStatus vessel) => vessel != null && _starts.TryGetValue(vessel, out var d) ? d.Values.Sum() : 0;
    }
}
