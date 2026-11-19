using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline's feedback for the LOCAL pilot (SPEARLINE.md §4). In the world: the plain face of the next armoured
    /// seal is drawn lifted from <c>plainFaceShowDistance</c>, so the face that cuts reads against the shields; a seal
    /// threaded on the first pass flashes round its hole. As toasts (authored text, <c>GameToastConfig_Spearline</c>):
    /// the armour hint at the first armoured seal, the seal that held (once), and a run of first-pass seals called. As
    /// sounds: the three slots, shipped empty. A look and a word only: nothing here is read by the race, and nothing is
    /// replicated.
    /// </summary>
    public partial class SpearlineController
    {
        int _litRing = -1;
        int _cleanStreak;
        bool _heldToastPosted, _armourToastPosted;
        float _nextFeedbackTick;

        void ResetFeedback()
        {
            _litRing = -1;
            _cleanStreak = 0;
            _heldToastPosted = _armourToastPosted = false;
            _nextFeedbackTick = 0f;
        }

        /// <summary>A few times a second: light the plain face of the local pilot's next armoured seal once it is near,
        /// and put out the one it has passed.</summary>
        void TickFeedback()
        {
            if (gameData == null || Time.time < _nextFeedbackTick || _sealPrisms.Count == 0) return;
            _nextFeedbackTick = Time.time + 0.25f;
            var local = gameData.LocalPlayer;
            var tf = local?.Vessel?.Transform;
            int want = -1;
            if (tf)
            {
                int threaded = local.RoundStats?.SwitchesThreaded ?? 0;
                int ring = threaded < RaceLength ? RingIndexFor(threaded) : -1;
                if (ring >= 0 && _sealOf.TryGetValue(ring, out var seal) && seal.Armoured &&
                    (tf.position - _course[ring].Position).sqrMagnitude <= plainFaceShowDistance * plainFaceShowDistance)
                {
                    want = ring;
                    if (!_armourToastPosted)
                    {
                        _armourToastPosted = true;
                        GameToastAPI.Post(GameToastSituation.SpearlineArmourHint, local.Domain);
                    }
                }
            }
            if (want == _litRing) return;
            if (_litRing >= 0) LiftPlainFace(_litRing, 0f);
            _litRing = want;
            if (_litRing >= 0) LiftPlainFace(_litRing, plainFaceLift);
        }

        void LiftPlainFace(int ring, float lift)
        {
            if (!_sealOf.TryGetValue(ring, out var seal)) return;
            for (int k = 0; k < seal.Count; k++)
            {
                int i = seal.Start + k;
                if (!_sealArmour[i] && Holds(_sealPrisms[i])) _sealPrisms[i].SetColorLift(lift, 0.3f);
            }
        }

        /// <summary>A regrown prism is laid fresh (unlifted): give it the face's lift if its seal is the lit one.</summary>
        void OnSealPrismRegrown(int i)
        {
            if (_sealRing[i] == _litRing && !_sealArmour[i] && Holds(_sealPrisms[i]))
                _sealPrisms[i].SetColorLift(plainFaceLift);
        }

        /// <summary>The local pilot crossed sealed ring <paramref name="ring"/>: it <paramref name="opened"/>, on the
        /// <paramref name="firstPass"/> or after closed ones. <see cref="_hole"/> holds the hole it was judged on.</summary>
        void OnLocalSealCrossed(int ring, Seal seal, bool opened, bool firstPass)
        {
            var local = gameData.LocalPlayer;
            var domain = local != null ? local.Domain : Domains.Blue;
            if (!opened)
            {
                _cleanStreak = 0;
                Play(sealHeldEvent);
                if (!_heldToastPosted)
                {
                    _heldToastPosted = true;
                    GameToastAPI.Post(GameToastSituation.SpearlineSealHeld, domain);
                }
                return;
            }
            Play(sealOpenedEvent);
            if (!firstPass)
            {
                _cleanStreak = 0;
                return;
            }
            FlashHole(seal);
            _cleanStreak++;
            if (cleanStreakCalls == null) return;
            for (int c = 0; c < cleanStreakCalls.Length; c++)
            {
                if (cleanStreakCalls[c] != _cleanStreak) continue;
                Play(cleanStreakEvent);
                GameToastAPI.Post(GameToastSituation.SpearlineCleanStreak, domain, _cleanStreak.ToString());
                break;
            }
        }

        /// <summary>The standing prisms bordering the hole just flown flash lifted and fade back to the face's look.</summary>
        void FlashHole(Seal seal)
        {
            if (holeFlashLift <= 0f) return;
            for (int h = 0; h < _hole.Count; h++)
            {
                var next = seal.Adjacency[_hole[h]];
                for (int m = 0; m < next.Length; m++)
                {
                    int i = seal.Start + next[m];
                    if (!Holds(_sealPrisms[i])) continue;
                    float rest = _sealRing[i] == _litRing && !_sealArmour[i] ? plainFaceLift : 0f;
                    _sealPrisms[i].SetColorLift(holeFlashLift);
                    _sealPrisms[i].SetColorLift(rest, holeFlashSeconds, holeFlashLift);
                }
            }
        }

        static void Play(FMODUnity.EventReference sound)
        {
            if (!sound.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(sound);
        }
    }
}
