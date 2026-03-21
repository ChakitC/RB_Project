using UnityEngine;

public struct AudioHandle
{
    readonly AudioService _service;
    readonly int _instanceId;

    internal AudioHandle(AudioService service, int instanceId)
    {
        _service = service;
        _instanceId = instanceId;
    }

    public bool IsValid => _service != null && _service.IsHandleValid(_instanceId);
    public bool IsPlaying => _service != null && _service.IsHandlePlaying(_instanceId);

    public void Stop()
    {
        _service?.Stop(_instanceId);
    }

    public void SetVolumeMultiplier(float multiplier)
    {
        _service?.SetHandleVolumeMultiplier(_instanceId, multiplier);
    }

    public void SetPitchMultiplier(float multiplier)
    {
        _service?.SetHandlePitchMultiplier(_instanceId, multiplier);
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        _service?.SetHandleWorldPosition(_instanceId, worldPosition);
    }

    public void SetFollowTarget(Transform target)
    {
        _service?.SetHandleFollowTarget(_instanceId, target, Vector3.zero);
    }

    public void SetFollowTarget(Transform target, Vector3 offset)
    {
        _service?.SetHandleFollowTarget(_instanceId, target, offset);
    }

    public void ClearFollowTarget()
    {
        _service?.ClearHandleFollowTarget(_instanceId);
    }
}
