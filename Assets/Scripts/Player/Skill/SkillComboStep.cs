using System;
using UnityEngine;

[Serializable]
public struct SkillComboStep
{
    public SkillGemDefinition executionSkill;
    [SerializeField, HideInInspector] string entryId;
    public Vector2 chainWindowN;
    public bool dropBufferOnWindowExpire;
    public string EntryId => entryId;

    public SkillComboStep(SkillGemDefinition skill, string id, Vector2 chainWindow, bool dropBuffer)
    {
        executionSkill = skill;
        entryId = id;
        chainWindowN = chainWindow;
        dropBufferOnWindowExpire = dropBuffer;
    }
}
