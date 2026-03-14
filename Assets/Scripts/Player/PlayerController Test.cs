using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using SingularityGroup.HotReload;
using UnityEngine.Serialization;

public class PlayerControllerTest : MonoBehaviour, IDamageable
{
    
    //Player_Status ----------------------------------------------------------------------------
    [Header("Player_Status")]
    WeaponType gunType;
    public float maxHealth;
    public float currentHealth;
    public float armor;
    public float currentStamina;
    public float maxStamina;
    public float moveSpeed;
    [SerializeField] float staminaRegenRate = 15f;
    
   //Weapons_Component --------------------------------------------------------------------------
   [Header("Weapons_Status")]
   public WeaponComponent currentWeapon;
   public float playerDamage = 0.0f;
   public float fireRate = 0.0f;
   public int Magazine = 0;
   public int maxMagazine = 0;
   [FormerlySerializedAs("PlayercriticalRate")] public float PlayerCriticalRate = 0.0f;
   [FormerlySerializedAs("PlayercriticriticalDamage")] public float PlayerCriticalDamage = 0.0f;
   [FormerlySerializedAs("reloadTimePerBullet")] public float reloadTime = 0.0f;
   private float nextFireTime = 0.0f;
   public float Stability = 0.0f;
   public float Range = 0.0f;
   public bool MagazineLoaded;
   public bool IsAlive => currentHealth > 0f;
   
   //-------------------------------------------------------------------------------------------
   
    private UIManager uiManager;
    public GameObject Bullet;
    public Transform firePoint;
    public Vector2 move, mouselook, joysticklook;
    private Vector3 rotationtaget;
    public bool isPc;
    public bool isReloading = false;
    
    
    [Header("Dash Settings")]
    public float dashDistance = 5f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;
    public float dashInvincibleTime = 0.15f;
    public LayerMask obstacleMask = ~0;
    
    
    [SerializeField] float dashCost = 10f;
       
    [SerializeField] float staminaRegenDelay = 1f;
    float staminaRegenBlockedUntil = 0f;
    CharacterController cc;
    bool isDashing, dashOnCooldown;
    bool isInvincible;
    Vector3 lastMoveDir = Vector3.forward;
   
