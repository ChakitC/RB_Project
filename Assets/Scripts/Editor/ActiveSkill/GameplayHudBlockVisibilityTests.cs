using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GameplayHudBlockVisibilityTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void StruggleFadesRightPanelsAndRestoresAuthoredVisibilityOnCancelAndUnbind()
    {
        var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spain/UI/GameplayHUD.prefab"));
        var actor = new GameObject("Block HUD player");
        actor.SetActive(false);
        var command = actor.AddComponent<InterruptionCommandController>();
        var active = typeof(InterruptionCommandController).GetField("<ActiveCooperativeBlock>k__BackingField", Private);
        try
        {
            var player = actor.AddComponent<PlayerContext>();
            player.interruptionCommand = command;
            var ammo = hud.transform.Find("AmmoDisplay").gameObject.AddComponent<CanvasGroup>();
            ammo.alpha = .7f;
            ammo.interactable = false;
            var visibility = hud.AddComponent<GameplayHudBlockVisibility>();
            visibility.Bind(player);
            string statusPose = EditorJsonUtility.ToJson(hud.transform.Find("PlayerHpStamina"));
            var session = (CooperativeBlockSession)FormatterServices.GetUninitializedObject(typeof(CooperativeBlockSession));
            var state = typeof(CooperativeBlockSession).GetField("<State>k__BackingField", Private);
            active.SetValue(command, session);
            void Advance(float seconds) => typeof(GameplayHudBlockVisibility).GetMethod("UpdateVisibility", Private)
                .Invoke(visibility, new object[] { seconds });
            state.SetValue(session, CooperativeBlockState.Preparing);
            Advance(1f);
            Assert.That(ammo.alpha, Is.EqualTo(.7f));
            state.SetValue(session, CooperativeBlockState.Struggling);
            Advance(.09f);
            Assert.That(ammo.alpha, Is.GreaterThan(0f).And.LessThan(.7f));
            Advance(.2f);
            Assert.That(ammo.alpha, Is.Zero);
            Assert.That(ammo.blocksRaycasts, Is.False);
            Assert.That(hud.transform.Find("SkillReady Ally 1").GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(hud.transform.Find("AllyTop").GetComponent<CanvasGroup>(), Is.Null);
            Assert.That(EditorJsonUtility.ToJson(hud.transform.Find("PlayerHpStamina")), Is.EqualTo(statusPose));
            Assert.That(ammo.gameObject.activeSelf, Is.True, "Hidden presenters must keep updating.");
            state.SetValue(session, CooperativeBlockState.Success);
            Advance(1f);
            Assert.That(ammo.alpha, Is.Zero);
            active.SetValue(command, null);
            Advance(.2f);
            Assert.That(ammo.alpha, Is.EqualTo(.7f));
            Assert.That(ammo.interactable, Is.False);
            Assert.That(ammo.blocksRaycasts, Is.True);
            active.SetValue(command, session);
            Advance(.2f);
            visibility.Bind(player);
            visibility.Bind(null);
            Assert.That(ammo.alpha, Is.EqualTo(.7f), "Rebinding cannot capture a faded baseline.");
        }
        finally
        {
            active.SetValue(command, null);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(hud);
        }
    }
}
