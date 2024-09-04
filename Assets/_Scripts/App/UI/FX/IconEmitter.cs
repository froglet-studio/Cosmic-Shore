using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.FX
{
    /// <summary>
    /// Built predominantly to add juice to currency balance changes, may have other uses
    /// Sends a bunch of ImageTemplate images eminating from Source to Target in a arc
    /// Large parts courtesy of GPT
    /// </summary>
    public class IconEmitter : MonoBehaviour
    {
        public enum EmissionMode { RandomAngle, Sweep, Scatter }

        [SerializeField] RectTransform Container;
        [SerializeField] Image Source;
        [SerializeField] Image Target;
        [SerializeField] Image ImageTemplate;
        [SerializeField] Vector2 SourceOffset;
        [SerializeField] Vector2 TargetOffset;
        [SerializeField] Vector2 StartSize;
        [SerializeField] Vector2 MidSize;
        [SerializeField] Vector2 EndSize;
        [SerializeField] int Quantity;
        [SerializeField] float MovementDuration;
        [SerializeField] float SpawnDuration;
        [SerializeField] EmissionMode Mode = EmissionMode.RandomAngle;
        [SerializeField] float MinAngle = 0f;
        [SerializeField] float MaxAngle = 0f;
        [SerializeField] float MaxScatterDistance = 50f;
        [SerializeField] bool SpiralBackIfPointingAway = true;
        [SerializeField] bool DoShrinkRoutine = true;
        [SerializeField] float JitterAmount = 0.1f;
        [SerializeField] float TargetAlpha = 0f; // Target alpha for fade

        [SerializeField] AudioClip targetReachedClip; // Audio clip
        [SerializeField] AudioClip onTriggerClip; // Audio clip
        [SerializeField] AudioSource audioSource; // Reference to the AudioSource component
        [SerializeField] float AudioVolume = 1.0f; // Volume for the audio clip
        [SerializeField] Vector2 TargetPulseMultiplier = new Vector2(1.5f, 1.5f); // Size to pulse to
        [SerializeField] float PulseDuration = 0.5f; // Duration of the pulse


        Vector2 sourceInitialSize;
        int arrivedCount = 0; // To keep track of how many images have arrived at the target

        void Start()
        {
            sourceInitialSize = Source.rectTransform.sizeDelta;
        }

        public void EmitIcons()
        {
            // Play the trigger audio clip
            if (onTriggerClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(onTriggerClip, AudioVolume);
            }

            // Reset arrived count
            arrivedCount = 0;

            // Reset the Source image size
            Source.rectTransform.sizeDelta = sourceInitialSize;

            // Start shrinking the Source image
            if (DoShrinkRoutine)
                StartCoroutine(ShrinkSourceImage());

            // Get world positions and convert them between coordinate spaces
            Vector3 sourceWorldPosition = Source.rectTransform.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform, // Parent of the icon images
                sourceWorldPosition,
                null, // Assumes canvas is set to Screen Space Overlay
                out Vector2 sourceLocalPosition
            );
            sourceLocalPosition += SourceOffset;

            // Convert the Target position to the coordinate space of the Source's parent
            Vector3 targetWorldPosition = Target.rectTransform.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                targetWorldPosition,
                null,
                out Vector2 targetLocalPosition
            );
            targetLocalPosition += TargetOffset;

            for (var i = 0; i < Quantity; i++)
            {
                var image = Instantiate(ImageTemplate);
                image.gameObject.SetActive(false);
                image.transform.SetParent(transform, false);
                image.rectTransform.sizeDelta = StartSize;

                image.rectTransform.anchoredPosition = sourceLocalPosition;


                // Determine the angle based on the selected mode
                float angle = 0f;
                if (Mode == EmissionMode.RandomAngle || Mode == EmissionMode.Scatter)
                {
                    angle = Random.Range(MinAngle, MaxAngle);
                }
                else if (Mode == EmissionMode.Sweep)
                {
                    angle = Mathf.Lerp(MinAngle, MaxAngle, (float)i / (Quantity - 1));
                }

                if (Mode == EmissionMode.Scatter)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle), Mathf.Sin(Mathf.Deg2Rad * angle)).normalized;
                    sourceLocalPosition += direction * Random.Range(0, MaxScatterDistance);
                }

                // Calculate a reasonable initial delay with jitter
                float initialDelay = (i * SpawnDuration / Quantity) + Random.Range(-JitterAmount, JitterAmount);


                StartCoroutine(MoveAlongArcCoroutine(image, sourceLocalPosition, targetLocalPosition, angle, initialDelay));
            }
        }

        IEnumerator MoveAlongArcCoroutine(Image image, Vector2 source, Vector2 target, float angle, float initialDelay)
        {
            yield return new WaitForSecondsRealtime(initialDelay);


            image.gameObject.SetActive(true);

            int steps = 0;
            int maxSteps = 50;
            float timePerStep = MovementDuration / maxSteps;

            // Calculate the direction vector based on the angle
            Vector2 direction = new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle), Mathf.Sin(Mathf.Deg2Rad * angle)).normalized;

            // Calculate the mid-point for the arc
            Vector2 midPoint = source + direction * Vector2.Distance(source, target) * 0.5f;

            // Adjust midPoint for spiraling up if necessary
            if (SpiralBackIfPointingAway && Mathf.Abs(angle) > 90f && Mathf.Abs(angle) < 270f)
            {
                midPoint += Vector2.up * Mathf.Abs(midPoint.y - source.y); // Bias towards upward spiral
            }

            while (steps < maxSteps)
            {
                float t = steps / (float)maxSteps;

                // Quadratic Bezier interpolation for parabolic motion
                Vector2 currentPos = Vector2.Lerp(
                    Vector2.Lerp(source, midPoint, t),
                    Vector2.Lerp(midPoint, target, t),
                    t
                );

                // Multi-phase size transition: grow to mid-size, then shrink to end-size
                float easedT = EaseInOutQuad(t);
                if (t < 0.5f)
                {
                    image.rectTransform.sizeDelta = Vector2.Lerp(StartSize, MidSize, easedT * 2f);
                }
                else
                {
                    image.rectTransform.sizeDelta = Vector2.Lerp(MidSize, EndSize, (easedT - 0.5f) * 2f);
                }

                // Fade to target alpha in the last 1/4 of the movement
                if (t > 0.75f)
                {
                    Color color = image.color;
                    color.a = Mathf.Lerp(1f, TargetAlpha, (t - 0.75f) * 4f);
                    image.color = color;
                }

                image.rectTransform.anchoredPosition = currentPos;

                steps++;
                yield return new WaitForSecondsRealtime(timePerStep);
            }

            arrivedCount++;

            // Play the audio clip when the image reaches its target
            if (targetReachedClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(targetReachedClip, AudioVolume);
            }

            // Start the Target pulsing coroutine when the first image arrives
            if (arrivedCount == 1)
            {
                StartCoroutine(PulseTargetImage());
            }

            Destroy(image.gameObject);
        }

        IEnumerator ShrinkSourceImage()
        {
            float elapsedTime = 0f;

            Vector2 initialSize = Source.rectTransform.sizeDelta;

            while (elapsedTime < SpawnDuration)
            {
                float t = elapsedTime / SpawnDuration;
                float easedT = EaseInOutQuad(t); // Apply easing to the time factor
                Source.rectTransform.sizeDelta = Vector2.Lerp(initialSize, Vector2.zero, easedT);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            Source.rectTransform.sizeDelta = Vector2.zero; // Ensure it ends exactly at zero
        }

        IEnumerator PulseTargetImage()
        {
            float elapsedTime = 0f;

            Vector2 initialSize = Target.rectTransform.sizeDelta;

            // Pulse grow
            while (elapsedTime < PulseDuration)
            {
                Target.rectTransform.sizeDelta = Vector2.Lerp(initialSize, initialSize*TargetPulseMultiplier, elapsedTime / PulseDuration);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            Target.rectTransform.sizeDelta = initialSize * TargetPulseMultiplier; // Ensure it reaches the pulse size

            // Wait until the midpoint of the arrivals
            yield return new WaitForSecondsRealtime((MovementDuration * Quantity / 2f) / 100f);

            elapsedTime = 0f;

            // Pulse shrink back to normal size
            while (elapsedTime < PulseDuration)
            {
                Target.rectTransform.sizeDelta = Vector2.Lerp(initialSize * TargetPulseMultiplier, initialSize, elapsedTime / PulseDuration);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            Target.rectTransform.sizeDelta = initialSize; // Reset for next time
        }

        // Easing function (ease in/out quadratic)
        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
    }
}