public enum CharacterAction { Move, Dash, FireRequest, Shoot, Reload, Skill, Melee, Rotate, Sprint, Crouch, Slide, Aim, Block, Chain }
public enum ActionBlockReason { None, NotInitialized, MissingModule, NotAlive, ControlBlocked, MenuOpen, Busy, Reloading, EmptyMagazine }

public readonly struct ActionAdmissionResult
{
    public ActionBlockReason Reason { get; }
    public bool Allowed => Reason == ActionBlockReason.None;
    public ActionCancellation Cancellations { get; }
    public ActionAdmissionResult(ActionBlockReason reason, ActionCancellation cancellations = ActionCancellation.None)
    {
        Reason = reason;
        Cancellations = reason == ActionBlockReason.None ? cancellations : ActionCancellation.None;
    }
}
