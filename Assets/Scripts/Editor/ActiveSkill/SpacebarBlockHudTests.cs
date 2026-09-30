using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SpacebarBlockHudTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void AuthoredHudIsReusedTracksMeterAndHidesOutsideStruggle()
    {
        var ui = new GameObject("Block UI");
        var actor = new GameObject("Block player");
        actor.SetActive(false);
        try
        {
            var context = ui.AddComponent<PlayerUIContext>();
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Spain/UI/GameplayHUD.prefab"), ui.transform);
            var player = actor.AddComponent<PlayerContext>();
            var command = actor.AddComponent<InterruptionCommandController>();
            player.playerUIContext = context;
            var hud = root.GetComponentInChildren<CooperativeBlockHud>(true);
            Assert.That(CooperativeBlockHud.Ensure(player, command), Is.SameAs(hud));
            Assert.That(CooperativeBlockHud.Ensure(player, command), Is.SameAs(hud));
            Assert.That(actor.GetComponentsInChildren<CooperativeBlockHud>(true), Is.Empty);
            Assert.That(hud.group.alpha, Is.Zero);
            var session = (CooperativeBlockSession)FormatterServices.GetUninitializedObject(typeof(CooperativeBlockSession));
            var meter = new CooperativeBlockMeter(new CooperativeBlockSettings());
            typeof(CooperativeBlockSession).GetField("meter", Private).SetValue(session, meter);
            var state = typeof(CooperativeBlockSession).GetField("<State>k__BackingField", Private);
            void Invoke(string name, params object[] args) => typeof(CooperativeBlockHud).GetMethod(name, Private).Invoke(hud, args);
            state.SetValue(session, CooperativeBlockState.Preparing);
            Invoke("Show", session);
            Assert.That(hud.group.alpha, Is.Zero);
            state.SetValue(session, CooperativeBlockState.Struggling);
            Invoke("Refresh");
            Assert.That(hud.group.alpha, Is.EqualTo(1f));
            Assert.That(hud.slantedMeterFill.FillAmount, Is.EqualTo(.5f));
            Invoke("Tick", hud.blinkInterval);
            Assert.That(hud.pressedArtwork[0].activeSelf, Is.True, "Prompt blinks without input.");
            Invoke("Tick", 0f);
            Assert.That(hud.pressedArtwork[0].activeSelf, Is.True, "Pause holds the current frame.");
            Invoke("Tick", hud.blinkInterval);
            Assert.That(hud.restingArtwork[0].activeSelf, Is.True);
            meter.Press();
            Invoke("OnPress", session);
            Assert.That(hud.slantedMeterFill.FillAmount, Is.EqualTo(.6f).Within(.001f));
            Assert.That(hud.pressedArtwork[0].activeSelf, Is.True);
            Assert.That(hud.restingArtwork[0].activeSelf, Is.False);
            Assert.That(hud.meterFill.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Invoke("Tick", .13f);
            Assert.That(hud.restingArtwork[0].activeSelf, Is.True, "Press feedback returns to the idle blink cycle.");
            foreach (var outcome in new[] { CooperativeBlockState.Success, CooperativeBlockState.Failure,
                CooperativeBlockState.Cancelled, CooperativeBlockState.Finished })
            {
                state.SetValue(session, outcome);
                Invoke("Refresh");
                Assert.That(hud.group.alpha, Is.Zero, outcome.ToString());
                Assert.That(hud.pressedArtwork[0].activeSelf, Is.False, "Blink stops outside struggle.");
            }
            hud.Bind(null);
            Assert.That(hud.group.alpha, Is.Zero);
        }
        finally { Object.DestroyImmediate(ui); Object.DestroyImmediate(actor); }
    }
}
