using UnityEngine;

public enum CoverHeight
{
    Low,   // ต้องก้ม
    High   // ยืนหลบได้
}

[DisallowMultipleComponent]
public class CoverPoint : MonoBehaviour
{
    [Header("ประเภทความสูงของกำบัง")]
    public CoverHeight height = CoverHeight.High;

    [Header("จุดที่ Player จะถูก snap มายืน")]
    public Transform snapPoint;

    [Tooltip("รัศมีคร่าว ๆ ที่ผู้เล่นสามารถกดใช้ coverSystem นี้ได้ (สำหรับ debug เฉย ๆ)")]
    public float useRadius = 1f;

    /// <summary>ทิศของกำแพง (ปกติใช้ transform.forward)</summary>
    public Vector3 CoverForward => transform.forward;

    /// <summary>ตำแหน่งที่ player จะถูก snap มา</summary>
    public Vector3 SnapPosition => snapPoint ? snapPoint.position : transform.position;

    private void OnDrawGizmos()
    {
        // สีตามความสูงกำบัง
        Gizmos.color = height == CoverHeight.Low ? Color.cyan : Color.green;

        // วาดจุด snap
        Vector3 pos = SnapPosition;
        Gizmos.DrawSphere(pos, 0.1f);

        // วาดทิศของกำแพง (forward)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos + CoverForward * 0.8f);

        // วาดรัศมีใช้งาน (ช่วยมองใน Scene)
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(pos, useRadius);
    }
}