using UnityEngine;

public struct AudioPlaybackRequest
{
    public AudioCue cue;
    public Vector3 worldPosition;
    public bool hasWorldPosition;
    public Transform followTarget;
    public Vector3 followOffset;
    public bool forceFollowTarget;
    public float volumeMultiplier;
    public float pitchMultiplier;
    public bool overrideLoop;
    public bool loop;

    public static AudioPlaybackRequest Create(AudioCue cue)
    {
        return new AudioPlaybackRequest
        {
            cue = cue,
            volumeMultiplier = 1f,
            pitchMultiplier = 1f
        };
    }
}
