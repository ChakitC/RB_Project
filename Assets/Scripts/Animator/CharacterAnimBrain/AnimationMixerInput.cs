using UnityEngine;

internal static class AnimationMixerInput
{
    internal static Vector2 Smooth(Vector2 current, Vector2 direction, float speed, bool snap,
        float sharpness, float deltaTime)
    {
        if (snap && direction.sqrMagnitude >= 0.0001f)
        {
            float step = Mathf.PI * 0.25f;
            float angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) / step) * step;
            direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        else if (snap) direction = Vector2.zero;
        return Vector2.Lerp(current, direction * Mathf.Clamp01(speed), 1f - Mathf.Exp(-sharpness * deltaTime));
    }
}
