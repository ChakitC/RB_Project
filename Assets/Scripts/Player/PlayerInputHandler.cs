using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] PlayerContext ctx;
    [Header("Chain Attack")]
    [SerializeField] SkillChainDef chainAttackDefinition;

    void Awake()
    {
        ResolveReferences();
    }

    void ResolveReferences()
    {
        if (!ctx)
            ctx = GetComponentInParent<PlayerContext>();

        ctx?.ResolveReferences();
    }
   
    public void OnMove(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (ctx == null) return;

        ctx.moveInput = c.ReadValue<Vector2>();
       
    }
    public void OnLook(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (ctx == null) return;

        ctx.lookInput = c.ReadValue<Vector2>();
    }
    public void OnDash(InputAction.CallbackContext c)
    {
        if (!c.performed) return;
        ResolveReferences();
        ctx?.stateHub?.RequestOnDash();
    }

    public void OnAim(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed) ctx?.WeaponSystem?.OnAim(true);
        if (c.canceled) ctx?.WeaponSystem?.OnAim(false);
        
    }

    public void OnMelee(InputAction.CallbackContext c)
    {
     
        ResolveReferences();
        if (c.performed) ctx?.stateHub?.RequestOnMelee();
    }

    public void OnFire(InputAction.CallbackContext c)
    {
        ResolveReferences();

        if (c.performed)
        {
            ctx?.stateHub?.RequestOnFire();
           
        }

        if (c.canceled)
        {

            ctx?.stateHub?.RequestCanceledFire();
        }
    }

    public void OnInterrace(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed) ctx?.Interactor?.InteractPressed();
        if (c.canceled) ctx?.Interactor?.InteractReleased();
    }

    public void OnAllyHelperCall(InputAction.CallbackContext c)
    {
        if (!c.performed)
            return;

        ResolveReferences();
        if (ctx == null)
            return;

        if (ctx != null && ctx.partyCommand != null && ctx.partyCommand.HasPartyCommands)
        {
            ctx.partyCommand.TryExecuteSelectedPartyCommand();
            return;
        }

        ctx.allyHelper?.SummonAllyHelper();
    }

    public void OnPartyCommandNext(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.SelectNextPartyCommand();
    }

    public void OnPartyCommandPrevious(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.SelectPreviousPartyCommand();
    }

    public void OnPartyCommandSlot1(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(0);
    }

    public void OnPartyCommandSlot2(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(1);
    }

    public void OnPartyCommandSlot3(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(2);
    }

    public void OnPartyCommandSlot4(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(3);
    }

    public void OnReload(InputAction.CallbackContext c)
    {
        if (!c.performed) return;
        ResolveReferences();
        ctx?.stateHub?.RequestReload();
    }

    public void OpenInventory(InputAction.CallbackContext c)
    {
        
        if (!c.performed) return;
        ResolveReferences();
        if (ctx?.playerUIContext?.inventoryUI == null)
            return;

        bool isOpen = ctx.playerUIContext.inventoryUI.activeSelf;
        ctx.playerUIContext.inventoryUI.SetActive(!isOpen);

     
    
    }

    public void OpenPassiveTree(InputAction.CallbackContext c)
    {
        
        if (!c.performed) return;
        ResolveReferences();
        if (ctx?.playerUIContext?.passiveTree == null)
            return;

        bool isOpen = ctx.playerUIContext.passiveTree.activeSelf;
        ctx.playerUIContext.passiveTree.SetActive(!isOpen);
    }

    public void OpenChainSkill(InputAction.CallbackContext c)
    {
        if (!c.performed)
            return;

        ResolveReferences();
        if (ctx?.partyCommand == null)
            return;

        ctx.partyCommand.TryExecuteChainAttack(chainAttackDefinition);
    }
}
