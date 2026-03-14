// CoverConfig.cs
using UnityEngine;

[CreateAssetMenu(menuName="Game/CoverConfig")]
public class CoverConfig : ScriptableObject
{
    [Header("Search & Snap")]
    public float searchRadius = 6f;            // ระยะค้นหา node รอบตัว
    public float autoEnterAngle = 40f;         // มุมที่ถือว่าเราหันเข้าหา coverSystem
    public float snapDistance = 1.2f;          // ระยะที่เริ่มดูดเข้ากำบัง
    public float snapSpeed = 10f;              // ความเร็ว Lerp เข้าจุด

    [Header("Slide & Lean")]
    public float slideSpeed = 4.5f;            // ความเร็วเลื่อนตามขอบ
    public float leanOffset = 0.35f;           // ระยะโผล่ด้านซ้าย/ขวา
    public float leanCheckRadius = 0.2f;       // ตรวจชนขณะโผล่
    public float leanCooldown = 0.15f;

    [Header("Vault & Exit")]
    public float vaultHeightThreshold = 1.1f;
    public float vaultForward = 1.2f;
    public float exitBackStep = 0.6f;

    [Header("Exposure (damage in %)")]
    public float exposureHidden = 0.35f;       // หลัง coverSystem
    public float exposurePeek = 0.9f;          // โผล่ยิง
    public float exposureRunning = 1.0f;       // นอก coverSystem

    [Header("LOS")]
    public LayerMask losMask;                  // ชั้นวัตถุที่บังสายตา
}