using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Melee Combo Config")]
public class MeleeComboConfig : ScriptableObject
{
    public int maxCombo = 3;
    public float comboResetTime = 1.0f;

    public float hitRadius = 1.6f;
    public float damage = 10f;
    public LayerMask hittableLayers;
    public Transform defaultHitOrigin;
}