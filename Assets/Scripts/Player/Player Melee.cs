using UnityEngine;

[DisallowMultipleComponent]
public class MeleeSystem : MonoBehaviour
{
    [Header("Legacy Compatibility")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private MeleeController meleeController;
    [SerializeField] private MeleeComboConfig config;

    void Awake()
    {
        if (!ctx)
            ctx = GetComponent<CharacteContext>();
        if (!meleeController)
            meleeController = GetComponent<MeleeController>();
        if (!meleeController)
            meleeController = gameObject.AddComponent<MeleeController>();
    }

    public void TryMelee()
    {
        meleeController?.TryStartMelee(CharacterAnimBrain.MeleeType.Heavy);
    }
    
    public void OnLastAttackEnd()
    {
    }
}
