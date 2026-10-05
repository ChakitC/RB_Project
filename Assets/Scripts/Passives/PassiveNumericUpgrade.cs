using System;

[Serializable]
public sealed class PassiveNumericUpgrade
{
    public string upgradeId;
    public ModifierOp operation;
    public float value;
}
