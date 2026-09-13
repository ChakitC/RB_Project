using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PartyHudTests
{
    [TestCase(.1,0)]
    [TestCase(.399,0)]
    [TestCase(.4,1)]
    [TestCase(.8,1)]
    public void ReleaseChoosesExactlyOneSkill(double duration,int expected)
    {
        var press=new PartyHudPressState();press.Begin(0);
        Assert.That(press.Release(duration),Is.EqualTo(expected));
        Assert.That(press.Release(duration),Is.EqualTo(-1));
    }
    [Test]
    public void HeldUltimateDoesNotFallBackToActiveEvenWhenRejected()
    {
        var press=new PartyHudPressState();press.Begin(0);
        Assert.That(press.Hold(.39),Is.False);
        Assert.That(press.Hold(.4),Is.True);
        Assert.That(press.Hold(.5),Is.False);
        Assert.That(press.Release(.6),Is.EqualTo(-1));
    }
    [Test]
    public void PauseOrRebindCancelsPendingPress()
    {
        var press=new PartyHudPressState();press.Begin(0);press.Cancel();
        Assert.That(press.Release(.2),Is.EqualTo(-1));Assert.That(press.Hold(.8),Is.False);
    }
    [Test]
    public void SeparatePrefabKeepsFourColumnsAndThreeHpOnlyAllies()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PartyHudPrefabBuilder.Output);
        Assert.That(prefab,Is.Not.Null);
        Assert.That(prefab.transform.Find("UI_Manager/PlayerHUD").gameObject.activeSelf,Is.False);
        var hud=prefab.GetComponentInChildren<PartyHudPresenter>(true);
        var so=new SerializedObject(hud);
        Assert.That(so.FindProperty("skillFrames").arraySize,Is.EqualTo(8));
        Assert.That(so.FindProperty("healthFills").arraySize,Is.EqualTo(3));
        Assert.That(so.FindProperty("comboRoot").objectReferenceValue,Is.Not.Null);
        Assert.That(((GameObject)so.FindProperty("comboRoot").objectReferenceValue).activeSelf,Is.False);
        foreach(var image in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true)) Assert.That(image.raycastTarget,Is.False);
    }
    [Test]
    public void KeyboardOverridesRestoreWithoutDisablingGamepad()
    {
        var actor=new GameObject("InputTest");var hud=new GameObject("HudTest");
        var asset=ScriptableObject.CreateInstance<InputActionAsset>();
        try
        {
            var map=new InputActionMap("Gameplay");asset.AddActionMap(map);
            var melee=map.AddAction("Melee",InputActionType.Button);
            melee.AddBinding("<Keyboard>/e");melee.AddBinding("<Gamepad>/buttonSouth");
            var input=actor.AddComponent<PlayerInput>();input.actions=asset;
            var ctx=actor.AddComponent<PlayerContext>();
            var router=hud.AddComponent<PartyHudInputRouter>();
            typeof(PartyHudInputRouter).GetMethod("Bind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(router,new object[]{ctx});
            var bound=input.actions.FindAction("Melee");
            Assert.That(bound.bindings[0].effectivePath,Is.Empty);
            Assert.That(bound.bindings[1].effectivePath,Is.EqualTo("<Gamepad>/buttonSouth"));
            typeof(PartyHudInputRouter).GetMethod("Restore",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(router,null);
            Assert.That(bound.bindings[0].effectivePath,Is.EqualTo("<Keyboard>/e"));
        }
        finally {Object.DestroyImmediate(hud);Object.DestroyImmediate(actor);Object.DestroyImmediate(asset);}
    }

    [Test]
    public void BindingAnotherPlayerRefreshesHpAndHidesAbsentCombo()
    {
        var hud=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PartyHudPrefabBuilder.Output));
        var first=new GameObject("FirstPlayer");var second=new GameObject("SecondPlayer");
        try
        {
            var presenter=hud.GetComponentInChildren<PartyHudPresenter>(true);
            var a=first.AddComponent<PlayerContext>();var ah=first.AddComponent<HealthSystem>();
            a.HealthSystem=ah;ah.maximumHealth=200;ah.currentHealth=100;
            var b=second.AddComponent<PlayerContext>();var bh=second.AddComponent<HealthSystem>();
            b.HealthSystem=bh;bh.maximumHealth=400;bh.currentHealth=100;
            var so=new SerializedObject(presenter);
            var fill=(UnityEngine.UI.Image)so.FindProperty("playerHealth").objectReferenceValue;
            presenter.Bind(a);Assert.That(fill.fillAmount,Is.EqualTo(.5f));
            presenter.Bind(b);Assert.That(fill.fillAmount,Is.EqualTo(.25f));
            Assert.That(((GameObject)so.FindProperty("comboRoot").objectReferenceValue).activeSelf,Is.False);
            presenter.Bind(null);Assert.That(fill.fillAmount,Is.Zero);
        }
        finally {Object.DestroyImmediate(hud);Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
    }
}
