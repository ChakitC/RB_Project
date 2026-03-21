using UnityEngine;

public class AudioEmitter : MonoBehaviour
{
    [SerializeField] private AudioCue cue;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool stopOnDisable = true;
    [SerializeField] private bool followTransform = true;
    [SerializeField] private bool restartIfAlreadyPlaying = true;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private float volumeMultiplier = 1f;
    [SerializeField] private float pitchMultiplier = 1f;

    AudioHandle _handle;

    void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    void OnDisable()
    {
        if (stopOnDisable)
            _handle.Stop();
    }

    public AudioHandle Play()
    {
        if (cue == null)
            return default;

        if (restartIfAlreadyPlaying)
            _handle.Stop();

        _handle = followTransform
            ? AudioService.Instance.PlayAttached(cue, transform, positionOffset, volumeMultiplier, pitchMultiplier)
            : AudioService.Instance.PlayAtPosition(cue, transform.position + positionOffset, volumeMultiplier, pitchMultiplier);

        return _handle;
    }

    public void Stop()
    {
        _handle.Stop();
    }
}
