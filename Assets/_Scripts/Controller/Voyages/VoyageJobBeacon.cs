using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One job on the voyage's board: a switch ring standing on the bearing of the job's destination, with the job's
    /// title and who brought it hung above it. Fly through it to take the job.
    ///
    /// <para>It carries words, the one thing a freestyle toy otherwise never does, for the painting's SHARE / REPAINT
    /// reason (<see cref="ToyChoiceLabel"/>): three identical rings that send the Ark three different places, at a moment
    /// no menu stands between the pilot and the choice. The words are the content's authored lines.</para>
    /// </summary>
    public sealed class VoyageJobBeacon : Toy
    {
        Action _onTaken;

        /// <summary>Stand a beacon at <paramref name="placement"/>; <paramref name="onTaken"/> runs when it is flown.</summary>
        public static VoyageJobBeacon Create(Transform parent, ToyPlacement placement, Color color, string label,
            ToyContext context, Action onTaken)
        {
            var go = ToyFactory.CreateRoot("VoyageJob", parent, placement, color);
            var beacon = go.AddComponent<VoyageJobBeacon>();
            beacon._onTaken = onTaken;
            if (!string.IsNullOrEmpty(label)) ToyChoiceLabel.Add(go.transform, label, color, placement.TriggerRadius);
            beacon.Initialize(null, context, placement);
            return beacon;
        }

        protected override void OnActivated(IVesselStatus localVessel) => _onTaken?.Invoke();
    }
}
