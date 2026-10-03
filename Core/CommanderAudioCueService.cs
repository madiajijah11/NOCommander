using System;
using UnityEngine;

namespace NuclearOptionCommander;

internal enum CommanderAudioCueType
{
    OrderAck,
    OrderDenied,
    UnitLost,
    UnitDamaged,
    ObjectiveAlert,
    AirDispatched,
    RadioBurst,
    PingPinpoint
}

/// <summary>
/// Provides crisp tactical UI and battlefield audio cues without external assets.
/// Synthesizes distinct military radio clicks, chirps, and tones procedurally and
/// leverages native GameAssets / SoundManager when available.
/// </summary>
internal sealed class CommanderAudioCueService
{
    internal static CommanderAudioCueService? Instance { get; private set; }

    private AudioClip? ackClip;
    private AudioClip? deniedClip;
    private AudioClip? lostClip;
    private AudioClip? alertClip;
    private AudioClip? radioBurstClip;
    private AudioClip? pinpointClip;

    private AudioSource? customAudioSource;
    private float lastAudioTime;
    private const float MinAudioSpacingSeconds = 0.08f;

    internal CommanderAudioCueService()
    {
        Instance = this;
        InitializeProceduralClips();
    }

    private void InitializeProceduralClips()
    {
        try
        {
            ackClip = SynthesizeTone("NOCommander_Ack", 960f, 0.05f, 0.35f, toneType: 1); // 960Hz crisp blip
            deniedClip = SynthesizeTone("NOCommander_Denied", 220f, 0.12f, 0.45f, toneType: 2); // Low frequency descending buzz
            lostClip = SynthesizeTone("NOCommander_Lost", 320f, 0.25f, 0.5f, toneType: 3); // Two-tone minor lament
            alertClip = SynthesizeTone("NOCommander_Alert", 1400f, 0.18f, 0.45f, toneType: 4); // Urgent warble
            radioBurstClip = SynthesizeRadioStaticBurst("NOCommander_RadioBurst", 0.06f, 0.25f); // Military squelch/burst
            pinpointClip = SynthesizeTone("NOCommander_Pinpoint", 1800f, 0.07f, 0.35f, toneType: 0); // High ping
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NOCommander] Failed to initialize procedural audio clips: {ex.Message}");
        }
    }

    internal void PlayCue(CommanderAudioCueType cueType)
    {
        if (!CommanderSettings.TacticalAudioEnabled) return;
        if (Time.unscaledTime - lastAudioTime < MinAudioSpacingSeconds) return;

        lastAudioTime = Time.unscaledTime;
        float masterVol = CommanderSettings.TacticalAudioVolume;

        switch (cueType)
        {
            case CommanderAudioCueType.OrderAck:
                PlayClip(ackClip, 0.45f * masterVol);
                break;

            case CommanderAudioCueType.OrderDenied:
                if (GameAssets.i != null && GameAssets.i.errorTone != null)
                {
                    PlayNative(GameAssets.i.errorTone, 0.5f * masterVol);
                }
                else
                {
                    PlayClip(deniedClip, 0.5f * masterVol);
                }
                break;

            case CommanderAudioCueType.UnitLost:
                if (GameAssets.i != null && GameAssets.i.deathSound != null)
                {
                    PlayNative(GameAssets.i.deathSound, 0.65f * masterVol);
                }
                else
                {
                    PlayClip(lostClip, 0.6f * masterVol);
                }
                break;

            case CommanderAudioCueType.UnitDamaged:
                PlayClip(deniedClip, 0.4f * masterVol);
                break;

            case CommanderAudioCueType.ObjectiveAlert:
                PlayClip(alertClip, 0.6f * masterVol);
                break;

            case CommanderAudioCueType.AirDispatched:
                PlayClip(ackClip, 0.45f * masterVol);
                break;

            case CommanderAudioCueType.RadioBurst:
                if (GameAssets.i != null && GameAssets.i.radioStatic != null)
                {
                    PlayNative(GameAssets.i.radioStatic, 0.3f * masterVol);
                }
                else
                {
                    PlayClip(radioBurstClip, 0.3f * masterVol);
                }
                break;

            case CommanderAudioCueType.PingPinpoint:
                PlayClip(pinpointClip, 0.5f * masterVol);
                break;
        }
    }

    private void PlayNative(AudioClip clip, float volume)
    {
        try
        {
            SoundManager.PlayInterfaceOneShot(clip);
        }
        catch
        {
            PlayClip(clip, volume);
        }
    }

    private void PlayClip(AudioClip? clip, float volume)
    {
        if (clip == null) return;

        EnsureAudioSource();
        if (customAudioSource != null)
        {
            customAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }

    private void EnsureAudioSource()
    {
        if (customAudioSource == null)
        {
            GameObject hostGo = new GameObject("NOCommander_AudioHost");
            UnityEngine.Object.DontDestroyOnLoad(hostGo);
            customAudioSource = hostGo.AddComponent<AudioSource>();
            customAudioSource.playOnAwake = false;
            customAudioSource.spatialBlend = 0f; // 2D flat stereo interface sound
            customAudioSource.bypassEffects = true;
            customAudioSource.bypassListenerEffects = true;
        }
    }

    internal void ResetSession()
    {
        lastAudioTime = 0f;
    }

    #region Procedural Audio Synthesis

    private static AudioClip SynthesizeTone(string name, float baseFreq, float duration, float amp, int toneType)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, (int)(sampleRate * duration));
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float normTime = (float)i / sampleCount;
            float timeSec = (float)i / sampleRate;
            float freq = baseFreq;
            float env = 1f;

            switch (toneType)
            {
                case 0: // Simple crisp blip
                    env = Mathf.Pow(1f - normTime, 1.5f);
                    break;

                case 1: // Order Ack: Quick two-step frequency chirp up
                    freq = normTime < 0.4f ? baseFreq : baseFreq * 1.33f;
                    env = normTime < 0.8f ? 1f : (1f - normTime) * 5f;
                    break;

                case 2: // Order Denied: Low descending drone
                    freq = Mathf.Lerp(baseFreq, baseFreq * 0.65f, normTime);
                    env = 1f - normTime;
                    break;

                case 3: // Unit Lost: Low minor chord lament (step down)
                    freq = normTime < 0.5f ? baseFreq : baseFreq * 0.75f;
                    env = Mathf.Cos(normTime * Mathf.PI * 0.5f);
                    break;

                case 4: // Alert: Urgency pulse
                    freq = baseFreq + Mathf.Sin(normTime * Mathf.PI * 16f) * 200f;
                    env = Mathf.Sin(normTime * Mathf.PI);
                    break;
            }

            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * timeSec) * env * amp;
        }

        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeRadioStaticBurst(string name, float duration, float amp)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, (int)(sampleRate * duration));
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];
        System.Random rnd = new System.Random(1337);

        for (int i = 0; i < sampleCount; i++)
        {
            float normTime = (float)i / sampleCount;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float env = Mathf.Sin(normTime * Mathf.PI); // Smooth attack and release envelope
            samples[i] = noise * env * amp;
        }

        clip.SetData(samples, 0);
        return clip;
    }

    #endregion
}
