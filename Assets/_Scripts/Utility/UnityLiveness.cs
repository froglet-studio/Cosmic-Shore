using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// "Is this reference still usable?" — the ONE predicate for a reference whose static
    /// type is an INTERFACE (or <c>object</c>) but whose runtime object is a
    /// <see cref="UnityEngine.Object"/>.
    ///
    /// <para><b>`x != null` and `x?.` ARE NOT ENOUGH, and never were.</b> Unity reports a
    /// destroyed object as null through an <i>overloaded</i> <c>==</c> on
    /// <c>UnityEngine.Object</c>. An interface reference never reaches that operator — `!=` and
    /// C#'s null-propagating `?.` are plain reference comparisons — so a destroyed
    /// <c>Player</c>, <c>VesselController</c> or <c>RoundStats</c> sails straight through the
    /// guard and throws <c>MissingReferenceException</c> on the first member access.</para>
    ///
    /// <para>The failure shape is what makes it expensive: the holder is usually a cached
    /// handle nothing clears (<c>GameDataSO.LocalPlayer</c>, <c>Player.Vessel</c>), and the
    /// consumer is usually an <c>Update</c>/<c>LateUpdate</c> loop — so one destroyed object
    /// becomes an UNBOUNDED per-frame exception, and Unity's stack capture then eats the
    /// frame rate. Three such sites were storming at once when this was written
    /// (<c>MiniGameHUD.Update</c>, and every objective provider through
    /// <c>ObjectiveIndicator.LateUpdate</c>), all reaching through one stale handle.</para>
    ///
    /// <para><b>Fix the HANDLE, not the reader.</b> Roughly sixty call sites read
    /// <c>LocalPlayer</c> with `?.` and every one of them is individually reasonable; making
    /// each one remember the trap is a rule you can forget sixty times. The handles that hand
    /// out interface references to Unity objects self-heal through this predicate instead, so
    /// a reader written tomorrow is correct with nothing to wire.</para>
    /// </summary>
    public static class UnityLiveness
    {
        /// <summary>
        /// True while <paramref name="reference"/> is non-null AND — if it is really a
        /// <see cref="UnityEngine.Object"/> — has not been destroyed. A plain C# object
        /// (an edit-mode test fake) is alive iff it is non-null, so test doubles that are
        /// not Unity objects pass through untouched.
        /// </summary>
        public static bool Alive(object reference)
            => reference != null && !(reference is Object o && !o);

        /// <summary>
        /// <paramref name="reference"/> if it is <see cref="Alive"/>, else <c>null</c> — so a
        /// destroyed object is handed out as a REAL null that `?.` and `!= null` can both see.
        /// This is the shape a cached handle's getter wants.
        /// </summary>
        public static T Live<T>(T reference) where T : class
            => Alive(reference) ? reference : null;
    }
}
