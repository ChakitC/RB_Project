using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] PlayerContext ctx;
    [Header("Chain Attack")]
    [SerializeField] SkillChainDef chainAttackDefinition;
    [Header("Melee Soft Target")]
    [SerializeField] float meleeSearchDistance = 4f;

    bool _chainConsumedInteractPress;
    bool _blockPressed;
    bool _dashNeedsRelease;
    InputAction _dashAction;
    InputAction _crouchAction;

    /// <summary>
    /// The chain the ChainReady [F] press would run. Exposed so the on-enemy prompt can ask whether
    /// that press would actually be accepted, instead of always advertising itself as available.
    /// </summary>
    public SkillChainDef ChainAttackDefinition => chainAttackDefinition;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable() => ResolveReferences();

    void OnDisable()
    {
        ctx?.movement?.CancelSprint();
        ctx?.movement?.ResetCrouchInput();
        _dashNeedsRelease = true;
        _chainConsumedInteractPress = false;
        _blockPressed = false;
    }

    void ResolveReferences()
    {
        if (!ctx)
            ctx = GetComponentInParent<PlayerContext>();

        ctx?.ResolveReferences();
        if (_dashAction == null)
        {
            var input = GetComponentInParent<PlayerInput>();
            if (input != null && input.actions != null)
                _dashAction = input.actions.FindAction("Dash", false);
        }
        if (_crouchAction == null)
        {
            var input = GetComponentInParent<PlayerInput>();
            if (input != null && input.actions != null)
                _crouchAction = input.actions.FindAction("Crouch", false);
        }
    }

    void Update()
    {
        if (_crouchAction != null && _crouchAction.enabled && !_crouchAction.IsPressed())
            ctx?.movement?.SetCrouchHeld(false);
        // The key may have been released while this input component was disabled.
        if (_dashNeedsRelease && _dashAction != null && _dashAction.enabled && !_dashAction.IsPressed())
        {
            _dashNeedsRelease = false;
            ctx?.movement?.SetDashHeld(false);
        }
    }
   
    public void OnMove(InputAction.CallbackContext c)
    {
        if (ctx == null) return;

        ctx.moveInput = c.ReadValue<Vector2>();

    }
    public void OnLook(InputAction.CallbackContext c)
    {
        if (ctx == null) return;

        ctx.lookInput = c.ReadValue<Vector2>();
    }
    public void OnDash(InputAction.CallbackContext c)
    {
        ResolveReferences();
        _dashAction = c.action;
        if (c.canceled)
        {
            _dashNeedsRelease = false;
            ctx?.movement?.SetDashHeld(false);
        }
        if (!c.performed || !c.ReadValueAsButton() || _dashNeedsRelease) return;
        if (ctx?.movement != null) ctx.movement.SetDashHeld(true);
        else ctx?.stateHub?.RequestOnDash();
    }

    public void OnAim(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed) ctx?.WeaponSystem?.OnAim(true);
        if (c.canceled) ctx?.WeaponSystem?.OnAim(false);
        
    }

    public void OnCrouch(InputAction.CallbackContext c)
    {
        ResolveReferences();
        _crouchAction = c.action;
        if (c.canceled) ctx?.movement?.SetCrouchHeld(false);
        else if (c.performed && c.ReadValueAsButton()) ctx?.movement?.SetCrouchHeld(true);
    }

    public void OnMelee(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (!c.performed)
            return;

        if (ctx == null || ctx.stateHub == null || !ctx.stateHub.CanStartMelee()) return;
        ThirdPersonTargetingUtility.FacePlayerTowardSoftTarget(
            ctx,
            actionRange: meleeSearchDistance);
        ctx?.stateHub?.RequestMeleePress();
    }

    public void OnFire(InputAction.CallbackContext c)
    {
        ResolveReferences();

        if (c.performed)
        {
            ctx?.stateHub?.RequestOnFire();
           
        }

        if (c.canceled)
        {

            ctx?.stateHub?.RequestCanceledFire();
        }
    }

    public void OnInterrace(InputAction.CallbackContext c)
    {
        ResolveReferences();

        if (c.performed)
        {
            if (ctx?.partyCommand != null &&
                ctx.partyCommand.TryExecuteChainReadyChainAttack(chainAttackDefinition)
                    == PartyCommandController.ChainReadyInputResult.Consumed)
            {
                _chainConsumedInteractPress = true;
                return;
            }

            ctx?.Interactor?.InteractPressed();
        }

        if (c.canceled)
        {
            if (_chainConsumedInteractPress)
            {
                _chainConsumedInteractPress = false;
                return;
            }

            ctx?.Interactor?.InteractReleased();
        }
    }

    public void OnAllyHelperCall(InputAction.CallbackContext c)
    {
        if (!c.performed)
            return;

        ResolveReferences();
        if (ctx == null)
            return;

        if (ctx.partyCommand != null && ctx.partyCommand.HasPartyCommands)
        {
            ctx.partyCommand.TryExecuteSelectedPartyCommand();
            return;
        }

        ctx.allyHelper?.SummonAllyHelper();
    }

    public void OnPartyCommandNext(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.SelectNextPartyCommand();
    }

    public void OnPartyCommandPrevious(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.SelectPreviousPartyCommand();
    }

    public void OnPartyCommandSlot1(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(0);
    }

    public void OnPartyCommandSlot2(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(1);
    }

    public void OnPartyCommandSlot3(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(2);
    }

    public void OnPartyCommandSlot4(InputAction.CallbackContext c)
    {
        ResolveReferences();
        if (c.performed && ctx?.partyCommand != null)
            ctx.partyCommand.TrySelectPartyCommandSlot(3);
    }

    public void OnPartyFollowUpSlot1(InputAction.CallbackContext c)
    {
        TryConsumePartyFollowUp(c, ChainActorRole.PartySlot1);
    }

    public void OnPartyFollowUpSlot2(InputAction.CallbackContext c)
    {
        TryConsumePartyFollowUp(c, ChainActorRole.PartySlot2);
    }

    public void OnPartyFollowUpHelper(InputAction.CallbackContext c)
    {
        TryConsumePartyFollowUp(c, ChainActorRole.Helper);
    }

    void TryConsumePartyFollowUp(InputAction.CallbackContext context, ChainActorRole role)
    {
        if (!context.performed)
            return;

        ResolveReferences();
        PartyFollowUpOpportunityController controller = ctx?.partyFollowUpController;
        if (controller == null || !controller.TryGetOffer(role, out PartyFollowUpOfferViewData offer))
            return;

        controller.TryConsumeOffer(role, offer.OfferId, out _);
    }

    public void OnSkillSlot1(InputAction.CallbackContext c) => TryCastSkillSlot(c, 0);
    public void OnSkillSlot2(InputAction.CallbackContext c) => TryCastSkillSlot(c, 1);
    public void OnSkillSlot3(InputAction.CallbackContext c) => TryCastSkillSlot(c, 2);

    void TryCastSkillSlot(InputAction.CallbackContext c, int slotIndex)
    {
        if (!c.performed)
            return;

        ResolveReferences();
        ctx?.SkillManager?.TryStartCastSlot(slotIndex);
    }

    public void OnInterruptionCommand(InputAction.CallbackContext c)
    {
        if (c.canceled) { _blockPressed = false; return; }
        if (!c.performed || _blockPressed || !c.ReadValueAsButton()) return;
        _blockPressed = true;
        ResolveReferences();
        if (ctx?.interruptionCommand != null)
            ctx.interruptionCommand.BlockBindingLabel = c.action.GetBindingDisplayString();
        if (GlobalTimeScaleManager.Instance.IsPaused) return;
        ctx?.interruptionCommand?.TryExecuteBlockInput(c.time);
    }

    public void OnReload(InputAction.CallbackContext c)
    {
        if (!c.performed) return;
        ResolveReferences();
        ctx?.stateHub?.RequestReload();
    }

    public void OpenInventory(InputAction.CallbackContext c)
    {
        
        if (!c.performed) return;
        ResolveReferences();
        if (ctx?.playerUIContext?.inventoryUI == null)
            return;

        bool isOpen = ctx.playerUIContext.inventoryUI.activeSelf;
        ctx.CancelSprint();
        ctx.movement?.ResetCrouchInput();
        ctx.playerUIContext.inventoryUI.SetActive(!isOpen);

     
    
    }

    public void OpenChainSkill(InputAction.CallbackContext c)
    {
        if (!c.performed)
            return;

        ResolveReferences();
        if (ctx?.partyCommand == null)
            return;

        ctx.partyCommand.TryExecuteChainAttack(chainAttackDefinition);
    }
}
