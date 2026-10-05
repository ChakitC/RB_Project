using TMPro;
using UnityEditor;
using UnityEngine;

public static class PassiveHudPrefabBuilder
{
    public const string ViewPath = "Assets/Character/Roma/Passive/UI/RomaPassiveHUD.prefab";
    public const string DefinitionPath = "Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset";
    public const string HudPath = "Assets/Spain/UI/GameplayHUD.prefab";
    static readonly Color Ink = new(0.06f, 0.085f, 0.10f, 0.94f);
    static readonly Color White = new(0.86f, 0.94f, 0.96f);
    static readonly Color Track = new(0.19f, 0.25f, 0.28f);
    static readonly Color Mint = new(0.59f, 0.89f, 0.80f);

    [MenuItem("Tools/RB/Passives/Create Roma HUD")]
    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Character/Roma/Passive/UI"))
            AssetDatabase.CreateFolder("Assets/Character/Roma/Passive", "UI");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath) == null) CreateView();
        var viewRoot = PrefabUtility.LoadPrefabContents(ViewPath);
        try
        {
            bool changed = false;
            foreach (var ring in viewRoot.GetComponentsInChildren<PassiveGaugeArc>(true))
                if (ring.GetComponent<CanvasRenderer>() == null)
                { ring.gameObject.AddComponent<CanvasRenderer>(); changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(viewRoot, ViewPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(viewRoot); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);
        var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(DefinitionPath);
        definition.hudPrefab = prefab.GetComponent<PassiveHudView>();
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        var hud = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var existing = hud.transform.Find("PassiveHUD");
            if (existing == null)
                existing = hud.transform.Find("PlayerHpStamina/PassiveHUD");
            if (existing != null && existing.parent != hud.transform)
            {
                // Expose the existing spawn point directly in GameplayHUD without
                // moving its authored screen rectangle during the migration.
                var hostRect = (RectTransform)existing;
                var hudRect = (RectTransform)hud.transform;
                var corners = new Vector3[4]; hostRect.GetWorldCorners(corners);
                Vector2 bottomLeft = hudRect.InverseTransformPoint(corners[0]);
                Vector2 topRight = hudRect.InverseTransformPoint(corners[2]);
                hostRect.SetParent(hud.transform, false);
                hostRect.anchorMin = hostRect.anchorMax = hostRect.pivot = Vector2.zero;
                hostRect.localScale = Vector3.one;
                hostRect.localRotation = Quaternion.identity;
                hostRect.sizeDelta = topRight - bottomLeft;
                hostRect.anchoredPosition = bottomLeft - hudRect.rect.min;
            }
            if (existing == null)
            {
                var host = Rect("PassiveHUD", hud.transform, new Vector2(720, 176), Vector2.zero);
                host.anchorMin = host.anchorMax = host.pivot = Vector2.zero;
                host.anchoredPosition = new Vector2(713, 124);
                host.gameObject.AddComponent<PassiveHudHost>();
                var layout = host.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                layout.spacing = 12; layout.childAlignment = TextAnchor.LowerLeft;
                layout.childControlWidth = layout.childControlHeight = false;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            }
            PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        return ViewPath;
    }

    static void CreateView()
    {
        var root = Rect("RomaPassiveHUD", null, new Vector2(348, 176), Vector2.zero);
        try
        {
            var background = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Ink; background.raycastTarget = false;
            var view = root.gameObject.AddComponent<RomaPassiveHudView>();
            var line = Rect("Accent", root, new Vector2(3, 144), new Vector2(-172, 0));
            var image = line.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Mint; image.raycastTarget = false;
            Text("Title", root, "ROMA  /  MOMENTUM", new Vector2(324, 24), new Vector2(0, 66), 14, White, TextAlignmentOptions.Left);
            Arc("MomentumTrack", root, 112, 6, 5, Track);
            var momentum = Arc("Momentum", root, 112, 6, 5, Mint); momentum.SetFill(0);
            Arc("StaminaTrack", root, 91, 3, 1, Track);
            var stamina = Arc("Stamina", root, 91, 3, 1, White);
            // 70% clockwise from twelve o'clock. Presentation marker, no gameplay gate.
            float angle = 0.7f * Mathf.PI * 2;
            var marker = Rect("Stamina70Marker", root, new Vector2(2, 9),
                new Vector2(-105, -5) + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 45.5f);
            marker.localEulerAngles = new Vector3(0, 0, -252);
            var markerImage = marker.gameObject.AddComponent<UnityEngine.UI.Image>(); markerImage.color = new Color(1, 0.69f, 0.28f); markerImage.raycastTarget = false;
            var count = Text("MomentumValue", root, "0", new Vector2(78, 36), new Vector2(-105, 0), 28, White);
            Text("MomentumLabel", root, "MOMENTUM", new Vector2(80, 16), new Vector2(-105, -24), 9, Mint);
            var sta = Text("StaminaValue", root, "STA  -- / --", new Vector2(195, 22), new Vector2(65, 30), 13, White, TextAlignmentOptions.Left);
            var next = Text("NextAttack", root, "NEXT ATTACK  +0%", new Vector2(195, 25), new Vector2(65, 2), 14, Mint, TextAlignmentOptions.Left);
            var damage = Text("LowStaminaDamage", root, "LOW STAMINA BONUS  --", new Vector2(195, 22), new Vector2(65, -26), 10, White, TextAlignmentOptions.Left);
            var feedback = Text("Feedback", root, "", new Vector2(324, 22), new Vector2(0, -70), 12, Mint);
            var so = new SerializedObject(view);
            void Bind(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;
            Bind("staminaRing", stamina); Bind("momentumRing", momentum); Bind("momentumText", count);
            Bind("staminaText", sta); Bind("nextAttackText", next); Bind("damageText", damage); Bind("feedbackText", feedback);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, ViewPath);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position;
        return rect;
    }
    static PassiveGaugeArc Arc(string name, Transform parent, float size, float width, int segments, Color color)
    {
        var ring = Rect(name, parent, Vector2.one * size, new Vector2(-105, -5)).gameObject.AddComponent<PassiveGaugeArc>();
        ring.Configure(width, segments, segments > 1 ? 5 : 0); ring.color = color; return ring;
    }
    static TMP_Text Text(string name, Transform parent, string value, Vector2 size, Vector2 position, float fontSize, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset; label.fontSize = fontSize; label.color = color;
        label.text = value; label.alignment = align; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; return label;
    }
}
