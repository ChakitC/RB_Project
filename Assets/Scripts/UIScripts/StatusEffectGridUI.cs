using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StatusEffectGridUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private StatusEffectController controller;
    [SerializeField] private StatusEffectCategory category = StatusEffectCategory.Buff;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private StatusEffectIconUI iconPrefab;

    [Header("Layout")]
    [SerializeField] private Vector2 cellSize = new Vector2(56f, 56f);
    [SerializeField] private Vector2 spacing = new Vector2(6f, 6f);
    [SerializeField] private int rowCount = 1;
    [SerializeField] private RectOffset padding = new RectOffset();
    [SerializeField] private bool autoSizeToContent = true;

    readonly List<StatusEffectIconUI> _icons = new();
    readonly List<StatusEffectInstance> _filteredEffects = new();
    readonly List<int> _filteredStackCounts = new();
    readonly Dictionary<object, int> _groupIndex = new();

    GridLayoutGroup _gridLayout;
    ContentSizeFitter _sizeFitter;
    bool _subscribed;

    void Reset()
    {
        contentRoot = transform as RectTransform;
        EnsureLayout();
    }

    void Awake()
    {
        EnsureLayout();
    }

    void OnEnable()
    {
        Subscribe();
        Rebuild();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnValidate()
    {
        EnsureLayout();

        if (Application.isPlaying)
            Rebuild();
    }

    public void Bind(StatusEffectController newController)
    {
        if (controller == newController)
        {
            Rebuild();
            return;
        }

        Unsubscribe();
        controller = newController;
        Subscribe();
        Rebuild();
    }

    public void SetCategory(StatusEffectCategory newCategory)
    {
        if (category == newCategory)
            return;

        category = newCategory;
        Rebuild();
    }

    void Subscribe()
    {
        if (_subscribed || controller == null || !isActiveAndEnabled)
            return;

        controller.EffectsChanged += HandleEffectsChanged;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || controller == null)
            return;

        controller.EffectsChanged -= HandleEffectsChanged;
        _subscribed = false;
    }

    void HandleEffectsChanged()
    {
        Rebuild();
    }

    void Rebuild()
    {
        if (!EnsureLayout())
            return;

        CollectEffects();

        for (int i = 0; i < _filteredEffects.Count; i++)
        {
            var icon = GetOrCreateIcon(i);
            if (!icon)
                continue;

            icon.gameObject.SetActive(true);
            icon.Bind(_filteredEffects[i], _filteredStackCounts[i]);
        }

        for (int i = _filteredEffects.Count; i < _icons.Count; i++)
        {
            if (!_icons[i])
                continue;

            _icons[i].Clear();
            _icons[i].gameObject.SetActive(false);
        }

        LayoutRebuilder.MarkLayoutForRebuild(contentRoot);
    }

    /// <summary>
    /// Group instance หลายตัวที่ effectId เดียวกันเป็นไอคอนเดียว (เกิดเมื่อ StatusEffectDef.separatePerSource เปิดและ
    /// มีหลายแหล่งลง effect เดียวกัน) — representative = instance ที่ TimeLeft เหลือนานสุด, stacks = ผลรวม.
    /// ค่ารวมของ modifiers (multiplicative) แสดงผ่าน RuntimeStatModifier ที่ StatsHub รวมอยู่แล้ว ไม่ต้องคำนวณซ้ำที่ UI.
    /// </summary>
    void CollectEffects()
    {
        _filteredEffects.Clear();
        _filteredStackCounts.Clear();
        _groupIndex.Clear();

        var activeEffects = controller != null ? controller.ActiveEffects : null;
        if (activeEffects == null)
            return;

        for (int i = 0; i < activeEffects.Count; i++)
        {
            var instance = activeEffects[i];
            var definition = instance?.Definition;
            if (definition == null || instance.CurrentStacks <= 0)
                continue;

            if (definition.category != category)
                continue;

            object groupKey = !string.IsNullOrWhiteSpace(definition.effectId)
                ? definition.effectId
                : (object)definition;

            if (!_groupIndex.TryGetValue(groupKey, out int index))
            {
                _groupIndex[groupKey] = _filteredEffects.Count;
                _filteredEffects.Add(instance);
                _filteredStackCounts.Add(instance.CurrentStacks);
                continue;
            }

            _filteredStackCounts[index] += instance.CurrentStacks;

            var current = _filteredEffects[index];
            bool candidateWins = instance.IsPermanent && !current.IsPermanent ||
                (!current.IsPermanent && instance.TimeLeft > current.TimeLeft);

            if (candidateWins)
                _filteredEffects[index] = instance;
        }
    }

    StatusEffectIconUI GetOrCreateIcon(int index)
    {
        while (_icons.Count <= index)
            _icons.Add(CreateIcon());

        if (_icons[index] == null)
            _icons[index] = CreateIcon();

        return _icons[index];
    }

    StatusEffectIconUI CreateIcon()
    {
        if (!contentRoot)
            return null;

        if (iconPrefab != null)
            return Instantiate(iconPrefab, contentRoot);

        var iconObject = new GameObject(
            $"{category}StatusIcon_{_icons.Count + 1}",
            typeof(RectTransform),
            typeof(StatusEffectIconUI));

        var rect = iconObject.GetComponent<RectTransform>();
        rect.SetParent(contentRoot, false);
        rect.localScale = Vector3.one;
        return iconObject.GetComponent<StatusEffectIconUI>();
    }

    bool EnsureLayout()
    {
        if (!contentRoot)
            contentRoot = transform as RectTransform;

        if (!contentRoot)
        {
            Debug.LogWarning($"{nameof(StatusEffectGridUI)} requires a RectTransform root.", this);
            return false;
        }

        if (!_gridLayout)
            _gridLayout = contentRoot.GetComponent<GridLayoutGroup>();

        if (!_gridLayout)
            _gridLayout = contentRoot.gameObject.AddComponent<GridLayoutGroup>();

        padding ??= new RectOffset();

        _gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        _gridLayout.childAlignment = TextAnchor.UpperLeft;
        _gridLayout.cellSize = cellSize;
        _gridLayout.spacing = spacing;
        _gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        _gridLayout.constraintCount = Mathf.Max(1, rowCount);
        _gridLayout.padding = padding;

        if (!_sizeFitter)
            _sizeFitter = contentRoot.GetComponent<ContentSizeFitter>();

        if (autoSizeToContent)
        {
            if (!_sizeFitter)
                _sizeFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();

            _sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else if (_sizeFitter)
        {
            _sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            _sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        return true;
    }
}
