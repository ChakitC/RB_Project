using NUnit.Framework;
using UnityEngine;

public sealed class RoomRuntimeContentTests
{
    [Test]
    public void EnsureRootsCreatesStableRuntimeHierarchy()
    {
        var roomObject = new GameObject("Room");
        try
        {
            RoomRuntimeContent content = roomObject.AddComponent<RoomRuntimeContent>();
            content.EnsureRoots();

            Transform persistent = content.PersistentRoot;
            Transform encounter = content.EncounterRoot;
            Transform temporary = content.TemporaryRoot;

            content.EnsureRoots();

            Assert.That(content.PersistentRoot, Is.SameAs(persistent));
            Assert.That(content.EncounterRoot, Is.SameAs(encounter));
            Assert.That(content.TemporaryRoot, Is.SameAs(temporary));
            Assert.That(persistent.parent, Is.SameAs(encounter.parent));
            Assert.That(encounter.parent, Is.SameAs(temporary.parent));
            Assert.That(persistent.parent.name, Is.EqualTo("RuntimeContent"));
        }
        finally
        {
            Object.DestroyImmediate(roomObject);
        }
    }

    [Test]
    public void ClearingTransientRootsKeepsPersistentContent()
    {
        var roomObject = new GameObject("Room");
        try
        {
            RoomRuntimeContent content = roomObject.AddComponent<RoomRuntimeContent>();
            content.EnsureRoots();

            new GameObject("ItemPickup").transform.SetParent(content.PersistentRoot, false);
            new GameObject("Enemy").transform.SetParent(content.EncounterRoot, false);
            new GameObject("TemporaryVfx").transform.SetParent(content.TemporaryRoot, false);

            content.ClearEncounterContent();
            content.ClearTemporaryContent();

            Assert.That(content.PersistentRoot.childCount, Is.EqualTo(1));
            Assert.That(content.EncounterRoot.childCount, Is.Zero);
            Assert.That(content.TemporaryRoot.childCount, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(roomObject);
        }
    }
}
