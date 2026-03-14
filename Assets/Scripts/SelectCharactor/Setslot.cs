using UnityEngine;

public class SlotPoint : MonoBehaviour
{
    [Tooltip("ถ้าใน SlotPoint มีลูกอย่างอื่น (เช่น decal/marker) ให้ใช้ตัวนี้กรองว่าอะไรคือ 'ตัวละคร'")]
    public bool requireCharacterSelectable = true;

    Transform _current; // ตัวที่ถูกวางอยู่ปัจจุบัน (เช่น Roma)
    
    public void Place(GameObject newObj)
    {
        if (!newObj) return;

        // ใช้ root ของ newObj เป็นตัวแทนทั้งก้อน (กันกรณีสคริปต์/Collider อยู่ child)
        Transform newRoot = newObj.transform.root;

        // หา "ตัวเก่า" ที่อยู่ใน slot (กันโดนตัวเอง + กัน marker)
        Transform old = FindOldChild(excludeRoot: newRoot);

        if (old != null)
        {
            Destroy(old.gameObject); // ✅ ลบ Roma (ตัวเก่า)
        }

        // เอาตัวใหม่มาเป็นลูกของ SlotPoint
        newRoot.SetParent(transform, false);
        newRoot.localPosition = Vector3.zero;
        newRoot.localRotation = Quaternion.identity;
        newRoot.localScale = Vector3.one;

        _current = newRoot;
    }

    Transform FindOldChild(Transform excludeRoot)
    {
        foreach (Transform child in transform)
        {
            if (!child) continue;

            // กัน “ตัวเอง” (RomaSelect ที่กำลังจะวาง) และลูกๆของมัน
            if (child.root == excludeRoot) continue;

            if (requireCharacterSelectable)
            {
                // กรองว่าอันนี้เป็น "ตัวละคร" จริงไหม
                if (child.GetComponentInChildren<CharacterSelectable>() == null)
                    continue;
            }

            return child; // เจอของเก่าแล้ว
        }
        return null;
    }
}