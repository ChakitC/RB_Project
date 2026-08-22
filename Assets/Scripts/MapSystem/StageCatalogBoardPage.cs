using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills a Basement board page from a <see cref="StageCatalogSO"/> instead of from hand-authored
/// placards, so adding a stage is an asset edit rather than a scene edit.
///
/// This is opt-in: a page without this component keeps whatever was authored into it, which is how
/// the existing pages still work. Put it on an empty page and it clones <see cref="placardTemplate"/>
/// once per catalog stage at <c>Awake</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageCatalogBoardPage : MonoBehaviour
{
    [Tooltip("catalog ที่ใช้สร้างป้ายด่านบนหน้านี้")]
    [SerializeField] private StageCatalogSO catalog;

    [Tooltip("ป้ายต้นแบบใต้หน้านี้ ต้องมี StagePlacardButton และถูกปิดไว้")]
    [SerializeField] private StagePlacardButton placardTemplate;

    [Tooltip("ตำแหน่ง anchor ของป้ายแต่ละใบ เรียงตามลำดับใน catalog")]
    [SerializeField] private Vector2[] placardAnchors =
    {
        new(0.20f, 0.52f),
        new(0.50f, 0.52f),
        new(0.80f, 0.52f),
    };

    private readonly List<GameObject> spawnedPlacards = new();

    void Awake()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        for (int i = 0; i < spawnedPlacards.Count; i++)
        {
            if (spawnedPlacards[i] != null)
                Destroy(spawnedPlacards[i]);
        }

        spawnedPlacards.Clear();

        if (catalog == null || placardTemplate == null)
            return;

        // The template is a prototype, never a live placard.
        placardTemplate.gameObject.SetActive(false);

        List<StageDefinitionSO> stages = catalog.GetBoardStages();
        int count = Mathf.Min(stages.Count, placardAnchors != null ? placardAnchors.Length : 0);
        if (stages.Count > count)
        {
            Debug.LogWarning(
                $"[StageCatalogBoardPage] '{catalog.name}' has {stages.Count} board stages but only " +
                $"{count} placard anchors. The rest are not shown.",
                this);
        }

        for (int i = 0; i < count; i++)
            spawnedPlacards.Add(CreatePlacard(stages[i], placardAnchors[i]));
    }

    GameObject CreatePlacard(StageDefinitionSO stage, Vector2 anchor)
    {
        StagePlacardButton placard = Instantiate(placardTemplate, transform);
        placard.name = stage.DisplayName;
        placard.gameObject.SetActive(true);
        placard.SetRunConfig(stage.RunConfig);

        var rect = (RectTransform)placard.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;

        TMP_Text label = placard.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = BuildLabel(stage);

        Button button = placard.GetComponent<Button>();
        if (button != null)
        {
            // The template's own onClick is authored; the runtime listener is what actually enters
            // the stage, so it is added per clone rather than baked into the prefab.
            button.onClick.AddListener(placard.EnterStage);
        }

        return placard.gameObject;
    }

    static string BuildLabel(StageDefinitionSO stage)
    {
        MapRunConfigSO config = stage.RunConfig;
        if (config == null)
            return stage.DisplayName;

        return $"{stage.DisplayName}\nLV.{config.StartLevel}–{config.TargetLevel}";
    }
}
