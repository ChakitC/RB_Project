using UnityEngine;

public static class DropCalculate
{
    private static float _globalDropRate = 1f;
    
    public static void SetGlobalDropRate(float value)
    {
        _globalDropRate = Mathf.Max(0f, value);
    }
    //BaseChance 0.50f = 50% Drop
    public static bool shouldDrop(float baseChance) //float Luck)
    {
        //baseChance = baseChance * Luck
        
        float finalchance = baseChance * _globalDropRate;
        finalchance = Mathf.Clamp01(finalchance);

        float roll = Random.value;
        return roll <= finalchance;
    }
}
