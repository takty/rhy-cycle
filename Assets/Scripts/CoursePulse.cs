using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class CoursePulse : MonoBehaviour
{
    [SerializeField]
    private float duration = 0.18f;

    [SerializeField]
    private float maximumWidthMultiplier = 1.35f;

    [SerializeField]
    private Color pulseColor =
        new Color(
            1.0f,
            0.75f,
            0.2f,
            1.0f
        );

    private LineRenderer line;
    private Color normalStartColor;
    private Color normalEndColor;
    private float normalWidthMultiplier;
    private Coroutine pulseCoroutine;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();

        normalStartColor = line.startColor;
        normalEndColor = line.endColor;
        normalWidthMultiplier =
            line.widthMultiplier;
    }

    private void OnDisable()
    {
        RestoreAppearance();
    }

    public void Play()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }

        pulseCoroutine =
            StartCoroutine(PlayPulse());
    }

    private IEnumerator PlayPulse()
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float strength =
                Mathf.Sin(
                    progress * Mathf.PI
                );

            line.widthMultiplier =
                Mathf.Lerp(
                    normalWidthMultiplier,
                    maximumWidthMultiplier,
                    strength
                );

            line.startColor =
                Color.Lerp(
                    normalStartColor,
                    pulseColor,
                    strength
                );

            line.endColor =
                Color.Lerp(
                    normalEndColor,
                    pulseColor,
                    strength
                );

            yield return null;
        }

        RestoreAppearance();
        pulseCoroutine = null;
    }

    private void RestoreAppearance()
    {
        if (line == null)
        {
            return;
        }

        line.widthMultiplier =
            normalWidthMultiplier;

        line.startColor =
            normalStartColor;

        line.endColor =
            normalEndColor;
    }
}