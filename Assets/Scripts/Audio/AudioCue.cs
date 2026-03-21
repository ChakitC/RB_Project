using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioCue", menuName = "Audio/Audio Cue")]
public sealed class AudioCue : ScriptableObject
{
    [Serializable]
    public struct Variation
    {
        public AudioClip clip;
        [Min(0f)] public float weight;
        [Min(0f)] public float volumeMultiplier;
        [Min(0.01f)] public float pitchMultiplier;
    }

    [Header("Identity")]
    public string cueId;
    public AudioCategory category = AudioCategory.Sfx;

    [Header("Playback")]
    public bool loop;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    public bool followTarget;
    [Range(-1f, 1f)] public float stereoPan;
    [Range(0, 256)] public int priority = 128;
    [Min(0f)] public float minDistance = 1f;
    [Min(0f)] public float maxDistance = 25f;
    [Min(0f)] public float dopplerLevel = 1f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Header("Variation")]
    [Min(0f)] public float baseVolume = 1f;
    [Min(0.01f)] public float basePitch = 1f;
    public Vector2 randomVolumeMultiplier = Vector2.one;
    public Vector2 randomPitchMultiplier = Vector2.one;
    public bool avoidImmediateRepeats = true;
    public List<Variation> variations = new();

    [Header("Playback Limits")]
    [Min(0f)] public float cooldown;
    [Min(0)] public int maxInstances;
    public bool stopOldestInstanceWhenLimitReached;

    public bool HasAnyClip
    {
        get
        {
            if (variations == null || variations.Count == 0)
                return false;

            for (int i = 0; i < variations.Count; i++)
            {
                if (variations[i].clip != null)
                    return true;
            }

            return false;
        }
    }
}
