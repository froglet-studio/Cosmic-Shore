using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One trigger of the Meerkat's wormhole (<c>R_VesselActions/MEERKAT.md</c>): <see cref="Trigger.Form"/> on RT
    /// (<c>InputEvents.RightStickAction</c>) forms the tunnel the reticle shows and dives in; <see cref="Trigger.Shorten"/>
    /// on LT (<c>LeftStickAction</c>) shortens a straighter reticle while held (a key or a touch ramps it to full in
    /// <c>keyRampSeconds</c>; a pad's analog LT is read live). Shared and stateless, the <c>StoatSlingActionSO</c> shape:
    /// which trigger it is comes from <see cref="trigger"/>, and every per-vessel number lives on
    /// <see cref="MeerkatSolitonExecutor"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "MeerkatSolitonAction", menuName = "ScriptableObjects/Vessel Actions/Meerkat Soliton")]
    public sealed class MeerkatSolitonActionSO : ShipActionSO
    {
        public enum Trigger { Form = 0, Shorten = 1 }

        [Tooltip("RT forms the tunnel (Form); LT shortens the reticle while held (Shorten).")]
        [SerializeField] Trigger trigger = Trigger.Form;

        public Trigger Which => trigger;

        // Implicit-bool, never ?. - a destroyed executor is a Unity null that ?. would call into.
        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<MeerkatSolitonExecutor>() : null;
            if (!exec) return;
            if (trigger == Trigger.Form) exec.Form();
            else exec.BeginShorten();
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<MeerkatSolitonExecutor>() : null;
            if (!exec) return;
            if (trigger == Trigger.Shorten) exec.EndShorten();
            else exec.EndForm();   // inside the roots RT pushes you faster while held
        }
    }
}
