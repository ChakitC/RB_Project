#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DefensiveBlockCameraTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void GameplayBlockShotFollowsPlayerTranslationWithoutRotating(bool cooperative, bool returning)
    {
        var root = new GameObject("Live Block camera anchor test"); root.SetActive(false);
        var playerObject = new GameObject("Player"); playerObject.SetActive(false);
        var helper = new GameObject("Helper");
        try
        {
            var camera = root.AddComponent<GameplayCameraController>();
            var player = playerObject.AddComponent<PlayerContext>();
            var type = typeof(GameplayCameraController);
            void Set(string field, object value) => type.GetField(field, Private).SetValue(camera, value);
            Set("playerContext", player); Set("cameraCollisionMask", (LayerMask)0);
            Set("blockShotActor", player.transform);
            Set("blockShotOwner", returning ? null : new object());
            Set("blockShotWeight", returning ? .5f : 1f);
            Set("blockShotHeading", Quaternion.Euler(0, 30, 0));
            player.transform.position = new Vector3(4, 0, 7);
            helper.transform.position = player.transform.position + Vector3.right * 1.6f;
            if (cooperative)
            {
                Set("cooperativeShotAlly", helper.transform);
                Set("cooperativeShotBasePosition", camera.blockLocalPosition);
                Set("cooperativeApproachEuler", camera.blockLocalEulerAngles);
                Set("cooperativeApproachFov", camera.blockFieldOfView);
            }
            Unity.Cinemachine.CameraState Sample()
            {
                if (cooperative) type.GetMethod("TickCooperativeFraming", Private).Invoke(camera, new object[] { 0f });
                var state = Unity.Cinemachine.CameraState.Default;
                state.RawPosition = player.transform.position + new Vector3(0, 5, -8);
                object[] args = { state };
                type.GetMethod("ApplyDefensiveBlockShot", Private).Invoke(camera, args);
                return (Unity.Cinemachine.CameraState)args[0];
            }
            var before = Sample();
            Vector3 movement = new Vector3(-2, .3f, 4);
            player.transform.position += movement;
            helper.transform.position += movement;
            player.transform.rotation = Quaternion.Euler(0, 170, 0);
            var after = Sample();
            Assert.That(Vector3.Distance(after.RawPosition, before.RawPosition + movement), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(after.RawOrientation, before.RawOrientation), Is.LessThan(.001f));
            if (cooperative)
            {
                helper.transform.position += Vector3.right * 2;
                Sample();
                Assert.That((Vector3)type.GetField("blockShotOrigin", Private).GetValue(camera), Is.EqualTo(player.transform.position));
            }
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(playerObject); Object.DestroyImmediate(helper); }
    }

    [TestCase(1.8f)]
    [TestCase(0f)]
    public void CooperativeSuccessReplacesStruggleOnceAndCleansUp(float strength)
    {
        var root = new GameObject("Cooperative success shake test"); root.SetActive(false);
        var profile = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        try
        {
            profile.cooperativeSuccessShakeDegrees = strength;
            var camera = root.AddComponent<GameplayCameraController>();
            var type = typeof(GameplayCameraController);
            object owner = new object(), other = new object();
            type.GetField("blockShotOwner", Private).SetValue(camera, owner);
            type.GetField("cooperativeShotAlly", Private).SetValue(camera, root.transform);
            type.GetMethod("ConfigureCooperativeShake", Private).Invoke(camera, new object[] { profile });
            Vector3 Sample() => (Vector3)type.GetMethod("SampleCooperativeShake", Private).Invoke(camera, null);
            camera.PlayCooperativeBlockSuccess(owner);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero), "Cannot win before contact.");
            camera.BeginCooperativeBlockStruggle(owner);
            camera.AdvanceCooperativeBlockShake(owner, .5f);
            camera.EndCooperativeBlockStruggle(owner);
            camera.PlayCooperativeBlockSuccess(other);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero));
            camera.PlayCooperativeBlockSuccess(owner);
            Assert.That(Sample().x, Is.EqualTo(-strength));
            profile.cooperativeSuccessShakeDegrees = 50f;
            Assert.That(Sample().x, Is.EqualTo(-strength), "Settings are snapshotted.");
            camera.AdvanceCooperativeBlockShake(owner, .05f);
            Vector3 decay = Sample();
            camera.PlayCooperativeBlockSuccess(owner);
            camera.AdvanceCooperativeBlockShake(other, 1f);
            camera.EndDefensiveBlockShot(other, false);
            Assert.That(Sample(), Is.EqualTo(decay));
            camera.AdvanceCooperativeBlockShake(owner, .2f);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero), "Struggle must not continue after the success kick.");
            camera.PlayCooperativeBlockSuccess(owner);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero), "Duplicate success cannot restart shake.");
            camera.EndDefensiveBlockShot(owner, true);
            Assert.That((bool)type.GetField("cooperativeSuccessShakeStarted", Private).GetValue(camera), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(profile); }
    }

    [Test]
    public void CooperativeShakeIsOwnedStartsOnceAndStopsWithoutDrift()
    {
        var root = new GameObject("Cooperative shake test"); root.SetActive(false);
        var profile = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        try
        {
            var camera = root.AddComponent<GameplayCameraController>();
            var type = typeof(GameplayCameraController);
            object owner = new object(), other = new object();
            type.GetField("blockShotOwner", Private).SetValue(camera, owner);
            type.GetField("cooperativeShotAlly", Private).SetValue(camera, root.transform);
            type.GetMethod("ConfigureCooperativeShake", Private).Invoke(camera, new object[] { profile });
            Vector3 Sample() => (Vector3)type.GetMethod("SampleCooperativeShake", Private).Invoke(camera, null);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero));
            camera.BeginCooperativeBlockStruggle(other);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero));
            camera.BeginCooperativeBlockStruggle(owner);
            Assert.That(Sample().x, Is.EqualTo(-profile.cooperativeImpactShakeDegrees));
            camera.AdvanceCooperativeBlockShake(owner, .4f);
            Vector3 gentle = Sample();
            Assert.That(gentle.magnitude, Is.GreaterThan(0f).And.LessThan(.2f));
            camera.BeginCooperativeBlockStruggle(owner);
            camera.AdvanceCooperativeBlockShake(other, 1f);
            camera.EndCooperativeBlockStruggle(other);
            Assert.That(Sample(), Is.EqualTo(gentle));
            profile.cooperativeStruggleShakeDegrees = 100f;
            Assert.That(Sample(), Is.EqualTo(gentle), "Active shots snapshot their settings.");
            camera.EndCooperativeBlockStruggle(owner);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero));
            camera.BeginCooperativeBlockStruggle(owner);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero), "A resolved contact cannot restart its impulse.");
            camera.EndDefensiveBlockShot(owner, false);
            Assert.That((bool)type.GetField("cooperativeShakeStarted", Private).GetValue(camera), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(profile); }
    }

    [Test]
    public void CooperativeShakeZeroSettingsDisableBothEffects()
    {
        var root = new GameObject("Disabled cooperative shake test"); root.SetActive(false);
        var profile = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        try
        {
            profile.cooperativeImpactShakeDegrees = profile.cooperativeStruggleShakeDegrees = 0f;
            var camera = root.AddComponent<GameplayCameraController>();
            var type = typeof(GameplayCameraController);
            object owner = new object();
            type.GetField("blockShotOwner", Private).SetValue(camera, owner);
            type.GetField("cooperativeShotAlly", Private).SetValue(camera, root.transform);
            type.GetMethod("ConfigureCooperativeShake", Private).Invoke(camera, new object[] { profile });
            camera.BeginCooperativeBlockStruggle(owner);
            foreach (float delta in new[] { 0f, .03f, .3f, 1f })
            {
                camera.AdvanceCooperativeBlockShake(owner, delta);
                Assert.That((Vector3)type.GetMethod("SampleCooperativeShake", Private).Invoke(camera, null), Is.EqualTo(Vector3.zero));
            }
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(profile); }
    }

    [TestCase(-1.6f, 1.777778f)]
    [TestCase(1.6f, 1.777778f)]
    [TestCase(-1.6f, 1f)]
    [TestCase(1.6f, .5625f)]
    public void CooperativeDollyKeepsBothDefendersInsideNormalBlockLens(float helperX, float aspect)
    {
        var bounds = new Bounds(Vector3.up * 1.1f, new Vector3(1.3f, 2.4f, 1.3f));
        bounds.Encapsulate(new Bounds(new Vector3(helperX, 1.1f, 0), new Vector3(1.3f, 2.4f, 1.3f)));
        var rotation = Quaternion.Euler(-.35f, -35.39f, 10.49f);
        var position = new Vector3(2.65f, 1.55f, -1.91f);
        var method = typeof(GameplayCameraController).GetMethod("CooperativeFramingRetreat", BindingFlags.Static | BindingFlags.NonPublic);
        float retreat = (float)method.Invoke(null, new object[] { bounds, position, rotation, 60f, aspect });
        Assert.That(retreat, Is.GreaterThanOrEqualTo(0f));
        position -= rotation * Vector3.forward * retreat;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            Vector3 view = Quaternion.Inverse(rotation) * (point - position);
            Assert.That(view.z, Is.GreaterThan(0f));
            float halfHeight = view.z * Mathf.Tan(30f * Mathf.Deg2Rad);
            Assert.That(Mathf.Abs(view.y) / halfHeight, Is.LessThanOrEqualTo(.85001f));
            Assert.That(Mathf.Abs(view.x) / (halfHeight * aspect), Is.LessThanOrEqualTo(.85001f));
        }
        Assert.That((float)method.Invoke(null, new object[] { bounds, position, rotation, 60f, aspect }), Is.LessThan(.0001f));
    }

    [Test]
    public void ShotStaysBehindPlayerWithGuardAhead()
    {
        var player = new GameObject("Camera framing Player");
        var actor = new GameObject("Camera framing Aires");
        var cameraObject = new GameObject("Camera framing output", typeof(Camera));
        try
        {
            player.transform.position = new Vector3(2, 0, 3);
            actor.transform.SetPositionAndRotation(player.transform.position + Vector3.right * 2.5f,
                Quaternion.Euler(0, 90, 0));
            var guard = actor.AddComponent<DefensiveBlockController>();
            var shot = cameraObject.AddComponent<DefensiveBlockCameraShot>();
            Invoke(shot, "Awake");
            shot.obstacleLayers = 0;
            shot.Bind(guard, player.transform);
            SetArrived(guard, true);
            Tick(shot, 1f);
            Vector3 position = cameraObject.transform.position;
            Assert.Less(Vector3.Dot(position - player.transform.position, actor.transform.forward), 0f);
            Assert.Less(Vector3.Distance(position, player.transform.position + actor.transform.rotation * shot.localPosition), 0.001f);
            Vector3 movement = Vector3.forward * 5;
            player.transform.position += movement;
            actor.transform.position += Vector3.left;
            Tick(shot, 1f);
            Assert.That(cameraObject.transform.position, Is.EqualTo(position + movement));
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(player); Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void CameraBlendsBackToExactPoseAndLensAfterBlock()
    {
        var actor = new GameObject("Camera test guard");
        var cameraObject = new GameObject("Camera test output", typeof(Camera));
        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 5, -8), Quaternion.Euler(17, 3, 0));
            camera.fieldOfView = 53;
            Vector3 original = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            var guard = actor.AddComponent<DefensiveBlockController>();
            var shot = cameraObject.AddComponent<DefensiveBlockCameraShot>();
            Invoke(shot, "Awake");
            shot.obstacleLayers = 0;
            shot.Bind(guard);
            SetArrived(guard, true);
            Tick(shot, 0.08f);
            Assert.IsTrue(shot.IsPlaying);
            Assert.Greater(Vector3.Distance(original, camera.transform.position), 0.1f);
            Tick(shot, 0.2f);
            Vector3 framed = camera.transform.position;
            SetArrived(guard, false);
            Tick(shot, 0.05f);
            Assert.That(camera.transform.position, Is.EqualTo(framed));
            Tick(shot, 0.25f);
            Assert.IsTrue(shot.IsPlaying);
            Assert.Greater(Vector3.Distance(framed, camera.transform.position), 0.1f);
            Tick(shot, 1f);
            Assert.IsFalse(shot.IsPlaying);
            Assert.That(camera.transform.position, Is.EqualTo(original));
            Assert.Less(Quaternion.Angle(camera.transform.rotation, originalRotation), 0.001f);
            Assert.AreEqual(53, camera.fieldOfView);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void ResetAndDisableDuringShotRestoreWithoutDrift()
    {
        var actor = new GameObject("Camera reset guard");
        var cameraObject = new GameObject("Camera reset output", typeof(Camera));
        try
        {
            Vector3 original = new Vector3(1, 5, -8);
            cameraObject.transform.position = original;
            var guard = actor.AddComponent<DefensiveBlockController>();
            var shot = cameraObject.AddComponent<DefensiveBlockCameraShot>();
            Invoke(shot, "Awake");
            shot.obstacleLayers = 0;
            shot.Bind(guard);
            SetArrived(guard, true);
            Tick(shot, 0.2f);
            SetArrived(guard, false);
            Tick(shot, 0.25f);
            // A second Block during return must not capture the intermediate pose as home.
            actor.transform.position = Vector3.forward * 3;
            SetArrived(guard, true);
            Tick(shot, 0.2f);
            shot.Bind(null);
            Assert.IsFalse(shot.IsPlaying);
            Assert.That(cameraObject.transform.position, Is.EqualTo(original));
            shot.Bind(guard);
            Tick(shot, 0.2f);
            cameraObject.SetActive(false);
            Invoke(shot, "OnDisable");
            Assert.IsFalse(shot.IsPlaying);
            Assert.That(cameraObject.transform.position, Is.EqualTo(original));
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void PendingOrRejectedWarpDoesNotTakeCamera()
    {
        var actor = new GameObject("Camera pending guard");
        var cameraObject = new GameObject("Camera pending output", typeof(Camera));
        try
        {
            var guard = actor.AddComponent<DefensiveBlockController>();
            var shot = cameraObject.AddComponent<DefensiveBlockCameraShot>();
            Invoke(shot, "Awake");
            shot.Bind(guard);
            typeof(DefensiveBlockController).GetField("active", Private).SetValue(guard, true);
            Tick(shot, 1f);
            Assert.IsFalse(shot.IsPlaying);
            SetArrived(guard, false);
            Tick(shot, 1f);
            Assert.IsFalse(shot.IsPlaying);
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(cameraObject); }
    }

    static void SetArrived(DefensiveBlockController guard, bool value)
    {
        typeof(DefensiveBlockController).GetField("active", Private).SetValue(guard, value);
        typeof(DefensiveBlockController).GetField("arrived", Private).SetValue(guard, value);
    }
    static void Tick(DefensiveBlockCameraShot shot, float delta) => Invoke(shot, "Tick", delta);
    static void Invoke(DefensiveBlockCameraShot shot, string method, params object[] args) =>
        typeof(DefensiveBlockCameraShot).GetMethod(method, Private).Invoke(shot, args);
}
#endif
