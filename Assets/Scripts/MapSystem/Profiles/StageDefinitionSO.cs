using UnityEngine;

/// <summary>
/// A stage as the player meets it: the identity progress is saved under, the run config it launches,
/// and how it reads on the Basement board.
///
/// <see cref="StageId"/> is the save key and must never change once a build has shipped. When a
/// stage has to be renamed, add the old id to <see cref="LegacyStageIds"/> instead: saved progress
/// under the old key is then adopted rather than lost.
/// </summary>
[CreateAssetMenu(menuName = "Game/Map/Profiles/Stage Definition")]
public class StageDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("รหัสด่านที่ใช้เก็บ save progress ห้ามเปลี่ยนหลังปล่อยบิลด์แล้ว")]
    [SerializeField] private string stageId;

    [Tooltip("ชื่อด่านที่แสดงบนบอร์ด")]
    [SerializeField] private string displayName;

    [Tooltip("รหัสเดิมของด่านนี้ ใช้ย้าย save progress เมื่อเปลี่ยน Stage Id")]
    [SerializeField] private string[] legacyStageIds;

    [Header("Content")]
    [Tooltip("run config ที่ด่านนี้เปิดเมื่อผู้เล่นกดเลือก")]
    [SerializeField] private MapRunConfigSO runConfig;

    [Header("Board")]
    [Tooltip("ลำดับการแสดงบนบอร์ด น้อยกว่ามาก่อน")]
    [SerializeField] private int boardOrder;

    [Tooltip("ซ่อนจากบอร์ดโดยไม่ต้องลบออกจาก catalog")]
    [SerializeField] private bool hiddenOnBoard;

    public string StageId => stageId != null ? stageId.Trim() : string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? (runConfig != null ? runConfig.StageDisplayName : name)
        : displayName;

    public string[] LegacyStageIds => legacyStageIds;
    public MapRunConfigSO RunConfig => runConfig;
    public int BoardOrder => boardOrder;
    public bool HiddenOnBoard => hiddenOnBoard;

    /// <summary>
    /// The stage id to launch with. It comes from the run config when the definition leaves it
    /// blank, so a stage does not have to restate what its config already says.
    /// </summary>
    public string ResolveStageId()
    {
        string id = StageId;
        if (!string.IsNullOrEmpty(id))
            return id;

        return runConfig != null ? runConfig.StageId : string.Empty;
    }
}
