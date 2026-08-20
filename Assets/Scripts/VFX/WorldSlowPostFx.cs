using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// หรี่จอตอน world slow โดยเกาะ <see cref="TimeSlowManager.SlowBlend01"/> ตรงๆ
/// ไม่ได้ผูกกับ perfect dodge โดยเฉพาะ ดังนั้นสโลว์จาก cutscene ก็ได้ภาพนี้ด้วย
///
/// ใช้ Volume ที่สร้างตอนรันไทม์ซ้อนทับ profile เดิมของโปรเจกต์ (weight = ความเข้มสโลว์)
/// จึงไม่แตะ DefaultVolumeProfile และไม่ต้องเพิ่มกล้อง/เลเยอร์
///
/// VFX ยังสว่างอยู่เพราะ post exposure หรี่ทุกอย่างเป็นสัดส่วน แต่ VFX เป็นสี HDR (ค่า > 1)
/// หรี่แล้วยังเกิน 1 อยู่ดีเลย clip เป็นขาวเหมือนเดิม ส่วนฉากที่ค่าราวๆ 1 จะมืดลงชัดเจน
///
/// Bloom อยู่ใน Volume ตัวนี้ด้วย ไม่ได้ไปดันค่าใน DefaultVolumeProfile
/// จึงฟุ้งเฉพาะตอนสโลว์ และตอน weight = 0 ค่า intensity จะเป็น 0 ทำให้ URP ข้าม bloom pass ไปเลย
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldSlowPostFx : MonoBehaviour
{
    [Header("ค่าที่ระดับสโลว์เต็ม")]
    [Tooltip("EV ที่หรี่ลง ยิ่งติดลบยิ่งมืด -1.2 ประมาณครึ่งสตอป")]
    [SerializeField, Range(-4f, 0f)] private float postExposure = -1.2f;

    [Tooltip("ลดความอิ่มสีของฉาก ให้ VFX สีจัดเด่นขึ้น")]
    [SerializeField, Range(-100f, 0f)] private float saturation = -25f;

    [Tooltip("ความเข้มขอบมืด ไม่ควรเกิน 0.4 เพราะช่วงนี้ผู้เล่นกำลังเล็งยิงสวน")]
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.35f;

    [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.45f;
    [SerializeField] private Color vignetteColor = Color.black;

    [Header("Bloom (ทำงานเฉพาะตอนสโลว์)")]
    [Tooltip("ความฟุ้งตอนสโลว์เต็ม 0 = ปิด base profile ของโปรเจกต์ตั้งไว้ 0 อยู่แล้วจึงไม่ฟุ้งตอนเล่นปกติ")]
    [SerializeField, Range(0f, 3f)] private float bloomIntensity = 0.7f;

    [Tooltip("ค่าความสว่างที่เริ่มฟุ้ง ยิ่งต่ำยิ่งฟุ้งลามไปโดนฉากด้วย ควรอยู่เหนือ 1 ถ้าอยากให้เฉพาะ HDR VFX ฟุ้ง")]
    [SerializeField, Range(0f, 3f)] private float bloomThreshold = 0.95f;

    [SerializeField, Range(0f, 1f)] private float bloomScatter = 0.75f;
    [SerializeField] private Color bloomTint = Color.white;

    [Header("Volume")]
    [Tooltip("ต้องสูงกว่า Volume อื่นในฉาก ไม่งั้นจะโดนทับ")]
    [SerializeField] private int volumePriority = 100;

    Volume _volume;
    VolumeProfile _profile;
    ColorAdjustments _colorAdjustments;
    Vignette _vignette;
    Bloom _bloom;
    float _appliedWeight = -1f;

    void OnEnable()
    {
        EnsureVolume();
        ApplyWeight(0f);
    }

    void OnDisable()
    {
        ApplyWeight(0f);
    }

    void OnDestroy()
    {
        if (_profile == null)
            return;

        if (Application.isPlaying)
            Destroy(_profile);
        else
            DestroyImmediate(_profile);

        _profile = null;
    }

    void LateUpdate()
    {
        float blend = TimeSlowManager.Instance.SlowBlend01;

        // ปกติทั้งเกมจะอยู่ที่ 0 ตลอด ไม่ต้องเขียน Volume ซ้ำทุกเฟรม
        if (blend == _appliedWeight)
            return;

        ApplyWeight(blend);
    }

    void ApplyWeight(float weight)
    {
        if (_volume == null)
            return;

        _volume.weight = Mathf.Clamp01(weight);
        _appliedWeight = weight;
    }

    void EnsureVolume()
    {
        if (_volume != null)
        {
            PushTuning();
            return;
        }

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.name = "WorldSlowPostFx Profile (Runtime)";
        _profile.hideFlags = HideFlags.HideAndDontSave;

        _colorAdjustments = _profile.Add<ColorAdjustments>(true);
        _vignette = _profile.Add<Vignette>(true);
        _bloom = _profile.Add<Bloom>(true);

        if (!TryGetComponent(out _volume))
            _volume = gameObject.AddComponent<Volume>();

        _volume.isGlobal = true;
        _volume.priority = volumePriority;
        _volume.profile = _profile;
        _volume.weight = 0f;

        PushTuning();
    }

    void PushTuning()
    {
        if (_colorAdjustments != null)
        {
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = postExposure;
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.saturation.value = saturation;
        }

        if (_vignette != null)
        {
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = vignetteIntensity;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = vignetteSmoothness;
            _vignette.color.overrideState = true;
            _vignette.color.value = vignetteColor;
        }

        if (_bloom != null)
        {
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = bloomIntensity;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = bloomThreshold;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = bloomScatter;
            _bloom.tint.overrideState = true;
            _bloom.tint.value = bloomTint;
        }

        if (_volume != null)
            _volume.priority = volumePriority;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_volume != null)
            PushTuning();
    }
#endif
}
