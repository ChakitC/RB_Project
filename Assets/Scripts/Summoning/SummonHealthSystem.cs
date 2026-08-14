using UnityEngine;

public sealed class SummonHealthSystem : HealthSystem
{
    [SerializeField] private SummonedEntityRuntime summonedRuntime;

    protected override void HandleHealthDepleted()
    {
        if (summonedRuntime == null)
        {
            SummonContext context = GetComponentInParent<SummonContext>();
            if (context != null)
            {
                context.ResolveReferences();
                summonedRuntime = context.SummonedRuntime;
            }
        }

        if (summonedRuntime == null)
            summonedRuntime = GetComponentInParent<SummonedEntityRuntime>();

        summonedRuntime?.BeginDespawn(SummonDespawnReason.Killed);
    }
}
