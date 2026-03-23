using UnityEngine;

public class PlayerContext : CharacteContext
{
    [Header("Modules")]
    public PlayerUIContext playerUIContext;
    
    [Header("Modules")]
    public PlayerMovementCC movement;
    public AllyHelperManager  allyHelper;
    
    [Header("Inventory")]
    public PlayerInventory inventory;

    [Header("Aim Target Reference")]
    public Transform aimTarget;
    
    private void Start()
    {
        var ui = Object.FindObjectOfType<UIMunuBar>(true);
        if (ui != null) ui.SetContext(this);
    }
}
