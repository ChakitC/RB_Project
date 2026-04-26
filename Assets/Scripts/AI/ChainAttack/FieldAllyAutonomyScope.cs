using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class FieldAllyAutonomyScope
{
    readonly FieldAllyMember owner;
    readonly FieldAllyTransitionController transitionController;

    MonoBehaviour[] _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
    bool[] _capturedDisabledComponentStates = Array.Empty<bool>();

    bool _autonomyCaptured;
    bool _defaultBehaviorTreeEnabled;
    bool _defaultAgentEnabled;
    bool _defaultAgentIsStopped;
    bool _defaultAgentUpdatePosition;
    bool _defaultAgentUpdateRotation;
    bool _defaultAgentHadPath;
    bool _actorProtectionApplied;
    bool _collisionMaskCaptured;
    bool _rigidbodyStateCaptured;
    int _actorInvincibilityToken;
    int _actorUntargetableToken;
    LayerMask _defaultRigidbodyExcludeLayers;
    LayerMask _defaultCharacterControllerExcludeLayers;
    bool _defaultRigidbodyIsKinematic;
    bool _defaultRigidbodyUseGravity;
    Vector3 _defaultAgentDestination;

    public FieldAllyAutonomyScope(FieldAllyMember owner, FieldAllyTransitionController transitionController)
    {
        this.owner = owner;
        this.transitionController = transitionController;
    }

    public bool IsApplied => _autonomyCaptured;

    public void Apply()
    {
        if (_autonomyCaptured)
            return;

        bool capturedAny = false;

        if (owner.BehaviorTreeRef != null)
        {
            _defaultBehaviorTreeEnabled = owner.BehaviorTreeRef.enabled;
            owner.BehaviorTreeRef.enabled = false;
            capturedAny = true;
        }

        if (owner.AgentRef != null && owner.AgentRef.enabled)
        {
            _defaultAgentEnabled = true;
            _defaultAgentIsStopped = owner.AgentRef.isStopped;
            _defaultAgentUpdatePosition = owner.AgentRef.updatePosition;
            _defaultAgentUpdateRotation = owner.AgentRef.updateRotation;
            _defaultAgentHadPath = owner.AgentRef.hasPath || owner.AgentRef.pathPending;
            _defaultAgentDestination = owner.AgentRef.isOnNavMesh
                ? owner.AgentRef.destination
                : owner.TransformRef.position;

            owner.AgentRef.isStopped = true;
            owner.AgentRef.updatePosition = false;
            owner.AgentRef.updateRotation = false;

            if (owner.AgentRef.isOnNavMesh)
                owner.AgentRef.nextPosition = owner.TransformRef.position;

            owner.AgentRef.enabled = false;
            capturedAny = true;
        }
        else
        {
            _defaultAgentEnabled = false;
            _defaultAgentHadPath = false;
            _defaultAgentDestination = owner.TransformRef.position;
        }

        if (ApplyTemporaryComponentDisables())
            capturedAny = true;

        if (ApplyTemporaryActorProtection())
            capturedAny = true;

        if (ApplyTemporaryNoCollision())
            capturedAny = true;

        if (ApplyTemporaryRigidbodyTeleportControl())
            capturedAny = true;

        if (ApplyPlayerChainLock())
            capturedAny = true;

        _autonomyCaptured = capturedAny;
    }

    public void Restore()
    {
        if (!_autonomyCaptured)
            return;

        bool restoreAgentToDefaultAutonomy = _defaultBehaviorTreeEnabled;

        if (owner.BehaviorTreeRef != null)
            owner.BehaviorTreeRef.enabled = _defaultBehaviorTreeEnabled;

        if (owner.AgentRef != null && _defaultAgentEnabled)
        {
            if (!owner.AgentRef.enabled)
                owner.AgentRef.enabled = true;

            transitionController.SyncAgentToTransform();

            owner.AgentRef.updatePosition = restoreAgentToDefaultAutonomy
                ? true
                : _defaultAgentUpdatePosition;
            owner.AgentRef.updateRotation = restoreAgentToDefaultAutonomy
                ? true
                : _defaultAgentUpdateRotation;
            owner.AgentRef.isStopped = restoreAgentToDefaultAutonomy
                ? false
                : _defaultAgentIsStopped;

            if ((!restoreAgentToDefaultAutonomy && !_defaultAgentIsStopped || restoreAgentToDefaultAutonomy) &&
                _defaultAgentHadPath &&
                owner.AgentRef.isOnNavMesh &&
                !owner.AgentRef.pathPending &&
                !owner.AgentRef.hasPath)
            {
                owner.AgentRef.SetDestination(_defaultAgentDestination);
            }
        }

        _defaultAgentEnabled = false;
        _defaultAgentHadPath = false;
        _defaultAgentDestination = owner.TransformRef.position;

        RestoreTemporaryComponentDisables();
        RestoreTemporaryActorProtection();
        RestoreTemporaryRigidbodyTeleportControl();
        RestoreTemporaryNoCollision();
        RestorePlayerChainLock();
        _autonomyCaptured = false;
    }

    bool ApplyTemporaryActorProtection()
    {
        if (owner.ActorRoleValue == ChainActorRole.Player || _actorProtectionApplied)
            return false;

        if (!owner.MakeAllyInvincibleDuringSequence && !owner.MakeAllyUntargetableDuringSequence)
            return false;

        owner.RefreshCollisionReferences();

        bool applied = false;

        if (owner.MakeAllyInvincibleDuringSequence && owner.ActorHealthSystemRef != null)
        {
            _actorInvincibilityToken = owner.ActorHealthSystemRef.AcquireInvincibilityToken();
            applied = true;
        }

        if (owner.MakeAllyUntargetableDuringSequence && owner.ActorTargetInfoRef != null)
        {
            _actorUntargetableToken = owner.ActorTargetInfoRef.AcquireUntargetableToken();
            applied = true;
        }

        _actorProtectionApplied = applied;
        return applied;
    }

    void RestoreTemporaryActorProtection()
    {
        if (_actorUntargetableToken != 0 && owner.ActorTargetInfoRef != null)
            owner.ActorTargetInfoRef.ReleaseUntargetableToken(_actorUntargetableToken);

        if (_actorInvincibilityToken != 0 && owner.ActorHealthSystemRef != null)
            owner.ActorHealthSystemRef.ReleaseInvincibilityToken(_actorInvincibilityToken);

        _actorUntargetableToken = 0;
        _actorInvincibilityToken = 0;
        _actorProtectionApplied = false;
    }

    bool ApplyTemporaryNoCollision()
    {
        if (!owner.IgnoreCollisionDuringSequence)
            return false;

        owner.RefreshCollisionReferences();

        if (owner.ActorRigidbodyRef == null && owner.ActorCharacterControllerRef == null)
            return false;

        if (!_collisionMaskCaptured)
        {
            _defaultRigidbodyExcludeLayers = owner.ActorRigidbodyRef != null
                ? owner.ActorRigidbodyRef.excludeLayers
                : 0;
            _defaultCharacterControllerExcludeLayers = owner.ActorCharacterControllerRef != null
                ? owner.ActorCharacterControllerRef.excludeLayers
                : 0;
            _collisionMaskCaptured = true;
        }

        if (owner.ActorRigidbodyRef != null)
            owner.ActorRigidbodyRef.excludeLayers = Physics.AllLayers;

        if (owner.ActorCharacterControllerRef != null)
            owner.ActorCharacterControllerRef.excludeLayers = Physics.AllLayers;

        return true;
    }

    void RestoreTemporaryNoCollision()
    {
        if (!_collisionMaskCaptured)
            return;

        if (owner.ActorRigidbodyRef != null)
            owner.ActorRigidbodyRef.excludeLayers = _defaultRigidbodyExcludeLayers;

        if (owner.ActorCharacterControllerRef != null)
            owner.ActorCharacterControllerRef.excludeLayers = _defaultCharacterControllerExcludeLayers;

        _collisionMaskCaptured = false;
    }

    bool ApplyTemporaryRigidbodyTeleportControl()
    {
        owner.RefreshCollisionReferences();

        Rigidbody actorRigidbody = owner.ActorRigidbodyRef;
        if (actorRigidbody == null)
            return false;

        if (!_rigidbodyStateCaptured)
        {
            _defaultRigidbodyIsKinematic = actorRigidbody.isKinematic;
            _defaultRigidbodyUseGravity = actorRigidbody.useGravity;
            _rigidbodyStateCaptured = true;
        }

        actorRigidbody.linearVelocity = Vector3.zero;
        actorRigidbody.angularVelocity = Vector3.zero;
        actorRigidbody.useGravity = false;
        actorRigidbody.isKinematic = true;
        return true;
    }

    void RestoreTemporaryRigidbodyTeleportControl()
    {
        if (!_rigidbodyStateCaptured)
            return;

        Rigidbody actorRigidbody = owner.ActorRigidbodyRef;
        if (actorRigidbody != null)
        {
            actorRigidbody.linearVelocity = Vector3.zero;
            actorRigidbody.angularVelocity = Vector3.zero;
            actorRigidbody.useGravity = _defaultRigidbodyUseGravity;
            actorRigidbody.isKinematic = _defaultRigidbodyIsKinematic;
        }

        _rigidbodyStateCaptured = false;
    }

    bool ApplyTemporaryComponentDisables()
    {
        MonoBehaviour[] components = ResolveComponentsToDisableDuringSequence();
        if (components == null || components.Length == 0)
        {
            _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
            _capturedDisabledComponentStates = Array.Empty<bool>();
            return false;
        }

        _capturedDisabledComponents = components;
        _capturedDisabledComponentStates = new bool[components.Length];

        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null)
                continue;

            _capturedDisabledComponentStates[i] = component.enabled;
            component.enabled = false;
        }

        return true;
    }

    void RestoreTemporaryComponentDisables()
    {
        if (_capturedDisabledComponents == null || _capturedDisabledComponents.Length == 0)
            return;

        int count = Mathf.Min(_capturedDisabledComponents.Length, _capturedDisabledComponentStates.Length);
        for (int i = 0; i < count; i++)
        {
            MonoBehaviour component = _capturedDisabledComponents[i];
            if (component == null)
                continue;

            component.enabled = _capturedDisabledComponentStates[i];
        }

        _capturedDisabledComponents = Array.Empty<MonoBehaviour>();
        _capturedDisabledComponentStates = Array.Empty<bool>();
    }

    MonoBehaviour[] ResolveComponentsToDisableDuringSequence()
    {
        List<MonoBehaviour> resolved = null;

        if (owner.ComponentsToDisableDuringSequenceRef != null &&
            owner.ComponentsToDisableDuringSequenceRef.Length > 0)
        {
            resolved = new List<MonoBehaviour>(owner.ComponentsToDisableDuringSequenceRef.Length);
            for (int i = 0; i < owner.ComponentsToDisableDuringSequenceRef.Length; i++)
            {
                MonoBehaviour component = owner.ComponentsToDisableDuringSequenceRef[i];
                if (component != null && !resolved.Contains(component))
                    resolved.Add(component);
            }
        }

        if (owner.ActorRoleValue == ChainActorRole.Player && owner.AutoDisablePlayerInputAndMovement)
        {
            resolved ??= new List<MonoBehaviour>(2);

            PlayerInputHandler inputHandler = owner.GetComponent<PlayerInputHandler>();
            if (inputHandler != null && !resolved.Contains(inputHandler))
                resolved.Add(inputHandler);

            PlayerMovementCC playerMovement = owner.GetComponent<PlayerMovementCC>();
            if (playerMovement != null && !resolved.Contains(playerMovement))
                resolved.Add(playerMovement);
        }

        return resolved != null && resolved.Count > 0
            ? resolved.ToArray()
            : Array.Empty<MonoBehaviour>();
    }

    bool ApplyPlayerChainLock()
    {
        if (owner.ActorRoleValue != ChainActorRole.Player || owner.ActorContextRef == null)
            return false;

        owner.ActorContextRef.moveInput = Vector2.zero;
        owner.ActorContextRef.lookInput = Vector2.zero;
        owner.ActorContextRef.WeaponSystem?.SetFiring(false);
        owner.ActorContextRef.stateHub?.RequestCanceledFire();
        owner.ActorContextRef.WeaponSystem?.OnAim(false);

        if (owner.ActorContextRef.DashSystem != null && owner.ActorContextRef.DashSystem.IsDashing)
            owner.ActorContextRef.DashSystem.CancelDash();

        return true;
    }

    void RestorePlayerChainLock()
    {
        if (owner.ActorRoleValue != ChainActorRole.Player || owner.ActorContextRef == null)
            return;

        owner.ActorContextRef.moveInput = Vector2.zero;
        owner.ActorContextRef.lookInput = Vector2.zero;
        owner.ActorContextRef.WeaponSystem?.SetFiring(false);
        owner.ActorContextRef.stateHub?.RequestCanceledFire();
        owner.ActorContextRef.WeaponSystem?.OnAim(false);
    }
}
