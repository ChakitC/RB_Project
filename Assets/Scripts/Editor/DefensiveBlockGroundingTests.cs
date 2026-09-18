#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DefensiveBlockGroundingTests
{
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
            BindingFlags.Instance | BindingFlags.NonPublic);
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

    public static void Run()
    {
        var tests = new DefensiveBlockGroundingTests();
        tests.AuthoredBeginSegmentKeepsFeetNearGround();
        tests.LandingRequiresNearbySolidWorldSupport();
        Debug.Log("DefensiveBlock grounding: 2 tests passed.");
    }
}
#endif
