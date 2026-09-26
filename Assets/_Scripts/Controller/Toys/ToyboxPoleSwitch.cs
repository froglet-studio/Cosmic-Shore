using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The two Toy Box top buttons, as places you fly to: a big switch at each of the cell's
    /// POLES, above and below the equatorial ring the ordinary toys sit on.
    ///
    /// <para><b>North: today's activity.</b> Threading it TAKES the player to the day's derived
    /// activity (<see cref="DailyToyActivity.Today"/>): it starts it, then puts the vessel in front
    /// of the ring that activity asks them to thread next (<see cref="ToyShellOption.Arrival"/>),
    /// facing through it - so a painting is waiting dead ahead rather than being started somewhere
    /// the player cannot see. It pays for trying (<see cref="DailyToyActivity.TryClaim"/>), the same
    /// pick and the same claim the app-shell card makes, so the menu and the world can never name
    /// two different activities or pay twice. <b>South: shuffle.</b> Threading it re-rolls every
    /// setting the live toys own (<see cref="ToyShuffle.Plan"/> + <see cref="ToyShuffle.ApplyAsync"/>),
    /// world last.</para>
    ///
    /// <para><b>No text.</b> Like every freestyle toy, a pole switch is read by its ring and its
    /// body; the Toy Box menu is where a player learns what it is called.</para>
    ///
    /// <para><b>Why the poles.</b> The equator is where the toys are, and these two are not toys -
    /// they are ways of pressing OTHER toys. A switch among the others would read as a seventh toy;
    /// one on the axis the ring turns about reads as belonging to the whole ring. They are drawn
    /// larger for the same reason: the arcade's two top buttons are bigger than its cards.</para>
    ///
    /// <para><b>Deliberately NOT an <see cref="IToyShellSurface"/>.</b> The app shell lists toys
    /// and the shuffle/daily pools are built from that list; a pole switch registering there would
    /// appear as a card in its own grid and could be dealt to itself.</para>
    ///
    /// <para>It has no <see cref="ScriptableObjects.ToyDefinitionSO"/>: a definition declares a
    /// <see cref="ToyCategory"/> and is harvested by the codex as a toy, and neither is true of
    /// these. <see cref="Toy.DisplayName"/> already tolerates a null definition.</para>
    /// </summary>
    public class ToyboxPoleSwitch : Toy
    {
        public enum PoleRole
        {
            DailyActivity = 0,
            Shuffle = 1,
        }

        /// <summary>Stand-off in front of the arrival ring, in ring radii - the Toy Box's own
        /// Navigate distance, so both ways of being taken somewhere arrive the same way.</summary>
        const float ArrivalRingFactor = 2.4f;

        /// <summary>Seconds of flight added to the stand-off: the ring may still be blooming (a
        /// fresh gate grows in over ~1.2 s and cannot fire until it has), and the player needs a
        /// beat to see it before they are through it.</summary>
        const float ArrivalLeadSeconds = 1.5f;

        PoleRole _role;
        bool _shuffling;

        public PoleRole Role => _role;

        /// <summary>
        /// Build one pole switch under <paramref name="parent"/>. The ring is the trigger volume,
        /// drawn at its own radius (the switch law), and it is NEUTRAL - neither switch hands you
        /// a specific domain, so neither may wear one (<see cref="ToySwitchSignal"/>). The body
        /// says which is which: CTA lime for today's activity (a reward waiting), white for shuffle.
        /// </summary>
        public static ToyboxPoleSwitch Build(PoleRole role, Transform parent, ToyPlacement placement,
            ToyContext context)
        {
            string id = role == PoleRole.DailyActivity ? "pole_daily_activity" : "pole_shuffle";
            var root = ToyFactory.CreateBareRoot(id, parent, placement.Position, placement.LookTarget,
                                                 placement.TriggerRadius);

            Color accent = role == PoleRole.DailyActivity
                ? ToyFactory.CtaLime(ToyFactory.Theme(context))
                : Color.white;

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            ToyFactory.AddSphereBody(body.transform, placement.BodyRadius, accent);

            var toy = root.AddComponent<ToyboxPoleSwitch>();
            toy._role = role;
            toy.ConfigureSwitchRing(placement.TriggerRadius, ToySwitchSignal.Neutral, Domains.Blue);
            toy.Initialize(null, context, placement);
            return toy;
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_role == PoleRole.DailyActivity) GoToDailyActivity();
            else if (!_shuffling) RunShuffle().Forget();
        }

        void GoToDailyActivity()
        {
            var pick = DailyToyActivity.Today;
            if (!pick.IsValid)
            {
                CSDebug.LogWarning("[ToyboxPoleSwitch] No toy is offering an activity right now, " +
                                   "so today's activity switch did nothing.");
                return;
            }

            var option = pick.Option;

            // Already under way? Then do not press it again - a second press PAUSES a painting and
            // ENDS a wander. Just go to it.
            var arrival = ResolveArrival(option);
            if (!arrival.IsValid)
            {
                // The player is already flying - OnTriggerEnter only fires in freestyle - so an
                // option that RequiresFreestyle needs no entry step here, unlike the menu's path.
                try
                {
                    option.Apply();
                }
                catch (System.Exception e)
                {
                    CSDebug.LogError($"[ToyboxPoleSwitch] Today's activity '{pick.ToyName} > " +
                                     $"{pick.ActivityName}' threw, so nothing was paid: {e}");
                    return;
                }

                arrival = ResolveArrival(option);
            }

            // An activity with no arrival (a wander, a voyage) happens wherever the player is, so
            // starting it IS taking them there.
            if (arrival.IsValid) TravelTo(arrival);

            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[ToyBox] Today's activity from the pole: {pick.ToyName} > {pick.ActivityName}" +
                (arrival.IsValid ? $", taken to {arrival.Position}." : ", started in place."));

            // Paid for TRYING, the same as the menu - and after the start, so an activity that
            // failed to start pays nothing. Idempotent: a second visit the same day pays 0.
            int paid = DailyToyActivity.TryClaim();
            if (paid > 0)
                CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ToyBox] Today's activity paid {paid} crystals.");
        }

        static ToyArrival ResolveArrival(ToyShellOption option)
        {
            if (option?.Arrival == null) return default;
            try
            {
                return option.Arrival();
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[ToyboxPoleSwitch] '{option.Label}' threw resolving where to go: {e}");
                return default;
            }
        }

        /// <summary>
        /// Put the vessel in front of <paramref name="arrival"/>'s ring, facing through it, at the
        /// Navigate stand-off plus a lead that scales with the vessel's own speed - it keeps its
        /// speed, so a fast hull needs more room to see the ring before it is through it.
        /// </summary>
        void TravelTo(ToyArrival arrival)
        {
            var gameData = Context != null ? Context.GameData : null;
            var player = gameData ? gameData.LocalPlayer : null;
            if (player?.Vessel == null)
            {
                CSDebug.LogWarning("[ToyboxPoleSwitch] No local vessel to take to today's activity.");
                return;
            }

            float speed = player.Vessel.VesselStatus != null
                ? Mathf.Max(0f, player.Vessel.VesselStatus.Speed)
                : 0f;
            float standOff = arrival.Radius * ArrivalRingFactor + speed * ArrivalLeadSeconds;

            Vector3 dir = arrival.Direction;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            var stand = arrival.Position - dir * standOff;
            player.SetPoseOfVessel(new Pose(stand, Quaternion.LookRotation(dir, up)));
        }

        async UniTaskVoid RunShuffle()
        {
            var plan = ToyShuffle.Plan();
            if (plan.Count == 0)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    "[ToyBox] Shuffle pole found nothing to change - no live toy is offering a setting.");
                return;
            }

            _shuffling = true;
            try
            {
                await ToyShuffle.ApplyAsync(plan, this.GetCancellationTokenOnDestroy());
            }
            catch (System.OperationCanceledException)
            {
                // The toybox went away mid-shuffle. What already applied stays applied.
            }
            finally
            {
                _shuffling = false;
            }
        }
    }
}
