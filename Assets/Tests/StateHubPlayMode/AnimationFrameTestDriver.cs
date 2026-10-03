using System;
using UnityEngine;

// Writes movement before the production Driver and observes the result after Brain.Update.
[DefaultExecutionOrder(900)]
public sealed class AnimationFrameTestDriver : MonoBehaviour
{
    public Action BeforeUpdate;
    public Action AfterLateUpdate;
    private void Update() => BeforeUpdate?.Invoke();
    private void LateUpdate() => AfterLateUpdate?.Invoke();
}
