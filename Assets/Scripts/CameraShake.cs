using UnityEngine;

// Trauma-based camera shake.
// Place this component on a child "ShakeNode" that is a child of the camera rig
// so it applies localPosition / localEulerAngles without fighting the rig's world position.
//
// Public API:
//   CameraShake.AddTrauma(float)  — add 0-1 trauma; values accumulate and decay automatically.
//
// Not yet connected to gameplay systems — ready to wire up later.
public class CameraShake : MonoBehaviour
{
    [SerializeField] float maxPositionOffset = 0.3f;
    [SerializeField] float maxRotationDeg = 2f;
    [SerializeField] float traumaDecayPerSecond = 1.5f;
    [SerializeField] float noiseSpeed = 8f;

    float _trauma;
    float _noiseOffset;

    void Awake()
    {
        _noiseOffset = Random.Range(0f, 100f);
    }

    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount);
    }

    void LateUpdate()
    {
        float shake = _trauma * _trauma;

        if (shake > 0.0001f)
        {
            float t = Time.time * noiseSpeed + _noiseOffset;
            float px = Mathf.PerlinNoise(t, 0f) * 2f - 1f;
            float py = Mathf.PerlinNoise(0f, t) * 2f - 1f;
            float rz = Mathf.PerlinNoise(t + 53f, t + 53f) * 2f - 1f;

            transform.localPosition = new Vector3(px, py, 0f) * (maxPositionOffset * shake);
            transform.localEulerAngles = new Vector3(0f, 0f, rz * maxRotationDeg * shake);
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localEulerAngles = Vector3.zero;
        }

        _trauma = Mathf.MoveTowards(_trauma, 0f, traumaDecayPerSecond * Time.deltaTime);
    }
}
