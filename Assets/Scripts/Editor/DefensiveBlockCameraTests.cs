#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DefensiveBlockCameraTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

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
            player.transform.position += Vector3.forward * 5;
            actor.transform.position += Vector3.left;
            Tick(shot, 1f);
            Assert.That(cameraObject.transform.position, Is.EqualTo(position));
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
