using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// วาดเอฟเฟคจอตอน world slow ตาม profile ที่ "คนสั่งสโลว์" ส่งมาให้
/// (<see cref="TimeSlowManager.ActiveVisual"/>) ถ้าไม่มีใครส่ง profile มา = ไม่ทำอะไรเลย
///
/// ตัวนี้ไม่ตัดสินใจเองว่าสโลว์ไหนควรมีเอฟเฟค เพราะเจตนาไม่เหมือนกัน
///   perfect dodge ของผู้เล่น -> อยากได้จอมืด
///   คัตซีน                    -> แค่หยุดเวลารอคัตซีนจบ ไม่อยากให้จอมืด
///   AI ที่ dash               -> ไม่ควรไปทำให้จอผู้เล่นมืด
///
/// ใช้ Volume ที่สร้างตอนรันไทม์ซ้อนทับ profile เดิมของโปรเจกต์ (weight = ความเข้มสโลว์)
/// จึงไม่แตะ DefaultVolumeProfile และไม่ต้องเพิ่มกล้อง/เลเยอร์
///
/// VFX ยังสว่างอยู่เพราะความมืดเป็นตัวคูณความสว่างทั้งจอ แต่ VFX เป็นสี HDR (ค่า > 1)
/// คูณแล้วยังเกิน 1 อยู่ดีเลย clip เป็นขาวเหมือนเดิม ส่วนฉากที่ค่าราวๆ 1 จะมืดลงชัดเจน
///
/// ความมืดไม่ได้เท่ากับ SlowBlend01 ตรงๆ แต่ไล่เข้าหามันด้วย dimFadeInTime/dimFadeOutTime
/// เพื่อให้ "เวลาช้าทันที แต่จอค่อยๆ มืด" ได้ โดยไม่ต้องไปแก้ curve ซึ่งจะทำให้เวลาช้าแบบค่อยเป็นค่อยไปตามไปด้วย
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldSlowPostFx : MonoBehaviour
{
    Volume _volume;
    VolumeProfile _profile;
    ColorAdjustments _colorAdjustments;
    Vignette _vignette;
    Bloom _bloom;

    // ถือ profile ที่ใช้อยู่ต่อจนกว่าจะ fade กลับจนสุด ไม่งั้นพอสโลว์จบจอจะดีดกลับทันทีแทนที่จะค่อยๆ คลาย
    WorldSlowPostFxSetting _activeVisual;
    float _appliedWeight = -1f;

    void OnEnable()
    {
        EnsureVolume();
        ApplyWeight(0f);
#if UNITY_EDITOR
        WorldSlowPostFxSetting.Changed += OnSettingChanged;
#endif
    }

    void OnDisable()
    {
        ApplyWeight(0f);
        _activeVisual = null;
#if UNITY_EDITOR
        WorldSlowPostFxSetting.Changed -= OnSettingChanged;
#endif
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
        var manager = TimeSlowManager.Instance;
        WorldSlowPostFxSetting requested = manager.ActiveVisual;

        if (requested != null && requested != _activeVisual)
        {
            _activeVisual = requested;
            PushTuning();
        }

        // ไม่มีใครขอเอฟเฟค = เป้าหมายคือ 0 แต่ยังใช้ profile เดิมไล่ลงมาให้จบก่อน
        float target = requested != null ? manager.SlowBlend01 : 0f;
        float current = _appliedWeight < 0f ? 0f : _appliedWeight;

        if (_activeVisual != null)
        {
            // ใช้ unscaled ให้ตรงกับ TimeSlowManager ที่นับ _elapsed ด้วย unscaledDeltaTime เหมือนกัน
            float dt = Time.unscaledDeltaTime;

            if (target > current && _activeVisual.dimFadeInTime > 0f)
                current = Mathf.MoveTowards(current, target, dt / _activeVisual.dimFadeInTime);
            else if (target < current && _activeVisual.dimFadeOutTime > 0f)
                current = Mathf.MoveTowards(current, target, dt / _activeVisual.dimFadeOutTime);
            else
                current = target;
        }
        else
        {
            current = target;
        }

        if (current != _appliedWeight)
            ApplyWeight(current);

        // คลายจนสุดแล้วค่อยปล่อย profile ทิ้ง
        if (_activeVisual != null && requested == null && _appliedWeight <= 0f)
            _activeVisual = null;
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
            return;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.name = "WorldSlowPostFx Profile (Runtime)";
        _profile.hideFlags = HideFlags.HideAndDontSave;

        _colorAdjustments = _profile.Add<ColorAdjustments>(true);
        _vignette = _profile.Add<Vignette>(true);
        _bloom = _profile.Add<Bloom>(true);

        if (!TryGetComponent(out _volume))
            _volume = gameObject.AddComponent<Volume>();

        _volume.isGlobal = true;
        _volume.profile = _profile;
        _volume.weight = 0f;
    }

    void PushTuning()
    {
        if (_activeVisual == null || _volume == null)
            return;

        if (_colorAdjustments != null)
        {
            // colorFilter คือตัวคูณความสว่าง จึงแปลงเปอร์เซ็นต์ความมืดมาตรงๆ ได้
            // 35% -> คูณ 0.65 ทั้งจอ ส่วน HDR VFX ที่ 8.0 เหลือ 5.2 ซึ่งยัง clip ขาวอยู่
            float k = _activeVisual.DimMultiplier;
            _colorAdjustments.colorFilter.overrideState = true;
            _colorAdjustments.colorFilter.value = new Color(k, k, k, 1f);
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.saturation.value = _activeVisual.saturation;
        }

        if (_vignette != null)
        {
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = _activeVisual.VignetteIntensity01;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = _activeVisual.vignetteSmoothness;
            _vignette.color.overrideState = true;
            _vignette.color.value = _activeVisual.vignetteColor;
            _vignette.rounded.overrideState = true;
            _vignette.rounded.value = _activeVisual.vignetteRounded;
            _vignette.center.overrideState = true;
            _vignette.center.value = _activeVisual.vignetteCenter;
        }

        if (_bloom != null)
        {
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = _activeVisual.bloomIntensity;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = _activeVisual.bloomThreshold;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = _activeVisual.bloomScatter;
            _bloom.tint.overrideState = true;
            _bloom.tint.value = _activeVisual.bloomTint;
        }

        _volume.priority = _activeVisual.volumePriority;
    }

#if UNITY_EDITOR
    // แก้ค่าบน asset -> เห็นผลทันทีโดยไม่ต้องออกจาก Play Mode
    void OnSettingChanged(WorldSlowPostFxSetting changed)
    {
        if (changed == _activeVisual)
            PushTuning();
    }
#endif
}
