using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the playable clips behind the visual playlist. Assigned clips are used
/// first; an original procedural preview is generated only when the project
/// has no audio asset assigned for that row.
/// </summary>
public class PlaylistMusicController : MonoBehaviour
{
    private AudioClip[] tracks = Array.Empty<AudioClip>();
    private string[] trackTitles = Array.Empty<string>();

    public int CurrentTrackIndex { get; private set; } = -1;
    public event Action<int> TrackSelected;

    public void Configure(IList<AudioClip> assignedTracks, string[] titles)
    {
        trackTitles = titles ?? Array.Empty<string>();
        tracks = new AudioClip[trackTitles.Length];

        for (int i = 0; i < tracks.Length; i++)
        {
            AudioClip assignedClip = assignedTracks != null && i < assignedTracks.Count
                ? assignedTracks[i]
                : null;
            tracks[i] = assignedClip != null ? assignedClip : CreateOriginalPreview(trackTitles[i], i);
        }
    }

    public void PlayTrack(int index)
    {
        if (index < 0 || index >= tracks.Length || tracks[index] == null)
            return;

        StopAmbientLoop();

        AudioManager audioManager = GetOrCreateAudioManager();
        if (audioManager == null)
            return;

        audioManager.PlayBGM(tracks[index]);
        CurrentTrackIndex = index;
        TrackSelected?.Invoke(index);
    }

    private static AudioManager GetOrCreateAudioManager()
    {
        if (AudioManager.Instance != null)
            return AudioManager.Instance;

        AudioManager existing = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
        if (existing != null)
            return existing;

        GameObject managerObject = new GameObject("AudioManager");
        return managerObject.AddComponent<AudioManager>();
    }

    private static void StopAmbientLoop()
    {
        CozyAmbientAudio ambient = UnityEngine.Object.FindFirstObjectByType<CozyAmbientAudio>();
        if (ambient != null)
            ambient.StopAmbient();
    }

    private static AudioClip CreateOriginalPreview(string title, int variation)
    {
        const int sampleRate = 22050;
        const float durationSeconds = 12f;
        const int channels = 1;
        int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        // This is a simple original pad and pulse, deliberately not a melody
        // or recording of the commercial titles displayed by the mockup.
        float baseFrequency = 92.5f + variation * 8.25f;
        float pulseFrequency = 1.6f + variation * 0.08f;
        for (int sample = 0; sample < sampleCount; sample++)
        {
            float time = sample / (float)sampleRate;
            float pad = Mathf.Sin(2f * Mathf.PI * baseFrequency * time) * 0.13f;
            pad += Mathf.Sin(2f * Mathf.PI * baseFrequency * 1.4983f * time) * 0.07f;
            pad += Mathf.Sin(2f * Mathf.PI * baseFrequency * 2.0f * time) * 0.04f;

            float pulsePhase = Mathf.Repeat(time * pulseFrequency, 1f);
            float pulseEnvelope = Mathf.Exp(-pulsePhase * 13f);
            float pulse = Mathf.Sin(2f * Mathf.PI * (baseFrequency * 4f) * time) * pulseEnvelope * 0.07f;
            float wobble = Mathf.Sin(2f * Mathf.PI * 0.11f * time + variation) * 0.025f;

            samples[sample] = Mathf.Clamp(pad + pulse + wobble, -0.35f, 0.35f);
        }

        AudioClip clip = AudioClip.Create("Original preview - " + title, sampleCount, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}