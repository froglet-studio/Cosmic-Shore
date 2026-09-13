using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The juke gesture contract (SCARAB.md §3.7). One property matters above the rest and it is
    /// the one the first cut got wrong: <b>a push that reaches the stick's limit COMMITS, at any
    /// push speed</b> — because committing is what fires the cavitation plate, and "the blast
    /// needs too fast a flick" was the playtest report that produced this file.
    /// </summary>
    public class ScarabJukeGestureTests
    {
        const float Engage = 0.35f;
        const float Perimeter = 1f;

        /// <summary>Feed a stick ramp through the resolver exactly as Update would, and return the
        /// actions it produced (None dropped).</summary>
        static List<ScarabJukeGestureAction> Play(params float[] deflections)
        {
            bool active = false, committed = false;
            var actions = new List<ScarabJukeGestureAction>();
            foreach (var d in deflections)
            {
                var a = ScarabJukeGesture.Resolve(d, active, committed, Engage, Perimeter);
                switch (a)
                {
                    case ScarabJukeGestureAction.Begin:
                        active = true;
                        committed = ScarabJukeGesture.AtLimit(d, Perimeter);
                        actions.Add(a);
                        break;
                    case ScarabJukeGestureAction.Commit:
                        committed = true;
                        actions.Add(a);
                        break;
                    case ScarabJukeGestureAction.End:
                        if (active) actions.Add(a);
                        active = false; committed = false;
                        break;
                }
            }
            return actions;
        }

        [Test]
        public void ASlowPushStillCommitsWhenItReachesTheLimit()
        {
            // THE REGRESSION THIS FILE EXISTS FOR. The thumb sweeps through the intermediate
            // magnitudes; the dash fires early and the commit lands when the stick arrives.
            var actions = Play(0f, 0.1f, 0.4f, 0.6f, 0.8f, 1f, 1f, 1f);
            Assert.AreEqual(2, actions.Count, "expected exactly Begin then Commit");
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
            Assert.AreEqual(ScarabJukeGestureAction.Commit, actions[1]);
        }

        [Test]
        public void AFastFlickCommitsImmediatelyAndNeverTwice()
        {
            // One frame from rest to the limit: Begin is already committed, so no separate Commit.
            var actions = Play(0f, 1f, 1f, 1f);
            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
        }

        [Test]
        public void APushThatNeverReachesTheLimitNeverCommits()
        {
            // The fine adjustment: it moves the ship and must not fire the plate or steal.
            var actions = Play(0f, 0.4f, 0.6f, 0.7f, 0.6f, 0.4f, 0.1f);
            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
            Assert.AreEqual(ScarabJukeGestureAction.End, actions[1]);
        }

        [Test]
        public void HoldingTheStickPinnedDashesExactlyOnce()
        {
            var ramp = new List<float> { 0f };
            for (int i = 0; i < 200; i++) ramp.Add(1f);
            var actions = Play(ramp.ToArray());
            Assert.AreEqual(1, actions.Count, "a held stick must not chatter out repeat dashes");
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
        }

        [Test]
        public void AWornStickThatTopsOutJustUnderFullStillCommits()
        {
            // The hardware margin. Without it the plate is unreachable on a stick that cannot
            // quite reach 1 — which is a controller problem presenting as a broken ability.
            Assert.IsTrue(ScarabJukeGesture.AtLimit(0.98f, Perimeter));
            var actions = Play(0f, 0.5f, 0.98f);
            Assert.AreEqual(ScarabJukeGestureAction.Commit, actions[1]);
        }

        [Test]
        public void HysteresisKeepsAWaveringPushAsOneGesture()
        {
            // Dipping below engage but staying above the release band must NOT restart the juke,
            // or a shaky thumb spends a dash per wobble.
            var actions = Play(0f, 0.5f, 0.3f, 0.5f, 0.3f, 0.5f);
            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
        }

        [Test]
        public void ReleasingBelowTheBandEndsTheGestureSoTheNextPushIsANewJuke()
        {
            var actions = Play(0f, 1f, 0f, 1f);
            Assert.AreEqual(3, actions.Count);
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
            Assert.AreEqual(ScarabJukeGestureAction.End, actions[1]);
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[2]);
        }

        [Test]
        public void ACommittedGestureDoesNotCommitAgainWhileHeld()
        {
            var actions = Play(0f, 0.5f, 1f, 1f, 1f, 1f);
            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual(ScarabJukeGestureAction.Commit, actions[1]);
        }

        [Test]
        public void ARestingStickProducesNothing()
        {
            Assert.AreEqual(0, Play(0f, 0.05f, 0.1f, 0.16f).Count,
                "stick drift below the release band must never start a juke");
        }

        /// <summary>
        /// Replay a ramp the way Update does, with a CALLER THAT REFUSES PARTIALS: a Begin below
        /// the perimeter is dropped and, crucially, leaves the gesture un-begun.
        ///
        /// This models a caller POLICY, not a shipped one. The Scarab had exactly this rule while
        /// the juke's blast rode a fully-held drift (a buried drift swallowed a nudge), and both
        /// the modifier and the rule are retired — <c>ScarabJukeController</c> no longer reads the
        /// drift at all. What is worth keeping is the property underneath, which is a contract of
        /// the state machine rather than of any caller: <b>Resolve is a pure function of
        /// (deflection, gestureActive, gestureCommitted), so a caller that DECLINES a Begin leaves
        /// the gesture un-begun and the same push still Begins — committed — when it reaches the
        /// limit.</b> Any future refusal rule is safe for that reason and no other.
        /// </summary>
        static List<ScarabJukeGestureAction> PlayWithCallerRefusingPartials(params float[] deflections)
        {
            bool active = false, committed = false;
            var actions = new List<ScarabJukeGestureAction>();
            foreach (var d in deflections)
            {
                var a = ScarabJukeGesture.Resolve(d, active, committed, Engage, Perimeter);
                bool atLimit = ScarabJukeGesture.AtLimit(d, Perimeter);
                switch (a)
                {
                    case ScarabJukeGestureAction.Begin:
                        if (!atLimit) break;              // the caller declines a partial
                        active = true; committed = true;
                        actions.Add(a);
                        break;
                    case ScarabJukeGestureAction.Commit:
                        committed = true; actions.Add(a);
                        break;
                    case ScarabJukeGestureAction.End:
                        if (active) actions.Add(a);
                        active = false; committed = false;
                        break;
                }
            }
            return actions;
        }

        [Test]
        public void ACallerRefusingPartialsProducesNothingFromANudge()
        {
            Assert.AreEqual(0, PlayWithCallerRefusingPartials(0f, 0.4f, 0.6f, 0.55f, 0.1f).Count,
                "a push that never reaches the limit produces nothing at all for such a caller");
        }

        [Test]
        public void RefusingTheNudgeDoesNotCostThePilotTheDash()
        {
            // The deferral is safe only because declining Begin leaves the gesture un-begun, so
            // the SAME push is still a fresh Begin when it reaches the limit — and one that is
            // committed on arrival. Lose this and any caller-side refusal would silently disarm
            // the dash it was only meant to defer.
            var actions = PlayWithCallerRefusingPartials(0f, 0.4f, 0.7f, 0.9f, 1f);
            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(ScarabJukeGestureAction.Begin, actions[0]);
        }
    }
}
