using System;

[Serializable]
public class AccessoryInstanceData
{
    public string instanceId;
    public string accessoryId;
    public string modifierId;
    public int upgradeLevel;

    public bool IsEmpty => string.IsNullOrWhiteSpace(accessoryId);

    public AccessoryInstanceData DeepClone()
    {
        return new AccessoryInstanceData
        {
            instanceId = instanceId,
            accessoryId = accessoryId,
            modifierId = modifierId,
            upgradeLevel = upgradeLevel
        };
    }
}
