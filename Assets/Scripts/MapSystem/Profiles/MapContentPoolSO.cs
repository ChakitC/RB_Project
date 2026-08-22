using UnityEngine;

/// <summary>
/// The rooms and encounters a stage can draw from — a biome. Two stages set in the same place share
/// one pool instead of each listing the same room definitions.
///
/// A <see cref="MapRunConfigSO"/> with no pool assigned keeps reading its own legacy lists, so
/// existing assets are unaffected.
/// </summary>
[CreateAssetMenu(menuName = "Game/Map/Profiles/Map Content Pool")]
public class MapContentPoolSO : ScriptableObject
{
    [Tooltip("ชื่อ biome หรือชุด content สำหรับอ่านใน Inspector")]
    [SerializeField] private string displayName;

    [Tooltip("รายการ room definition ที่ generator ใช้เลือก prefab ห้องตามชนิด node")]
    [SerializeField] private RoomDefinitionSO[] roomDefinitions;

    [Tooltip("รายการ encounter definition ที่ generator ใช้เลือกศัตรูและ wave ตามชนิด node")]
    [SerializeField] private EncounterDefinitionSO[] encounterDefinitions;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public RoomDefinitionSO[] RoomDefinitions => roomDefinitions;
    public EncounterDefinitionSO[] EncounterDefinitions => encounterDefinitions;
}
