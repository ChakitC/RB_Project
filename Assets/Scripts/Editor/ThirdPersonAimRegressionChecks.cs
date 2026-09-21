#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu-driven checks; these do not require a test assembly to reference Assembly-CSharp.
public static class ThirdPersonAimRegressionChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/RB/Third Person/Run Aim Regression Checks")]
    public static void Run()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Run collider checks outside Play Mode.");

        Scene original = SceneManager.GetActiveScene();
        Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        int passed = 0;
        try
        {
            foreach (bool nested in new[] { false, true })
                passed += CheckColliderLayout(nested);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(temporary, true);
        }

        Debug.Log($"Third-person aim: {passed} regression checks passed.");
    }

    static int CheckColliderLayout(bool nested)
    {
        var fixture = new GameObject("Temporary aim regression");
        try
        {
            Vector3 origin = new(5000f, 5000f, 5000f);
            var aimObject = new GameObject("Aim");
            aimObject.transform.SetParent(fixture.transform);
            aimObject.SetActive(false);
            ThirdPersonAimController aim = aimObject.AddComponent<ThirdPersonAimController>();
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(fixture.transform);
            muzzle.position = origin + new Vector3(-0.6f, 0f, 1f);

            var actor = new GameObject("Enemy");
            actor.transform.SetParent(fixture.transform);
            EnemyContext context = actor.AddComponent<EnemyContext>();
            var modules = actor;
            if (nested)
            {
                modules = new GameObject("GamePlayStats_System");
                modules.transform.SetParent(actor.transform);
            }
            CharacterColliderRefs refs = modules.AddComponent<CharacterColliderRefs>();
            context.ColliderRefs = refs;
            BoxCollider body = Box(actor.transform, "CharacterPosition", origin + Vector3.forward * 2f,
                new Vector3(2f, 2f, 0.4f));
            refs.CharacterPositionCollider = body;
            BoxCollider zone = Box(actor.transform, "HitZone_Arm", origin + Vector3.forward * 3f,
                new Vector3(0.1f, 0.2f, 0.1f));
            zone.isTrigger = true;
            zone.gameObject.layer = LayerMask.NameToLayer("Hit");
            Require(zone.gameObject.layer >= 0, "The Hit layer must exist.");
            typeof(CharacterColliderRefs).GetField("hitZones", Hidden).SetValue(refs,
                new List<CharacterColliderRefs.HitZoneColliderRef>
                {
                    new() { Collider = zone, HitZone = CharacterHitZone.Torso }
                });
            BoxCollider unmapped = Box(actor.transform, "Unmapped model collider",
                origin + Vector3.forward * 2.5f, Vector3.one * 0.2f);
            unmapped.isTrigger = true;
            unmapped.gameObject.layer = zone.gameObject.layer;

            Ray cameraRay = new(origin, Vector3.forward);
            Physics.SyncTransforms();
            Require(Cast(aim, cameraRay, true, out RaycastHit hit) && hit.collider == zone,
                "Body capsule or unmapped collider stole the camera aim from the arm.");
            typeof(ThirdPersonAimController).GetProperty("AimPoint").SetValue(aim, hit.point);
            Vector3 direction = aim.ResolveShotDirection(muzzle, 0f);
            Require(zone.Raycast(new Ray(muzzle.position, direction), out _, 10f),
                "Unspread weapon direction misses the camera-selected arm.");
            Require(!Cast(aim, cameraRay, false, out _),
                "Non-damageable body capsule incorrectly blocks the muzzle probe.");

            BoxCollider wall = Box(fixture.transform, "World cover", origin + Vector3.forward * 1.5f,
                Vector3.one * 0.25f);
            Physics.SyncTransforms();
            Require(Cast(aim, cameraRay, true, out hit) && hit.collider == wall,
                "World cover in front of the arm must still stop camera aim.");
            Require(Cast(aim, cameraRay, false, out hit) && hit.collider == wall,
                "World cover must still block the muzzle probe.");
            wall.enabled = false;

            typeof(CharacterColliderRefs).GetField("hitZones", Hidden).SetValue(refs,
                new List<CharacterColliderRefs.HitZoneColliderRef>());
            Physics.SyncTransforms();
            Require(Cast(aim, cameraRay, true, out hit) && hit.collider == body,
                "Legacy actors without hit zones lost body-collider targeting.");

            Vector3 nearPoint = muzzle.position + new Vector3(0.3f, 0.2f, 0.1f);
            typeof(ThirdPersonAimController).GetProperty("AimPoint").SetValue(aim, nearPoint);
            Require(Vector3.Angle(aim.ResolveShotDirection(muzzle, 0f), nearPoint - muzzle.position) < 0.01f,
                "A nearby forward target was moved away from its exact camera hit.");
            return 7;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    static BoxCollider Box(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        var collider = obj.AddComponent<BoxCollider>();
        collider.size = size;
        return collider;
    }

    static bool Cast(ThirdPersonAimController aim, Ray ray, bool triggers, out RaycastHit hit)
    {
        object[] args = { ray, 10f, 0f, triggers, default(RaycastHit) };
        bool found = (bool)typeof(ThirdPersonAimController).GetMethod("TryGetFirstValidHit", Hidden)
            .Invoke(aim, args);
        hit = (RaycastHit)args[4];
        return found;
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [MenuItem("Tools/RB/Third Person/Run Live Aim Shot Checks")]
    public static void RunLiveShotChecks() => Debug.Log(CheckLiveShots());

    public static string CheckLiveShots()
    {
        Require(Application.isPlaying && EditorApplication.isPaused,
            "Pause Play Mode while aiming at a live enemy hurtbox before running shot checks.");
        PlayerContext player = UnityEngine.Object.FindFirstObjectByType<PlayerContext>();
        Require(player != null && player.thirdPersonAim != null && player.WeaponSystem != null,
            "A player with weapon and third-person aim is required.");
        ThirdPersonAimController aim = player.thirdPersonAim;
        WeaponSystem weapon = player.WeaponSystem;
        Physics.SyncTransforms();
        typeof(ThirdPersonAimController).GetMethod("TickAimPoint", Hidden).Invoke(aim, null);
        Require(Cast(aim, new Ray(aim.CameraRayOrigin, aim.CameraRayDirection), true, out RaycastHit hit),
            "Aim at a hurtbox within ten world units.");
        CharacteContext target = CharacterContextModuleLookup.ResolveContext(hit.collider);
        HealthSystem health = target != null ? target.HealthSystem : null;
        Require(health != null && health.currentHealth > 100f,
            "Use a healthy test enemy so these temporary shots cannot kill it.");

        SimulationMode previousMode = Physics.simulationMode;
        float healthBefore = health.currentHealth;
        Projectile bullet = null;
        int passed = 0;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            foreach (float speed in new[] { weapon.bulletSpeed, 200f })
            {
                for (int shot = 0; shot < 5; shot++)
                {
                    var existing = new HashSet<Projectile>(
                        UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None));
                    Vector3 direction = aim.ResolveShotDirection(weapon.FirePoint, 0f);
                    new WeaponProjectileSpawner().Spawn(new WeaponProjectileSpawnContext(
                        weapon.projectileConfig, weapon.projectilePrefab, weapon.FirePoint,
                        player.transform, player.transform.root, player, null, null,
                        weapon.gunType, 0f, 1f, 10f, speed, direction, 0f, null,
                        "aim-regression", null, default));
                    foreach (Projectile candidate in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    {
                        if (!existing.Contains(candidate))
                        {
                            bullet = candidate;
                            break;
                        }
                    }
                    Require(bullet != null, "WeaponProjectileSpawner did not spawn a bullet.");
                    for (int step = 0; step < 8 && bullet.gameObject.activeInHierarchy; step++)
                    {
                        typeof(Projectile).GetMethod("FixedUpdate", Hidden).Invoke(bullet, null);
                        if (bullet.gameObject.activeInHierarchy)
                            Physics.Simulate(Time.fixedDeltaTime);
                    }
                    Require(health.currentHealth < healthBefore,
                        $"Shot {shot + 1} at speed {speed} missed {hit.collider.name}.");
                    Require(!bullet.gameObject.activeInHierarchy,
                        "The normal non-piercing test bullet did not despawn on impact.");
                    health.currentHealth = healthBefore;
                    bullet = null;
                    passed++;
                }
            }
        }
        finally
        {
            if (bullet != null && bullet.gameObject.activeInHierarchy)
                bullet.DespawnForRoomTransition();
            health.currentHealth = healthBefore;
            Physics.simulationMode = previousMode;
        }

        return $"Live aim: {passed}/10 actual projectiles hit {hit.collider.name} and despawned " +
               $"at speeds {weapon.bulletSpeed} and 200. Target HP restored.";
    }
}
#endif
