using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Fragment")]
public sealed class PassiveFragment : ScriptableObject
{
    public List<PassiveParameter> parameters = new();
    public List<PassiveVariable> variables = new();
    [SerializeReference] public List<PassiveStep> steps = new();
}
