#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DefensiveBlockGroundingTests
{
    [Test]
    public void AuthoredImpactSegmentKeepsASupportingFootNearGround()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BlockAnimationProfile>(
            "Assets/Data/DefensiveBlock/AiresBlockAnimation.asset");
        Assert.That(profile.impactStartNormalized, Is.LessThan(profile.impactEndNormalized));
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(profile.impactClip)));
        try
        {
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.position = Vector3.zero;
            var animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            var feet = model.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "foot.l" || t.name == "foot.r").ToArray();
            Assert.That(feet.Length, Is.EqualTo(2));
            for (int i = 0; i <= 60; i++)
            {
                float time = Mathf.Lerp(profile.impactStartNormalized, profile.impactEndNormalized, i / 60f);
                profile.impactClip.SampleAnimation(model, time * profile.impactClip.length);
                Assert.That(Mathf.Min(feet[0].position.y, feet[1].position.y), Is.InRange(0f, .25f), $"Airborne recoil at {time}");
            }
        }
        finally { Object.DestroyImmediate(model); }
    }

    [Test]
    public void AuthoredBeginSegmentKeepsFeetNearGround()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BlockAnimationProfile>(
            "Assets/Data/DefensiveBlock/AiresBlockAnimation.asset");
        Assert.That(profile.beginStartNormalized, Is.LessThanOrEqualTo(profile.guardPoseNormalized));
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            AssetDatabase.GetAssetPath(profile.beginClip)));
        try
        {
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.position = Vector3.zero;
            var animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            var feet = model.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "foot.l" || t.name == "foot.r").ToArray();
            Assert.That(feet.Length, Is.EqualTo(2));
            for (int i = 0; i <= 20; i++)
            {
                float normalized = Mathf.Lerp(profile.beginStartNormalized, profile.guardPoseNormalized, i / 20f);
                profile.beginClip.SampleAnimation(model, normalized * profile.beginClip.length);
                foreach (var foot in feet)
                    Assert.That(foot.position.y, Is.InRange(0f, 0.25f), $"{foot.name} at {normalized}");
            }
        }
        finally { Object.DestroyImmediate(model); }
    }

    [Test]
    public void LandingRequiresNearbySolidWorldSupport()
    {
        var actor = new GameObject("Block ground test actor");
        actor.SetActive(false);
        var controller = actor.AddComponent<DefensiveBlockController>();
        controller.worldLayers = 1;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var collider = floor.GetComponent<Collider>();
        Vector3 position = new Vector3(5000f, 5000f, 5000f);
        floor.transform.position = position - Vector3.up * 0.5f;
        var method = typeof(DefensiveBlockController).GetMethod("HasLandingGround",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(Vector3), typeof(Quaternion) }, null);
        bool Supported() => (bool)method.Invoke(controller, new object[] { position + Vector3.up * 0.08f, Quaternion.identity });
        try
        {
            Physics.SyncTransforms();
            Assert.IsTrue(Supported(), "Allow the normal baked NavMesh surface offset.");
            collider.enabled = false;
            Physics.SyncTransforms();
            Assert.IsFalse(Supported(), "A floor removed during fade-out must invalidate the landing.");
            collider.enabled = true;
            collider.isTrigger = true;
            Physics.SyncTransforms();
            Assert.IsFalse(Supported(), "Triggers must not support a landing.");
            collider.isTrigger = false;
            floor.transform.position -= Vector3.up;
            Physics.SyncTransforms();
            Assert.IsFalse(Supported(), "A floor far below must not validate a floating pose.");
        }
        finally
        {
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void CompanionPlacementUsesStableLocomotionBodyAndStillDetectsWalls()
    {
        var holder = new GameObject("Inactive Block placement fixture");
        holder.SetActive(false);
        var bodyRoot = new GameObject("Active locomotion body for physics query");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Vector3 point = new Vector3(5000f, 5000f, 5000f);
        try
        {
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/AI Ally/Ally.prefab"), holder.transform);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Charactor/Aires.prefab"), actor.transform);
            var ctx = actor.GetComponent<AllyContext>();
            // Keep the physics query body active while gameplay modules stay inactive,
            // using the production locomotion body's dimensions.
            var productionBody = ctx.cc;
            var queryBody = bodyRoot.AddComponent<CharacterController>();
            queryBody.center = productionBody.center;
            queryBody.height = productionBody.height;
            queryBody.radius = productionBody.radius;
            queryBody.skinWidth = productionBody.skinWidth;
            ctx.cc = queryBody;
            ctx.ColliderRefs = model.GetComponentInChildren<CharacterColliderRefs>(true);
            var controller = actor.GetComponentInChildren<DefensiveBlockController>(true);
            if (controller == null) controller = actor.AddComponent<DefensiveBlockController>();
            typeof(DefensiveBlockController).GetField("ctx", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, ctx);
            var selectBody = typeof(DefensiveBlockController).GetMethod("ResolvePlacementBody",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Collider Body() => (Collider)selectBody.Invoke(controller, null);
            var animated = ctx.ColliderRefs.CharacterPositionCollider;
            Assert.AreSame(ctx.cc, Body());
            Assert.IsTrue(CharacterPlacementFootprintUtility.TryGetColliderFootprint(Body(), actor.transform,
                out var expected, out _));
            animated.transform.localPosition += new Vector3(.1f, -.15f, -.1f);
            animated.transform.localRotation *= Quaternion.Euler(15f, 0f, 10f);
            Assert.AreSame(ctx.cc, Body());
            Assert.IsTrue(CharacterPlacementFootprintUtility.TryGetColliderFootprint(Body(), actor.transform,
                out var actual, out _));
            Assert.AreEqual(expected, actual, "Animation must not change the placement footprint.");
            floor.transform.position = point - Vector3.up * .5f;
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            floor.layer = wall.layer = 31;
            wall.transform.position = point + new Vector3(0f, 1.2f, .4f);
            wall.transform.localScale = new Vector3(4f, 3f, .2f);
            var request = new CharacterPlacementRequest(actor.transform, Body(), actual, ctx.TargetIdentity,
                null, default, new[] { new CharacterPlacementRequest.Candidate(point + Vector3.up * .068f,
                    Quaternion.identity, 0f, 0) }, null, 0f, null, 1 << 31, 0, null, null,
                false, false, false, runtimePolicy: CharacterPlacementRuntimePolicy.CreateDefault(false,
                    .75f, QueryTriggerInteraction.Ignore, collisionPadding: 0f));
            Physics.SyncTransforms();
            Assert.IsTrue(CharacterPlacementResolver.TryResolve(request, null, out var blocked));
            Assert.That(blocked.Score.MaxWorldPenetration, Is.GreaterThan(.005f), "Keep rejecting walls.");
            wall.GetComponent<Collider>().enabled = false;
            Physics.SyncTransforms();
            Assert.IsTrue(CharacterPlacementResolver.TryResolve(request, null, out var grounded));
            Assert.That(grounded.Score.MaxWorldPenetration, Is.LessThanOrEqualTo(.005f),
                "A clear floor must not reject the locomotion body.");
            ctx.cc.enabled = false;
            Assert.AreSame(animated, Body(), "Preserve model collider fallback without an enabled locomotion body.");
        }
        finally
        {
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(bodyRoot);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(wall);
        }
    }

    public static void Run()
    {
        var tests = new DefensiveBlockGroundingTests();
        tests.AuthoredBeginSegmentKeepsFeetNearGround();
        tests.AuthoredImpactSegmentKeepsASupportingFootNearGround();
        tests.LandingRequiresNearbySolidWorldSupport();
        tests.CompanionPlacementUsesStableLocomotionBodyAndStillDetectsWalls();
        Debug.Log("DefensiveBlock grounding: 4 tests passed.");
    }
}
#endif
