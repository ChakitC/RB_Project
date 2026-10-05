using UnityEngine;

public partial class PlayerMovementCC
{
    [Header("Held Crouch")]
    [SerializeField, Range(0.1f, 1f)] private float crouchSpeedMultiplier = 0.45f;
    [SerializeField, Range(0.1f, 1f)] private float crouchHeightMultiplier = 0.6f;
    private readonly Collider[] standHits = new Collider[32];
    private bool crouchHeld;
    private bool crouchNeedsRelease;
    private CharacterController crouchController;
    private float standingHeight, standingStepOffset, loweredHeight, loweredStepOffset;
    private Vector3 standingCenter, loweredCenter;
    private bool ownsCrouchBody;
    private bool crouchBodyOverridden;
    public bool IsCrouching => ownsCrouchBody && CanUsePlayerLocomotion();

    public void SetCrouchHeld(bool held)
    {
        if (!held)
        {
            crouchHeld = false;
            crouchNeedsRelease = false;
            if (IsSliding) ReleaseSlide();
            TryRestoreCrouchBody();
            crouchBodyOverridden = false;
            return;
        }
        if (crouchHeld || crouchNeedsRelease || !isActiveAndEnabled) return;
        crouchHeld = true;
        TryBeginSlide();
        CancelSprint();
        if (CanUsePlayerLocomotion()) LowerCrouchBody();
    }

    public void ResetCrouchInput()
    {
        crouchHeld = false;
        crouchNeedsRelease = true;
        CancelSlide();
        TryRestoreCrouchBody();
    }

    private void UpdateCrouchAndSlide()
    {
        if (stateHub == null) return;
        var facts = stateHub.GetActionFacts();
        if (GlobalTimeScaleManager.Instance.IsPaused || facts.UI != UIStateId.Normal)
        {
            ResetCrouchInput();
            return;
        }
        if (!CanUsePlayerLocomotion())
        {
            CancelSlide();
            TryRestoreCrouchBody();
            return;
        }
        if (IsSliding && !CanSlide()) CancelSlide();
        if (crouchHeld) LowerCrouchBody();
        else TryRestoreCrouchBody();
    }

    private bool StillOwnsCrouchBody()
    {
        if (!ownsCrouchBody) return false;
        if (crouchController != null && Mathf.Approximately(crouchController.height, loweredHeight) &&
            (crouchController.center - loweredCenter).sqrMagnitude < 0.000001f &&
            Mathf.Approximately(crouchController.stepOffset, loweredStepOffset)) return true;
        // Another shape owner has taken over. Never restore our stale snapshot over it.
        ownsCrouchBody = false;
        crouchBodyOverridden = true;
        return false;
    }

    private void LowerCrouchBody()
    {
        if (StillOwnsCrouchBody() || crouchBodyOverridden || _characterController == null) return;
        crouchController = _characterController;
        standingHeight = crouchController.height;
        standingCenter = crouchController.center;
        standingStepOffset = crouchController.stepOffset;
        loweredHeight = Mathf.Max(crouchController.radius * 2f, standingHeight * crouchHeightMultiplier);
        loweredCenter = standingCenter - Vector3.up * ((standingHeight - loweredHeight) * 0.5f);
        loweredStepOffset = Mathf.Min(standingStepOffset, loweredHeight);
        crouchController.height = loweredHeight;
        crouchController.center = loweredCenter;
        crouchController.stepOffset = loweredStepOffset;
        ownsCrouchBody = true;
    }

    public bool CanStandForDash() => !StillOwnsCrouchBody() || HasStandingClearance();

    public void PrepareForAcceptedDash()
    {
        CancelSlide();
        TryRestoreCrouchBody();
    }

    private bool HasStandingClearance()
    {
        var cc = crouchController;
        Vector3 scale = cc.transform.lossyScale;
        float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(radius * 2f, standingHeight * Mathf.Abs(scale.y));
        Vector3 center = cc.transform.TransformPoint(standingCenter);
        float halfLine = height * 0.5f - radius;
        // Slightly shrink the query so ordinary floor contact does not prevent standing.
        int count = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * halfLine,
            center + Vector3.up * halfLine, Mathf.Max(0.001f, radius - 0.01f), standHits,
            Physics.DefaultRaycastLayers & ~cc.excludeLayers.value, QueryTriggerInteraction.Ignore);
        if (count == standHits.Length) return false;
        Transform root = actorRoot ? actorRoot : transform;
        for (int i = 0; i < count; i++)
        {
            var hit = standHits[i];
            if (CharacterBodySweepUtility.IsOwnedByActor(hit.transform, root) ||
                Physics.GetIgnoreLayerCollision(cc.gameObject.layer, hit.gameObject.layer) ||
                Physics.GetIgnoreCollision(cc, hit)) continue;
            return false;
        }
        return true;
    }

    private void TryRestoreCrouchBody()
    {
        if (!StillOwnsCrouchBody() || !HasStandingClearance()) return;
        crouchController.height = standingHeight;
        crouchController.center = standingCenter;
        crouchController.stepOffset = standingStepOffset;
        ownsCrouchBody = false;
    }
}