    //---------------------------------------- Inupt ---------------------------------------------
    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }
    
    public void OnMouseLook(InputAction.CallbackContext context)
    {
        mouselook = context.ReadValue<Vector2>();
    }
    
    public void OnJoyStickLook(InputAction.CallbackContext context)
    {
        joysticklook = context.ReadValue<Vector2>();
    }
        
    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        Debug.Log(currentStamina);
        if (currentStamina >= dashCost) TryDash();
        
    }

    public void IsReload(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (!isReloading)
            {
                if (Magazine >= maxMagazine)
                {
                    return;
                }
                else
                {
                    Debug.Log("Reloading");
                    Relord(); 
                } 
            }
              
        }
    }

    public void Shoot(InputAction.CallbackContext context)
    {
        if (Time.time >= nextFireTime && Magazine > 0 && !isReloading)
        {
            nextFireTime = Time.time + fireRate;
            ShootProjectile();
            Magazine--;
            Debug.Log("Total Magazine =" +Magazine);
            if(uiManager != null)
            {
                uiManager.UpdateAmmoText(Magazine, maxMagazine);
            }
        }
        
        //-------------------------------------------SetPlayerStatus---------------------------------------------
    }
    
    void Awake()
    {
        cc = GetComponent<CharacterController>();
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        
        if (currentWeapon != null )
        {
            playerDamage = currentWeapon.Damage;
            fireRate = currentWeapon.fireRate;
            Magazine = currentWeapon.Magazine;
            maxMagazine = currentWeapon.maxMagazine; 
            PlayerCriticalRate = currentWeapon.criticalRate;
            PlayerCriticalDamage = currentWeapon.criticalDamage;
            reloadTime = currentWeapon.reloadTime;
            nextFireTime = currentWeapon.nextFireTime;
            Stability = currentWeapon.Stability;
            Range = currentWeapon.Range;
            MagazineLoaded = currentWeapon.MagazineLoaded;
            
            
            Debug.LogError("พบ WeaponComponent");
        }
        else
        {
            Debug.LogError("ไม่พบ WeaponComponent ");
        }
    }
    
   
    
    //--------------------------------------------------------------------------------------------------------------
   
    void Start()
    {
        
        uiManager = FindObjectOfType<UIManager>(); 
        
            if(uiManager != null)
            {
                uiManager.UpdateAmmoText(Magazine, maxMagazine);
            }
    }
    void Update()
    {
        RegenerateStamina();
        
        Vector3 inputDir = new Vector3(move.x, 0f, move.y);
        if (inputDir.sqrMagnitude > 0.001f)
            lastMoveDir = inputDir.normalized;
        if (!isDashing)
        {
            
            cc.SimpleMove(inputDir * moveSpeed);
        }
        
        if (isPc)
        {
            RaycastHit hit;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(mouselook);
            if (Physics.Raycast(ray, out hit))
            {
                rotationtaget = hit.point;
            }
            moveplayerWithAim();
        }
        else
        {
            if (joysticklook.x != 0 || joysticklook.y != 0)
            {
                moveplayer();
            }
            else
            {
                moveplayerWithAim();   
            }
        }
        
    }

    private void RegenerateStamina()
    {
        if (Time.time < staminaRegenBlockedUntil) return;
        if (currentStamina >= maxStamina) return;

        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        uiManager?.UpdateStamina(currentStamina, maxStamina);
    }

    public void moveplayer()
    {
        Vector3 movement = new Vector3(move.x, 0f, move.y);

        if (movement != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), 0.20f);
        }
        
        transform.Translate(movement * moveSpeed * Time.deltaTime,  Space.World);
    }
    
    //----------------------------------------- Dash -------------------------------------------------------------
    void TryDash()
    {
        if (isDashing || dashOnCooldown) return;
        
         if (currentStamina < dashCost)
         {
             Debug.Log("Not enough staminaSystem to DashSystem.");
             return;
         }
         
         currentStamina = Mathf.Max(0f, currentStamina - dashCost);
         uiManager?.UpdateStamina(currentStamina, maxStamina);
         staminaRegenBlockedUntil = Time.time + staminaRegenDelay;
         
        
        Vector3 inputDir = new Vector3(move.x, 0f, move.y);
        Vector3 dashDir = (inputDir.sqrMagnitude > 0.001f ? inputDir : lastMoveDir);
        if (dashDir.sqrMagnitude < 0.001f) dashDir = transform.forward;
        dashDir.Normalize();
        
        float radius = cc ? cc.radius : 0.3f;
        float height = cc ? cc.height : 1.8f;
        
        Vector3 center = transform.position + Vector3.up * (height * 0.5f);
        Vector3 p1 = center + Vector3.up * (height * 0.5f - radius);
        Vector3 p2 = center - Vector3.up * (height * 0.5f - radius);
        
        float maxDist = dashDistance;
        if (Physics.CapsuleCast(p1, p2, radius, dashDir, out RaycastHit hit, dashDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            maxDist = Mathf.Max(0f, hit.distance - 0.05f); 
        }
        
        StartCoroutine(DashRoutine(dashDir, maxDist));
        
        
    }
    
    IEnumerator DashRoutine(Vector3 dir, float dist)
    {
        isDashing = true;
        dashOnCooldown = true;

        float speed = (dist <= 0f) ? 0f : dist / dashDuration;
        StartCoroutine(InvincibleTimer(dashInvincibleTime));

        float t = 0f;
        while (t < dashDuration)
        {
            
            cc.Move(dir * speed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        dashOnCooldown = false;
    }
        IEnumerator InvincibleTimer(float time)
        {
            isInvincible = true;
            yield return new WaitForSeconds(time);
            isInvincible = false;
        }
    
    //-----------------------------------------------------------------------------------------

    public void moveplayerWithAim()
    {
        if (isPc)
        {
            var lookPos = rotationtaget - transform.position;
            lookPos.y = 0;
            var rotation = Quaternion.LookRotation(lookPos);
            
            Vector3 aimDirection = new Vector3(rotationtaget.x, 0f, rotationtaget.z);
            
            if (aimDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 0.20f);
            }
        }
        else
        {
            Vector3 aimDirection = new Vector3(joysticklook.x, 0f, joysticklook.y);
            if (aimDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimDirection), 0.20f);
            }
        }
        
        Vector3 movement = new Vector3(move.x, 0f, move.y);
        transform.Translate(movement * moveSpeed * Time.deltaTime,  Space.World);
    }

    void ShootProjectile()
    {
        if (Bullet == null || firePoint == null) { Debug.LogError("Bullet prefab หรือ firePoint ไม่ถูกตั้งค่า"); return; }
        GameObject projInstance = Instantiate(Bullet, firePoint.position, firePoint.rotation);
        BulletTest projectileScript = projInstance.GetComponent<BulletTest>();
        
        if (projectileScript == null) { Debug.LogError("Bullet prefab ไม่มีสคริปต์ Bullet"); return; }
        
            projectileScript.Initialize( //ส่ง PlayerStatus ไปให้กระสุน
                shooterPosition: firePoint.position,
                gunType: this.gunType,
                baseDamage: this.playerDamage,
                critRate: this.PlayerCriticalRate,
                critDamage: this.PlayerCriticalDamage
            );
            
            Vector3 shotDirection = firePoint.forward;
            projectileScript.SetDirection(shotDirection);
            
    }

    void Relord()
    {
            StartCoroutine(ReloadCoroutine());
            IEnumerator ReloadCoroutine()
            {
                isReloading = true;
                while (Magazine < maxMagazine)
                {
                    
                    if (Magazine < maxMagazine && MagazineLoaded == false )
                    {
                        yield return new WaitForSeconds(reloadTime);
                        Magazine++; 
                        
                        if(uiManager != null)
                        {
                            uiManager.UpdateAmmoText(Magazine, maxMagazine);
                        }
                        Debug.Log("Magazine: " + Magazine);
                    }
                    else if (Magazine < maxMagazine && MagazineLoaded == true )
                    {
                        yield return new WaitForSeconds(reloadTime);
                        Magazine = maxMagazine;
                        
                        if(uiManager != null)
                        {
                            uiManager.UpdateAmmoText(Magazine, maxMagazine);
                        }
                    }
                }
                isReloading = false;
                Debug.Log("Reload finished!");
            } 
            
    }
    
    public void TakeDamage(float finalDamage)
    {
        if (isInvincible) return;
        if (!IsAlive) return;
        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        Debug.Log("Player HP = " + currentHealth);
        if (!IsAlive) Die();
    }

    void Die()
    {
        StartCoroutine(DieCorcoutine());
    }
    IEnumerator DieCorcoutine ()
    {
        Debug.Log(gameObject.name + " has been defeated!");
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
        
    }
    
   
}
