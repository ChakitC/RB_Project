using UnityEngine;

public class PlayerContext : CharacteContext
{
    
    [Header("Modules")]
    public PlayerMovementCC movement;
    
    
    [Header("Inventory")]
    public PlayerInventory inventory;

    [Header("Aim Target Reference")]
    public Transform aimTarget;

    [Header("Input Values")]
    public Vector2 moveInput;
    public Vector2 lookInput;
    
    [Header("Character State")]
    public StateHub  stateHub;
    
    public bool isPC = false;
    // public bool isPC = true;
    
   
}
