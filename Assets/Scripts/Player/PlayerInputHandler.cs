using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] PlayerContext ctx;
   
    public void OnMove(InputAction.CallbackContext c)
    {
        ctx.moveInput = c.ReadValue<Vector2>();
       
    }
    public void OnLook(InputAction.CallbackContext c)
    {
        ctx.lookInput = c.ReadValue<Vector2>();
    }
    public void OnDash(InputAction.CallbackContext c)
    {
        if (!c.performed) return;
      ctx.stateHub.RequestOnDash();
    }

    public void OnAim(InputAction.CallbackContext c)
    {
        if (c.performed) ctx.WeaponSystem.OnAim(true);
        if (c.canceled) ctx.WeaponSystem.OnAim(false);
        
    }

    public void OnMelee(InputAction.CallbackContext c)
    {
     
        if (c.performed) ctx.stateHub.RequestOnMelee();
    }

    public void OnFire(InputAction.CallbackContext c)
    {
        if (c.performed)
        {
            ctx.stateHub.RequestOnFire();
           
        }

        if (c.canceled)
        {

            ctx.stateHub.RequestCanceledFire();
        }
    }

    public void OnInterrace(InputAction.CallbackContext c)
    {
        if (c.performed) ctx.Interactor.InteractPressed();
        if (c.canceled) ctx.Interactor.InteractReleased();
    }

    public void OnAllyHelperCall(InputAction.CallbackContext c)
    {
        if (c.performed) ctx.allyHelper.SummonAllyHelper();

    }

    public void OnReload(InputAction.CallbackContext c)
    {
        if (!c.performed) return;
        ctx.stateHub.RequestReload();
    }

    public void OpenInventory(InputAction.CallbackContext c)
    {
        
        if (!c.performed) return;
        bool isOpen = ctx.playerUIContext.inventoryUI.activeSelf;
        ctx.playerUIContext.inventoryUI.SetActive(!isOpen);

     
    
    }

    public void OpenPassiveTree(InputAction.CallbackContext c)
    {
        
        if (!c.performed) return;
        bool isOpen = ctx.playerUIContext.passiveTree.activeSelf;
        ctx.playerUIContext.passiveTree.SetActive(!isOpen);
    }
}