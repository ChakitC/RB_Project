using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum MorphChangeMode
{
    AnimationOnly,
    ModelOnly,
    Both
}

[HideMonoScript]
public sealed class MorphSkillPayloadDef : SkillPayloadDef
{
    // โครงสร้างเดียวกับ ApplyStatusSkillPayloadDef.StatusApplication (decouple ไว้ในไฟล์ตัวเอง)
    [System.Serializable]
    public sealed class MorphStatusApplication
    {
        [AssetsOnly, Required, InlineEditor, LabelText("Status Effect")]
        public StatusEffectDef effect;

        [Min(1), LabelText("Stacks")]
        public int stacks = 1;
    }

    [SerializeField, BoxGroup("Morph")]
    [LabelText("Change Mode")]
    private MorphChangeMode changeMode = MorphChangeMode.Both;

    [SerializeField, BoxGroup("Morph"), Min(0f)]
    [LabelText("Duration"), SuffixLabel("s")]
    private float duration = 8f;

    [SerializeField, BoxGroup("Morph/Model"), ShowIf("@changeMode != MorphChangeMode.AnimationOnly")]
    [AssetsOnly, LabelText("Morph Model Prefab")]
    private GameObject morphModelPrefab;

    [SerializeField, BoxGroup("Morph/Model"), ShowIf("@changeMode != MorphChangeMode.AnimationOnly")]
    [LabelText("Controller (optional)")]
    private RuntimeAnimatorController morphController;

    [SerializeField, BoxGroup("Morph/Model"), ShowIf("@changeMode != MorphChangeMode.AnimationOnly")]
    [LabelText("Avatar (optional)")]
    private Avatar morphAvatar;

    [SerializeField, BoxGroup("Morph/Animation"), ShowIf("@changeMode != MorphChangeMode.ModelOnly")]
    [LabelText("Morph Anim Profile")]
    private CharacterAnimProfileSO morphAnimProfile;

    [SerializeField, BoxGroup("Morph/Status"), LabelText("Status Effects (while morphed)")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    private List<MorphStatusApplication> statusApplications = new();

    public MorphChangeMode ChangeMode => changeMode;
    public float Duration => duration;
    public GameObject MorphModelPrefab => morphModelPrefab;
    public RuntimeAnimatorController MorphController => morphController;
    public Avatar MorphAvatar => morphAvatar;
    public CharacterAnimProfileSO MorphAnimProfile => morphAnimProfile;
    public IReadOnlyList<MorphStatusApplication> StatusApplications => statusApplications;

    public bool ChangesModel => changeMode != MorphChangeMode.AnimationOnly;
    public bool ChangesAnimation => changeMode != MorphChangeMode.ModelOnly;

    public bool ChangesStatus
    {
        get
        {
            if (statusApplications == null)
                return false;

            for (int i = 0; i < statusApplications.Count; i++)
                if (statusApplications[i] != null && statusApplications[i].effect != null)
                    return true;

            return false;
        }
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (duration <= 0f)
            issues.Add("Morph payload has no duration configured.");

        if (ChangesModel && morphModelPrefab == null)
            issues.Add("Morph payload changes Model but Morph Model Prefab is missing.");

        if (ChangesAnimation && morphAnimProfile == null)
            issues.Add("Morph payload changes Animation but Morph Anim Profile is missing.");

        if (!ChangesModel && !ChangesAnimation && !ChangesStatus)
            issues.Add("Morph payload does nothing: configure a visual change and/or at least one status effect.");

        if (statusApplications != null)
            for (int i = 0; i < statusApplications.Count; i++)
                if (statusApplications[i] != null && statusApplications[i].effect == null)
                    issues.Add($"Morph payload status entry #{i} has no Status Effect assigned.");
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null || context.CasterObject == null)
            return;

        GameObject host = new GameObject("MorphSkillRuntime");
        host.transform.SetParent(null);

        MorphSkillRuntime runtime = host.AddComponent<MorphSkillRuntime>();
        runtime.Initialize(context, this);
    }
}
