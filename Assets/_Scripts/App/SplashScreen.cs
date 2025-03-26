using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using CosmicShore;

public class SplashLoader : MonoBehaviour
{
    [SerializeField] Image logoImage; // Assign in Inspector
    [SerializeField] float fadeDuration = 1.5f; // Time for fade in/out
    [SerializeField] float minDisplayTime = 2f; // Minimum time to display splash screen
    [SerializeField] string nextSceneName = "Menu_Main"; // Scene to load
    [SerializeField] Animator SceneTransitionAnimator;

    private AsyncOperation asyncLoad;
    private float elapsedTime = 0f;
    private bool isFadingOut = false;

    void Start()
    {
        StartCoroutine(LoadSceneAsync());
        StartCoroutine(FadeInLogo());
    }

    IEnumerator LoadSceneAsync()
    {
        // Start loading the next scene in the background
        asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
        asyncLoad.allowSceneActivation = false; // Prevent auto-switch

        // Monitor loading progress
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // Calculate estimated remaining time
        float estimatedRemainingLoadTime = Mathf.Max(fadeDuration, (elapsedTime / 0.9f) * 0.1f);

        // Ensure splash screen is shown for at least minDisplayTime
        float timeLeftToMeetMinDisplay = Mathf.Max(0, minDisplayTime - elapsedTime);
        float delayBeforeFade = Mathf.Max(0, estimatedRemainingLoadTime - fadeDuration, timeLeftToMeetMinDisplay);

        yield return new WaitForSeconds(delayBeforeFade);

        // Start fading out the logo
        StartCoroutine(FadeOutLogo());
    }

    IEnumerator FadeInLogo()
    {
        float timer = 0f;
        Color color = logoImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            logoImage.color = color;
            yield return null;
        }

        color.a = 1f;
        logoImage.color = color;
    }

    IEnumerator FadeOutLogo()
    {
        if (isFadingOut) yield break;
        isFadingOut = true;


        float timer = 0f;
        Color color = logoImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            logoImage.color = color;
            yield return null;
        }

        color.a = 0f;
        logoImage.color = color;

        SceneTransitionAnimator.enabled = true;
        SceneTransitionAnimator.SetTrigger("Start");

        yield return new WaitForSecondsRealtime(.5f);

        // Activate the scene once fade-out is complete
        asyncLoad.allowSceneActivation = true;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;
    }
}
