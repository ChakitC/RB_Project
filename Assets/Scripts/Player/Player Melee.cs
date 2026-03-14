using UnityEngine;

public class MeleeSystem : MonoBehaviour
{
    [Header("Refs")]
    public CharacteContext ctx;
    
    [Header("Melee Config")]
    public MeleeComboConfig config;
    
    // public Animator anim;
    // public MeleeComboConfig config;

    private int currentCombo = 0;
    private float comboTimer = 0f;
    private bool isComboWindow = false;
    private bool canAttack = true;
    
    
    void Update()
    
    {
        if (currentCombo > 0)
        {
            comboTimer += Time.deltaTime;
            if (comboTimer > config.comboResetTime)
                ResetCombo();
        }
    }
    
    public void TryMelee()
    {
        if (!canAttack) return;
        if (!isComboWindow && currentCombo > 0) return;

        currentCombo++;
        if (currentCombo > config.maxCombo)
        {
            ResetCombo();
            currentCombo = 1;
        }
        
        Debug.Log("meleeAttack" +canAttack);
        // anim.SetInteger("ComboIndex", currentCombo);          AnimationAttack
        // anim.SetTrigger("Attack");

        canAttack = false;
        comboTimer = 0f;
    }
    
    public void OnLastAttackEnd()
    {
        ResetCombo();
    }
    
    void ResetCombo()
    {
        currentCombo = 0;
        comboTimer = 0f;
        isComboWindow = false;
        canAttack = true;
        // anim.SetInteger("ComboIndex", 0);       Animation
    }


   
}
