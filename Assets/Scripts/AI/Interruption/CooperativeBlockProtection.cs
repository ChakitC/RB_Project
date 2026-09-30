// A scope releases only its own handles, even if another cinematic also protects an actor.
internal sealed class CooperativeBlockProtection : System.IDisposable
{
    readonly CharacteContext actor;
    readonly HealthSystem health;
    readonly StatusEffectController status;
    readonly CharacterKnockbackMotor knockback;
    int healthToken, statusToken, knockbackToken;
    bool released;
    public CooperativeBlockProtection(CharacteContext actor)
    {
        this.actor = actor;
        health = actor.HealthSystem; status = actor.StatusEffects; knockback = actor.KnockbackMotor;
        healthToken = health != null ? health.AcquireInvincibilityToken() : 0;
        statusToken = status != null ? status.AcquireApplicationBlockToken() : 0;
        knockbackToken = knockback != null ? knockback.AcquireApplicationBlockToken() : 0;
        actor.PushWorldSlowExemption();
    }
    public void Dispose()
    {
        if (released) return;
        released = true;
        if (health != null) health.ReleaseInvincibilityToken(healthToken);
        if (status != null) status.ReleaseApplicationBlockToken(statusToken);
        if (knockback != null) knockback.ReleaseApplicationBlockToken(knockbackToken);
        if (actor != null) actor.PopWorldSlowExemption();
    }
}
