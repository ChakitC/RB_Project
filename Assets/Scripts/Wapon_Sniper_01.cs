using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    public string gunType;
    public float fireRate = 0.5f;
    public int Magazine = 5;
    public int maxMagazine = 5;
    public float reloadTime = 0.5f;  //ระยะเวลาในการ Relord
    public float Damage = 10f;
    public float criticalRate = 10f;
    public float criticalDamage = 10f;
    public float nextFireTime = 0f;
    public float Stability;  //ค่าความเสถียนของปืนโดยมีผลต่อแกน rotation y ของ firepoin.forward
    public float Range;      //เช็คระยะห่างของ player และ Enemy เพื่อสร้างดาเมจดรอป
    public bool MagazineLoaded;
    void Start()
    {
        
    }

   
    void Update()
    {
        
    }
}
