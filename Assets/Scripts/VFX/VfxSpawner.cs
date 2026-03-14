using UnityEngine;
using DamageNumbersPro;

public class VfxSpawner : MonoBehaviour
{
    public static VfxSpawner Instance { get; private set; }

    public DamageNumber numberPrefab;
    
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
       DontDestroyOnLoad(gameObject);
    }

    public void SpawnVfx(GameObject prefab, Vector3 pos, Vector3 normal, float extraLife = 0f ,float scale = 1f)
    {
        if (prefab == null) return;

        Quaternion rot = Quaternion.LookRotation(normal);
        GameObject vfx = Instantiate(prefab, pos, rot);
        
        vfx.transform.localScale = Vector3.one * scale;
        
        var ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            float duration = ps.main.duration + ps.main.startLifetime.constantMax + extraLife;
            Destroy(vfx, duration);
        }
        else
        {
            Destroy(vfx, 2f + extraLife);
        }
    }

    public void SpawnDamageNumber(Vector3 position , float number)
    {
        if (numberPrefab == null) return;
        DamageNumber dn = numberPrefab.Spawn(position, number);
    }
    
    
    
}