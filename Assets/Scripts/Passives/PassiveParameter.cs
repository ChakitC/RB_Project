using System;
using UnityEngine;

public enum PassiveParameterKind { Number, Status }

[Serializable]
public sealed class PassiveParameter
{
    public string id;
    public PassiveParameterKind kind;
    public float number;
    public StatusEffectDef status;
}
