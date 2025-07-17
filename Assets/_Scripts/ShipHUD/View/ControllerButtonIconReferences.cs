using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class ControllerButtonIconReferences : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] Sprite inactiveIcon;
    [SerializeField] Sprite activeIcon;
    [SerializeField] float swapDuration = 0.2f;

    public Button Button { get; private set; }
    Image _img;
    Coroutine _swap;

    void Awake()
    {
        Button = GetComponent<Button>() ?? GetComponentInChildren<Button>();
        _img = GetComponent<Image>();
        _img.sprite = inactiveIcon;
    }

    public void ShowActive() => StartSwap(activeIcon);
    public void ShowInactive() => StartSwap(inactiveIcon);

    void StartSwap(Sprite target)
    {
        if (_swap != null) StopCoroutine(_swap);
        _swap = StartCoroutine(AnimateSwap(target));
    }

    IEnumerator AnimateSwap(Sprite target)
    {
        float half = swapDuration * 0.5f;
        // fade out
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            _img.color = new Color(1, 1, 1, 1 - t / half);
            yield return null;
        }
        _img.sprite = target;
        // fade in
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            _img.color = new Color(1, 1, 1, t / half);
            yield return null;
        }
        _img.color = Color.white;
    }
}
