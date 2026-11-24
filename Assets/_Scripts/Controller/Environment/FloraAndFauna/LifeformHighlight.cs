using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Light up ONE creature or plant for one pilot's eyes: its spindles lean toward the pilot's domain hue and glow (the
    /// leash mark's look, <see cref="Fauna.ApplyLeashMark"/>), so the thing a pilot is aiming at reads in the world itself
    /// (the LOCKED rule: show a world fact in the world, never as a ring on the screen). The Jewel Wasp's aim (Garrett,
    /// 2026-11-14: "when aiming with right trigger, highlight fauna").
    ///
    /// <para>Presentation only, on this machine only: nothing is published, and nothing about the creature changes. Taking
    /// the light off puts back what was there: a leashed creature its holder's mark, anything else no mark.</para>
    /// </summary>
    public static class LifeformHighlight
    {
        static readonly List<Spindle> s_spindles = new();

        /// <summary>Light <paramref name="target"/> (a <see cref="Fauna"/> or a <see cref="Flora"/>) in
        /// <paramref name="domain"/>'s hue: <paramref name="lean"/> 0..1 of the way, colours x (1 + <paramref name="glow"/>).</summary>
        public static void Light(Component target, Domains domain, float lean, float glow)
        {
            if (!target) return;
            if (target is Fauna fauna)
            {
                fauna.ApplyLeashMark(domain, lean, glow);
                return;
            }
            var colors = PrismLit.ColorSet;
            Color hue = default;
            bool on = lean > 0f && colors && colors.TryGetPrismKindColors(domain, PrismKind.Plain, out hue, out _);
            target.GetComponentsInChildren(true, s_spindles);
            for (int i = 0; i < s_spindles.Count; i++)
                if (s_spindles[i]) s_spindles[i].SetHueLean(hue, on ? lean : 0f, glow);
            s_spindles.Clear();
        }

        /// <summary>Take the light off <paramref name="target"/>: a leashed creature wears its holder's mark again.</summary>
        public static void Clear(Component target)
        {
            if (!target) return;
            if (target is Fauna fauna && fauna.IsLeashed)
            {
                if (fauna.IsSimAuthority) fauna.RefreshLeashMark();
                return;
            }
            Light(target, default, 0f, 0f);
        }
    }
}
