using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Passives/Recipe")]
public sealed class PassiveRecipe : ScriptableObject
{
    public List<PassiveParameter> parameters = new();
    public List<PassiveVariable> variables = new();
    public List<PassiveRecipeRule> rules = new();
}
