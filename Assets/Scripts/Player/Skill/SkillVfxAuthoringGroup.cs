using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillVfxAuthoringGroup : MonoBehaviour
{
    [SerializeField] private string entryId;

    public string EntryId => entryId;

    public void Configure(string id)
    {
        entryId = id ?? string.Empty;
        gameObject.name = $"VFX_{(string.IsNullOrEmpty(id) ? "Main" : char.ToUpper(id[0]) + id.Substring(1))}";
    }
}
