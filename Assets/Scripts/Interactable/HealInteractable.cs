using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InteractableLink))]
public class HealInteractable : MonoBehaviour, IInteractable
{
    enum HealRecipientMode
    {
        InteractorOnly,
        InteractorAndAllies
    }

    [Header("Interaction")]
    [SerializeField] private int priority;
    [SerializeField] private string prompt = "Heal";

    [Header("Heal")]
    [SerializeField, Min(0f)] private float healAmount = 25f;
    [SerializeField] private bool healToFull;
    [SerializeField] private bool healByMaxHealthPercent;
    [SerializeField, Range(0f, 1f)] private float maxHealthPercent = 0.5f;
    [SerializeField] private HealRecipientMode recipientMode = HealRecipientMode.InteractorOnly;

    [Header("After Use")]
    [SerializeField] private bool deactivateAfterUse = true;
    [SerializeField] private GameObject objectToDeactivate;

    public int Priority => priority;

    public void ConfigurePartyPercentHeal(float percent, bool oneUse = true)
    {
        healToFull = false;
        healByMaxHealthPercent = true;
        maxHealthPercent = Mathf.Clamp01(percent);
        recipientMode = HealRecipientMode.InteractorAndAllies;
        deactivateAfterUse = oneUse;
    }

    public string GetPrompt(Interactor interactor)
    {
        return prompt;
    }

    public bool CanInteract(Interactor interactor)
    {
        List<HealthSystem> healthSystems = ResolveTargetHealths(interactor);
        for (int i = 0; i < healthSystems.Count; i++)
        {
            HealthSystem health = healthSystems[i];
            if (health != null && health.CanHeal(GetHealAmount(health)))
                return true;
        }

        return false;
    }

    public void Interact(Interactor interactor)
    {
        List<HealthSystem> healthSystems = ResolveTargetHealths(interactor);
        bool healedAny = false;
        for (int i = 0; i < healthSystems.Count; i++)
        {
            HealthSystem health = healthSystems[i];
            if (health != null && health.Heal(GetHealAmount(health)))
                healedAny = true;
        }

        if (healedAny)
            DeactivateIfNeeded();
    }

    float GetHealAmount(HealthSystem health)
    {
        if (health == null)
            return 0f;

        if (healToFull)
            return Mathf.Max(0f, health.maximumHealth - health.currentHealth);

        if (healByMaxHealthPercent)
            return Mathf.Max(0f, health.maximumHealth * maxHealthPercent);

        return healAmount;
    }

    List<HealthSystem> ResolveTargetHealths(Interactor interactor)
    {
        List<HealthSystem> healthSystems = new();
        HashSet<int> seenContexts = new();
        HashSet<int> seenHealths = new();

        if (interactor == null)
            return healthSystems;

        CharacteContext context = interactor.OwnerContext;
        if (context != null)
        {
            AddContextHealth(healthSystems, seenContexts, seenHealths, context);
            if (recipientMode == HealRecipientMode.InteractorAndAllies && context is PlayerContext playerContext)
                AddPlayerAllies(healthSystems, seenContexts, seenHealths, playerContext);

            return healthSystems;
        }

        AddHealth(healthSystems, seenHealths, interactor.GetComponentInParent<HealthSystem>());
        AddHealth(healthSystems, seenHealths, interactor.GetComponentInChildren<HealthSystem>(true));
        return healthSystems;
    }

    void AddPlayerAllies(
        List<HealthSystem> healthSystems,
        HashSet<int> seenContexts,
        HashSet<int> seenHealths,
        PlayerContext playerContext)
    {
        if (playerContext == null)
            return;

        AddHelperAlly(healthSystems, seenContexts, seenHealths, playerContext.allyHelper);
        AddFieldAllies(healthSystems, seenContexts, seenHealths, playerContext.fieldAllyManager);
    }

    void AddHelperAlly(
        List<HealthSystem> healthSystems,
        HashSet<int> seenContexts,
        HashSet<int> seenHealths,
        AllyHelperManager helperManager)
    {
        GameObject helperObject = helperManager != null ? helperManager.HelperObject : null;
        if (helperObject == null)
            return;

        AddContextHealth(
            healthSystems,
            seenContexts,
            seenHealths,
            helperObject.GetComponentInChildren<AllyContext>(true));
    }

    void AddFieldAllies(
        List<HealthSystem> healthSystems,
        HashSet<int> seenContexts,
        HashSet<int> seenHealths,
        FieldAllyManager fieldAllyManager)
    {
        if (fieldAllyManager == null)
            return;

        foreach (FieldAllyMember member in fieldAllyManager.RegisteredMembers)
        {
            if (member == null)
                continue;

            AddContextHealth(healthSystems, seenContexts, seenHealths, member.ActorContext);
        }
    }

    void AddContextHealth(
        List<HealthSystem> healthSystems,
        HashSet<int> seenContexts,
        HashSet<int> seenHealths,
        CharacteContext context)
    {
        if (context == null)
            return;

        int contextId = context.GetInstanceID();
        if (!seenContexts.Add(contextId))
            return;

        context.ResolveReferences();
        HealthSystem health = context.HealthSystem;
        if (health == null)
        {
            health = context.GetComponentInChildren<HealthSystem>(true);
            if (health != null)
                context.HealthSystem = health;
        }

        AddHealth(healthSystems, seenHealths, health);
    }

    void AddHealth(List<HealthSystem> healthSystems, HashSet<int> seenHealths, HealthSystem health)
    {
        if (health == null)
            return;

        int healthId = health.GetInstanceID();
        if (seenHealths.Add(healthId))
            healthSystems.Add(health);
    }

    void DeactivateIfNeeded()
    {
        if (!deactivateAfterUse)
            return;

        GameObject target = objectToDeactivate != null ? objectToDeactivate : gameObject;
        target.SetActive(false);
    }
}
