using UnityEngine;

/// <summary>
/// ค่าจูนภาพตอน world slow ทั้งหมด ตัว <see cref="WorldSlowPostFx"/> ไม่เก็บค่าพวกนี้เองแล้ว
/// เพื่อไม่ให้มี source of truth สองทาง (asset กับ prefab) แบบเดียวกับ <see cref="DashSetting"/>
///
/// ไม่ได้อยู่ใน DashSetting เพราะภาพชุดนี้เกาะ world slow ทุกแหล่ง ไม่ใช่แค่ perfect dodge
/// สโลว์จาก cutscene ก็ใช้ค่าเดียวกันนี้
/// </summary>
[CreateAssetMenu(menuName = "Game/VFX/World Slow Post FX Setting", fileName = "WorldSlowPostFxSetting")]
public sealed class WorldSlowPostFxSetting : ScriptableObject
{
    [Header("ความมืดตอนสโลว์เต็ม")]
    [Tooltip("จอมืดลงกี่เปอร์เซ็นต์ 0 = ไม่มืดเลย, 100 = ดำสนิท ค่านี้คูณความสว่างทั้งจอ " +
             "VFX ที่เป็นสี HDR (เกิน 1) หารแล้วยังเกิน 1 อยู่ดีจึงยังขาวจ้าเหมือนเดิม")]
    [Range(0f, 100f)] public float dimPercent = 35f;

    [Tooltip("ลดความอิ่มสีของฉาก ให้ VFX สีจัดเด่นขึ้น 0 = ไม่เปลี่ยน")]
    [Range(-100f, 0f)] public float saturation = -25f;

    [Header("ขอบมืด (ไล่จากกลางจอออกขอบ)")]
    [Tooltip("ความเข้มของขอบมืดเป็นเปอร์เซ็นต์ ยิ่งสูงวงยิ่งกินเข้ามาใกล้กลางจอ")]
    [Range(0f, 100f)] public float vignettePercent = 55f;

    [Tooltip("ความนุ่มของการไล่ ยิ่งสูงยิ่งเบลอไม่เห็นขอบวง")]
    [Range(0.01f, 1f)] public float vignetteSmoothness = 0.6f;

    public Color vignetteColor = Color.black;

    [Tooltip("false = วงรีตามอัตราส่วนจอ (พอดีจอ), true = วงกลมจริงซึ่งจะล้นบน-ล่างบนจอ 16:9")]
    public bool vignetteRounded = false;

    [Tooltip("จุดศูนย์กลางของวง 0.5,0.5 = กลางจอ")]
    public Vector2 vignetteCenter = new Vector2(0.5f, 0.5f);

    [Header("การไล่ระดับความมืด")]
    [Tooltip("วินาทีที่ใช้ไล่ความมืดขึ้นจนสุด 0 = มืดทันที ค่านี้ไม่กระทบความเร็วเวลา เวลาช้าทันทีเสมอ")]
    [Min(0f)] public float dimFadeInTime = 0.12f;

    [Tooltip("วินาทีที่ใช้คลายความมืดตอนสโลว์จบ 0 = คลายตามหาง curve ของ slow เป๊ะ")]
    [Min(0f)] public float dimFadeOutTime = 0f;

    [Header("Bloom (ทำงานเฉพาะตอนสโลว์)")]
    [Tooltip("ความฟุ้งตอนสโลว์เต็ม 0 = ปิด base profile ของโปรเจกต์ตั้งไว้ 0 อยู่แล้วจึงไม่ฟุ้งตอนเล่นปกติ")]
    [Range(0f, 3f)] public float bloomIntensity = 0.7f;

    [Tooltip("ค่าความสว่างที่เริ่มฟุ้ง ยิ่งต่ำยิ่งฟุ้งลามไปโดนฉากด้วย ควรอยู่เหนือ 1 ถ้าอยากให้เฉพาะ HDR VFX ฟุ้ง")]
    [Range(0f, 3f)] public float bloomThreshold = 0.95f;

    [Range(0f, 1f)] public float bloomScatter = 0.75f;
    public Color bloomTint = Color.white;

    [Header("Volume")]
    [Tooltip("ต้องสูงกว่า Volume อื่นในฉาก ไม่งั้นจะโดนทับ")]
    public int volumePriority = 100;

    /// <summary>ตัวคูณความสว่างที่ได้จาก <see cref="dimPercent"/> (100% = 0 = ดำสนิท)</summary>
    public float DimMultiplier => 1f - Mathf.Clamp01(dimPercent * 0.01f);

    /// <summary><see cref="vignettePercent"/> ในหน่วยที่ URP ใช้ (0..1)</summary>
    public float VignetteIntensity01 => Mathf.Clamp01(vignettePercent * 0.01f);

#if UNITY_EDITOR
    /// <summary>
    /// ยิงตอนค่าใน Inspector ถูกแก้ ให้ <see cref="WorldSlowPostFx"/> ที่ใช้ asset นี้อยู่
    /// push ค่าใหม่เข้า Volume ทันที จะได้จูนเห็นผลสดๆ ระหว่าง Play Mode
    /// (ค่าอยู่บน asset ไม่ใช่ component จึงไม่โดนรีเซ็ตตอนออกจาก Play)
    /// </summary>
    public static event System.Action<WorldSlowPostFxSetting> Changed;

    void OnValidate()
    {
        if (Changed != null)
            Changed(this);
    }
#endif
}
