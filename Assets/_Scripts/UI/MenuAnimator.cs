using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements.Experimental;

public abstract class MenuAnimator : MonoBehaviour
{
    [Serializable]
    public enum EasingType
    {
        Step,
        Linear,
        InSine,
        OutSine,
        InOutSine,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InPower,
        OutPower,
        InOutPower,
        InBounce,
        OutBounce,
        InOutBounce,
        InElastic,
        OutElastic,
        InOutElastic,
        InBack,
        OutBack,
        InOutBack,
        InCirc,
        OutCirc,
        InOutCirc
    }

    [SerializeField] protected float Duration = 1f;
    [SerializeField] protected EasingType easingType = EasingType.Linear;

    public float Ease(EasingType easing, float t)
    {
        switch (easing)
        {
            default:
                return Easing.Linear(t);

            case EasingType.Step:
                return Easing.Step(t);

            case EasingType.Linear:
                return Easing.Linear(t);

            case EasingType.InSine:
                return Easing.InSine(t);

            case EasingType.OutSine:
                return Easing.OutSine(t);

            case EasingType.InOutSine:
                return Easing.InOutSine(t);

            case EasingType.InQuad:
                return Easing.InQuad(t);

            case EasingType.OutQuad:
                return Easing.OutQuad(t);

            case EasingType.InOutQuad:
                return Easing.InOutQuad(t);

            case EasingType.InCubic:
                return Easing.InCubic(t);

            case EasingType.OutCubic:
                return Easing.OutCubic(t);

            case EasingType.InOutCubic:
                return Easing.InOutCubic(t);

            case EasingType.InPower:
                return Easing.InPower(t, 2);

            case EasingType.OutPower:
                return Easing.OutPower(t, 2);

            case EasingType.InOutPower:
                return Easing.InOutPower(t, 2);

            case EasingType.InBounce:
                return Easing.InBounce(t);

            case EasingType.OutBounce:
                return Easing.OutBounce(t);

            case EasingType.InOutBounce:
                return Easing.InOutBounce(t);

            case EasingType.InElastic:
                return Easing.InElastic(t);

            case EasingType.OutElastic:
                return Easing.OutElastic(t);

            case EasingType.InOutElastic:
                return Easing.InOutElastic(t);

            case EasingType.InBack:
                return Easing.InBack(t);

            case EasingType.OutBack:
                return Easing.OutBack(t);

            case EasingType.InOutBack:
                return Easing.InOutBack(t);

            case EasingType.InCirc:
                return Easing.InCirc(t);

            case EasingType.OutCirc:
                return Easing.OutCirc(t);

            case EasingType.InOutCirc:
                return Easing.InOutCirc(t);
        }
    }

    public virtual void Animate()
    {
        StartCoroutine(AnimateCoroutine());
    }
    protected abstract IEnumerator AnimateCoroutine();
}