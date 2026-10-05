using System;
using UnityEngine;

[Serializable]
public sealed class PassiveVariable
{
    public string id;
    public bool persistent;
    public float initial;
    public float maximum = 100f;
}
