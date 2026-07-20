using System;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public sealed class GameAudio : MonoBehaviour
{
    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float jumpVolume = 0.35f;

    [SerializeField]
    private float jumpDuration = 0.07f;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float measureVolume = 0.45f;

    [SerializeField]
    private float measureDuration = 0.09f;

    private const int SampleRate = 44100;

    private AudioSource jumpSource;
    private AudioSource measureSource;

    private AudioClip jumpClip;
    private AudioClip measureClip;

    private void Awake()
    {
        AudioSource[] sources =
        GetComponents<AudioSource>();

        jumpSource = sources[0];

        measureSource = sources.Length >= 2
                ? sources[1]
                : gameObject.AddComponent<AudioSource>();

        ConfigureSource(jumpSource);
        ConfigureSource(measureSource);

        jumpClip = CreateJumpClip();
        measureClip = CreateMeasureClip();
    }

    private void OnDestroy()
    {
        if (jumpClip != null)
        {
            Destroy(jumpClip);
        }

        if (measureClip != null)
        {
            Destroy(measureClip);
        }
    }

    public void PlayJump(float synchronization)
    {
        if (jumpClip == null)
        {
            return;
        }

        jumpSource.pitch = Mathf.Lerp(
            0.9f,
            1.15f,
            Mathf.Clamp01(synchronization)
        );

        jumpSource.PlayOneShot(
            jumpClip,
            jumpVolume
        );
    }

    private AudioClip CreateJumpClip()
    {
        int sampleCount = Mathf.Max(
            1,
            Mathf.RoundToInt(
                SampleRate * jumpDuration
            )
        );

        float[] samples =
            new float[sampleCount];

        System.Random random =
            new System.Random(12345);

        for (int i = 0; i < sampleCount; i++)
        {
            float time =
                (float)i / SampleRate;

            float progress =
                (float)i / sampleCount;

            float frequency =
                Mathf.Lerp(
                    520.0f,
                    280.0f,
                    progress
                );

            float square =
                Mathf.Sin(
                    2.0f *
                    Mathf.PI *
                    frequency *
                    time
                ) >= 0.0f
                    ? 1.0f
                    : -1.0f;

            float noise =
                (float)(
                    random.NextDouble() * 2.0 - 1.0
                );

            float envelope =
                Mathf.Pow(
                    1.0f - progress,
                    2.0f
                );

            float sample =
                (
                    square * 0.7f +
                    noise * 0.3f
                ) * envelope;

            samples[i] =
                Mathf.Round(sample * 8.0f) /
                8.0f;
        }

        AudioClip clip = AudioClip.Create(
            "Jump8Bit",
            sampleCount,
            1,
            SampleRate,
            false
        );

        clip.SetData(samples, 0);

        return clip;
    }

    private AudioClip CreateMeasureClip()
    {
        int sampleCount = Mathf.Max(
            1,
            Mathf.RoundToInt(
                SampleRate * measureDuration
            )
        );

        float[] samples =
            new float[sampleCount];

        double phase = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress =
                (float)i / sampleCount;

            float frequency =
                Mathf.Lerp(
                    130.0f,
                    45.0f,
                    progress
                );

            phase +=
                2.0 *
                Math.PI *
                frequency /
                SampleRate;

            float sine =
                Mathf.Sin((float)phase);

            float square =
                sine >= 0.0f
                    ? 1.0f
                    : -1.0f;

            float envelope =
                Mathf.Pow(
                    1.0f - progress,
                    3.0f
                );

            float sample =
                (
                    sine * 0.75f +
                    square * 0.25f
                ) * envelope;

            samples[i] =
                Mathf.Round(sample * 12.0f) /
                12.0f;
        }

        AudioClip clip = AudioClip.Create(
            "Measure8Bit",
            sampleCount,
            1,
            SampleRate,
            false
        );

        clip.SetData(samples, 0);

        return clip;
    }
    public void PlayMeasure()
    {
        if (measureClip == null)
        {
            return;
        }

        measureSource.pitch = 1.0f;

        measureSource.PlayOneShot(
            measureClip,
            measureVolume
        );
    }

    private static void ConfigureSource(
        AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0.0f;
    }
}
