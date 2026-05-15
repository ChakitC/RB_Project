using System.Linq;
using UnityEngine;

public class PartySelectionCommitter : MonoBehaviour, IGameSaveParty
{
    [Header("Root (default = this)")]
    [SerializeField] Transform slotRoot;

    [Header("Auto-detected")]
    [SerializeField] PartySlot[] slots;
    

    void OnValidate()
    {
        if (!slotRoot) slotRoot = transform;
        CacheSlots();
    }

    void CacheSlots()
    {
        if (!slotRoot) return;

        // หา PartySlot ใต้ลูกทั้งหมด แล้วเรียงตามลำดับลูกใน Hierarchy
        
        slots = slotRoot.GetComponentsInChildren<PartySlot>(true)
            .OrderBy(s => s.transform.GetSiblingIndex())
            .ToArray();
        // SaveManager.Instance.SaveParty(); PartySlot จะเป็นคน load เอง
    }

    public void OnSaveParty(PartyData data)
    {
        if (data == null)
        {
            Debug.LogError("[PartySelectionCommitter] OnSaveParty: data is null");
            return;
        }

        if (data.partyIds == null)
            data.partyIds = new System.Collections.Generic.List<string>();

        EnsureSize(data.partyIds, slots.Length);

        for (int i = 0; i < slots.Length; i++)
        {
            var id = (slots[i] && slots[i].Selected)
                ? slots[i].Selected.characterId
                : string.Empty;

            data.partyIds[i] = string.IsNullOrWhiteSpace(id) ? string.Empty : id;
        }

        
    }

    static void EnsureSize(System.Collections.Generic.List<string> list, int size)
    {
        if (list == null) return;

        while (list.Count < size) list.Add(string.Empty);
        if (list.Count > size) list.RemoveRange(size, list.Count - size);
    }
    
    public void OnLoadParty(PartyData data)
    {
    
    }
    
    
    
}
