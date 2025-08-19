namespace CosmicShore.Game
{
    public interface IHUDEffects
    {
        void AnimateDrain(int index, float duration, float fromNormalized);
        void AnimateRefill(int index, float duration, float toNormalized);
        void SetMeter(int index, float normalized);

        void SetToggle(string key, bool on);

    }
}