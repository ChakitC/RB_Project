using NUnit.Framework;
using UnityEngine;

public sealed class PlayerTargetingLifecycleTests
{
    [Test]
    public void DisableEnable_InvalidatesOldLifeHandle_AndRegistryDoesNotDuplicate()
    {
        GameObject actor = new GameObject("Pooled Target");

        try
        {
            EnemyContext context = actor.AddComponent<EnemyContext>();
            int firstGeneration = context.LifeGeneration;
            SkillTargetHandle firstLife = SkillTargetHandle.For(context);

            Assert.That(CharacterContextRegistry.ActiveContexts, Has.Member(context));
            Assert.That(firstLife.TryResolveLiveContext(out CharacteContext resolved), Is.True);
            Assert.That(resolved, Is.SameAs(context));

            actor.SetActive(false);
            Assert.That(CharacterContextRegistry.ActiveContexts, Has.No.Member(context));
            Assert.That(firstLife.TryResolveLiveContext(out _), Is.False);

            actor.SetActive(true);
            Assert.That(context.LifeGeneration, Is.GreaterThan(firstGeneration));
            Assert.That(firstLife.TryResolveLiveContext(out _), Is.False);

            int registrations = 0;
            for (int i = 0; i < CharacterContextRegistry.ActiveContexts.Count; i++)
            {
                if (CharacterContextRegistry.ActiveContexts[i] == context)
                    registrations++;
            }

            Assert.That(registrations, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void ReticleScore_RejectsPointsBehindCamera()
    {
        GameObject cameraObject = new GameObject("Targeting Test Camera");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            Assert.That(
                ThirdPersonTargetingUtility.TryGetReticleScore(
                    camera,
                    camera.transform.position - camera.transform.forward * 5f,
                    out _),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void LineOfSight_FailsClosedWhenNonAllocBufferIsFull()
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject target = new GameObject("Target Root");

        try
        {
            obstacle.transform.position = new Vector3(0f, 0f, 2f);
            target.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            bool visible = ThirdPersonTargetingUtility.HasLineOfSightNonAlloc(
                Vector3.zero,
                target.transform.position,
                target.transform,
                ~0,
                new RaycastHit[1]);

            Assert.That(visible, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(obstacle);
            Object.DestroyImmediate(target);
        }
    }
}
