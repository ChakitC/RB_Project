using NUnit.Framework;
using UnityEngine;

public sealed class PartyFormationUnityTests
{
    GameObject _owner;
    PartyFormationController _controller;

    [SetUp]
    public void SetUp()
    {
        _owner = new GameObject("PartyFormationController_Test");
        _controller = _owner.AddComponent<PartyFormationController>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_owner);
    }

    [Test]
    public void TriangleSlotsMatchPartyIndexContract()
    {
        Assert.That(
            _controller.GetLocalSlotOffset(1, PartyFormationController.FormationMode.Triangle),
            Is.EqualTo(new Vector3(-1.5f, 0f, -1.8f)));
        Assert.That(
            _controller.GetLocalSlotOffset(2, PartyFormationController.FormationMode.Triangle),
            Is.EqualTo(new Vector3(1.5f, 0f, -1.8f)));
        Assert.That(
            _controller.GetLocalSlotOffset(3, PartyFormationController.FormationMode.Triangle),
            Is.EqualTo(new Vector3(0f, 0f, -3.2f)));
    }

    [Test]
    public void SingleFileSlotsPreservePartyOrder()
    {
        Assert.That(
            _controller.GetLocalSlotOffset(1, PartyFormationController.FormationMode.SingleFile),
            Is.EqualTo(new Vector3(0f, 0f, -1.8f)));
        Assert.That(
            _controller.GetLocalSlotOffset(2, PartyFormationController.FormationMode.SingleFile),
            Is.EqualTo(new Vector3(0f, 0f, -3.2f)));
        Assert.That(
            _controller.GetLocalSlotOffset(3, PartyFormationController.FormationMode.SingleFile),
            Is.EqualTo(new Vector3(0f, 0f, -4.6f)));
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    public void OnlyCompanionPartyIndicesOwnFormationSlots(int partyIndex, bool expected)
    {
        Assert.That(PartyFormationController.IsCompanionPartyIndex(partyIndex), Is.EqualTo(expected));
    }
}
