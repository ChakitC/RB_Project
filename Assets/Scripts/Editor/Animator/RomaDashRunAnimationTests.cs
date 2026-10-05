#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RomaDashRunAnimationTests
{
    [TestCase(false, 30)]
    [TestCase(false, 60)]
    [TestCase(false, 120)]
    [TestCase(true, 30)]
    [TestCase(true, 60)]
    [TestCase(true, 120)]
    public void DashToRunKeepsArmRotationsContinuous(bool backward, int frameRate)
    {
        var root = new GameObject("Roma Dash to Run regression");
        root.SetActive(false);
        CharacterAnimProfileSO profile = null;
        try
        {
            profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>(
                "Assets/Data/Animation/Roma/Roma_AnimProfile.asset"));
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Character/Roma/Roma.fbx"), root.transform);
            var animancer = root.AddComponent<AnimancerComponent>();
            animancer.Animator = model.GetComponentInChildren<Animator>();
            animancer.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var brain = root.AddComponent<CharacterAnimBrain>();
            typeof(CharacterAnimBrain).GetField("animancer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(brain, animancer);
            brain.SetAnimProfileOverride(profile);
            root.SetActive(true);
            brain.IsSprinting = true;
            brain.MoveSpeed01 = 1f;
            brain.MoveDirLocal = Vector2.up;
            brain.PlayDash(0.25f, backward ? Vector2.down : Vector2.up);

            var dash = animancer.Layers[0].CurrentState;
            Assert.That(dash.Clip, Is.SameAs(backward ? profile.dashB.Clip : profile.dashF.Clip));
            var bones = model.GetComponentsInChildren<Transform>(true).Where(bone =>
                bone.name.StartsWith("arm_") || bone.name.StartsWith("forearm_") ||
                bone.name.StartsWith("hand.") || bone.name.StartsWith("shoulder.")).ToArray();
            Assert.That(bones.Length, Is.GreaterThanOrEqualTo(16));
            var previous = bones.Select(bone => bone.localRotation).ToArray();
            bool blended = false;
            bool completed = false;
            float maxDelta = 0f;
            string maxBone = null;
            for (int frame = 0; frame < frameRate; frame++)
            {
                animancer.Evaluate(1f / frameRate);
                var current = animancer.Layers[0].CurrentState;
                bool running = current.Clip == profile.sprint.Clip && current.Weight > 0f;
                if (running)
                {
                    blended |= dash.Weight > 0f;
                    completed |= dash.Weight == 0f && current.Weight == 1f;
                }
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    float delta = Quaternion.Angle(previous[bone], bones[bone].localRotation);
                    if (running && delta > maxDelta)
                    {
                        maxDelta = delta;
                        maxBone = bones[bone].name;
                    }
                    previous[bone] = bones[bone].localRotation;
                }
            }
            Assert.That(blended, Is.True, "Dash and Run must overlap instead of cutting.");
            Assert.That(completed, Is.True, "The fade must reach Run at full weight.");
            // Run alone reaches about 22 degrees/frame at 60 Hz. The old Dash
            // endpoint caused a 91.7-degree flip during this same crossfade.
            Assert.That(maxDelta, Is.LessThan(35f * 60f / frameRate),
                $"{maxBone} jumped {maxDelta:F2} degrees at {frameRate} Hz.");
            TestContext.WriteLine($"backward={backward}, fps={frameRate}, max={maxDelta:F3}, bone={maxBone}");
        }
        finally
        {
            Object.DestroyImmediate(root);
            if (profile != null) Object.DestroyImmediate(profile);
        }
    }
}
#endif
