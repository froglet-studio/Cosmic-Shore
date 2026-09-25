using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The two Toy Box top buttons, as places you fly to: a big switch at each of the cell's
    /// POLES, above and below the equatorial ring the ordinary toys sit on.
    ///
    /// <para><b>North: today's activity.</b> Threading it starts the day's derived activity
    /// (<see cref="DailyToyActivity.Today"/>) and pays for trying it
    /// (<see cref="DailyToyActivity.TryClaim"/>) - the same pick and the same claim the app-shell
    /// card makes, so the menu and the world can never name two different activities or pay
    /// twice. <b>South: shuffle.</b> Threading it re-rolls every setting the live toys own
    /// (<see cref="ToyShuffle.Plan"/> + <see cref="ToyShuffle.ApplyAsync"/>), world last.</para>
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
    /// <see cref="ToyCategory"/> and is harvested by the codex as a toy, and
    /// neither is true of these. <see cref="Toy.DisplayName"/> already tolerates a null
    /// definition.</para>
    /// </summary>
    public class ToyboxPoleSwitch : Toy
    {
        public enum PoleRole
        {
            DailyActivity = 0,
            Shuffle = 1,
        }

        /// <summary>How often the daily label re-reads its source - a day rolls over rarely, and
        /// the registry fills in as the other toys bloom, so once a second is plenty.</summary>
        const float LabelRefreshSeconds = 1f;

        PoleRole _role;
        TMP_Text _label;
        string _lastLabel = "";
        float _nextRefresh;
        bool _shuffling;
        string _lastShuffle = "";

        /// <summary>Set by <see cref="Build"/> before <see cref="Toy.Initialize"/>.</summary>
        public PoleRole Role => _role;

        /// <summary>
        /// Build one pole switch under <paramref name="parent"/>. The ring is the trigger volume,
        /// drawn at its own radius (the switch law), and it is NEUTRAL - neither switch hands you
        /// a specific domain, so neither may wear one (<see cref="ToySwitchSignal"/>).
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
            toy._label = ToyFactory.AddRingedLabel(root.transform, "", accent,
                                                   placement.TriggerRadius, placement.BodyRadius);
            toy.RefreshLabel();

            toy.ConfigureSwitchRing(placement.TriggerRadius, ToySwitchSignal.Neutral,
                                    Domains.Blue);
            toy.Initialize(null, context, placement);
            return toy;
        }

        protected override void Update()
        {
            base.Update();

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + LabelRefreshSeconds;
            RefreshLabel();
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (_role == PoleRole.DailyActivity) StartDailyActivity();
            else if (!_shuffling) RunShuffle().Forget();
        }

        void StartDailyActivity()
        {
            var pick = DailyToyActivity.Today;
            if (!pick.IsValid)
            {
                CSDebug.LogWarning("[ToyboxPoleSwitch] No toy is offering an activity right now, " +
                                   "so today's activity switch did nothing.");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[ToyBox] Today's activity from the pole: {pick.ToyName} > {pick.ActivityName}.");

            // The player is already flying - OnTriggerEnter only fires in freestyle - so an
            // option that RequiresFreestyle needs no entry step here, unlike the menu's path.
            try
            {
                pick.Option.Apply();
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[ToyboxPoleSwitch] Today's activity '{pick.ToyName} > " +
                                 $"{pick.ActivityName}' threw, so nothing was paid: {e}");
                return;
            }

            // Paid for TRYING, the same as the menu - and after the apply, so an activity that
            // failed to start pays nothing.
            int paid = DailyToyActivity.TryClaim();
            if (paid > 0)
                CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ToyBox] Today's activity paid {paid} crystals.");

            RefreshLabel();
        }

        async UniTaskVoid RunShuffle()
        {
            List<ToyShuffle.Pick> plan = ToyShuffle.Plan();
            if (plan.Count == 0)
            {
                _lastShuffle = "NOTHING TO SHUFFLE";
                RefreshLabel();
                return;
            }

            _shuffling = true;
            // Described BEFORE it is applied - an option's label is read off its live toy.
            string summary = ToyShuffle.Describe(plan);
            RefreshLabel();

            try
            {
                await ToyShuffle.ApplyAsync(plan, this.GetCancellationTokenOnDestroy());
                _lastShuffle = summary;
            }
            catch (System.OperationCanceledException)
            {
                // The toybox went away mid-shuffle. What already applied stays applied.
            }
            finally
            {
                _shuffling = false;
                if (this) RefreshLabel();
            }
        }

        void RefreshLabel()
        {
            if (!_label) return;

            string text = _role == PoleRole.DailyActivity ? DailyLabel() : ShuffleLabel();
            if (text == _lastLabel) return;
            _lastLabel = text;
            _label.text = text;
        }

        static string DailyLabel()
        {
            // ASCII only - the UI font carries nothing past it (see CLAUDE.md, anti-patterns).
            var pick = DailyToyActivity.Today;
            string what = pick.IsValid
                ? $"{pick.ToyName}: {pick.ActivityName}".ToUpperInvariant()
                : "NOTHING TODAY";

            string reward;
            if (DailyToyActivity.ClaimedToday)
            {
                var left = DailyToyActivity.TimeUntilTomorrow;
                reward = $"DONE - NEW IN {(int)left.TotalHours}H {left.Minutes:00}M";
            }
            else
            {
                int crystals = DailyToyActivity.RewardCrystals;
                reward = crystals > 0 ? $"+{crystals} CRYSTALS" : "TRY IT";
            }

            return $"TODAY'S ACTIVITY\n<size=60%>{what}\n{reward}</size>";
        }

        string ShuffleLabel()
        {
            if (_shuffling) return "SHUFFLE\n<size=60%>SHUFFLING...</size>";
            string line = string.IsNullOrEmpty(_lastShuffle)
                ? "NEW WORLD, DOMAIN, VESSEL"
                : _lastShuffle.ToUpperInvariant();
            return $"SHUFFLE\n<size=60%>{line}</size>";
        }
    }
}
