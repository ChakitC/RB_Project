using System;
using System.Collections.Generic;

[Serializable]
public sealed class CharacterSkillUpgradeNodeSaveData
{
    public string nodeId;
    public int paidCost;

    public CharacterSkillUpgradeNodeSaveData DeepClone()
    {
        return new CharacterSkillUpgradeNodeSaveData
        {
            nodeId = nodeId,
            paidCost = paidCost,
        };
    }
}

[Serializable]
public sealed class CharacterSkillTreeProgressSaveData
{
    public string slotId;
    public string optionId;
    public string treeId;
    public List<CharacterSkillUpgradeNodeSaveData> unlockedNodes = new();

    public CharacterSkillTreeProgressSaveData DeepClone()
    {
        var clone = new CharacterSkillTreeProgressSaveData
        {
            slotId = slotId,
            optionId = optionId,
            treeId = treeId,
        };

        if (unlockedNodes == null)
            return clone;

        for (int i = 0; i < unlockedNodes.Count; i++)
        {
            CharacterSkillUpgradeNodeSaveData node = unlockedNodes[i];
            if (node != null)
                clone.unlockedNodes.Add(node.DeepClone());
        }

        return clone;
    }
}
