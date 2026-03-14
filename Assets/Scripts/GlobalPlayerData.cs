using UnityEngine;


public static class GlobalPlayerData 
{
   
    public static PlayerControllerTest Player; 
    
    public static float PlayerDamage 
    {
        get 
        {
            if (Player !=null)
            {
                return Player.playerDamage;
            }
            
            return 1f; 
          
        }
    }
}