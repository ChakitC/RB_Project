public enum CharacterAction { Move, Dash, FireRequest, Shoot, Reload, Skill, Melee, Rotate }
public enum ActionBlockReason { None, NotInitialized, MissingModule, NotAlive, ControlBlocked, MenuOpen, Busy, Reloading, EmptyMagazine }

public readonly struct ActionAdmissionResult
{
    public ActionBlockReason Reason { get; }
    public bool Allowed => Reason == ActionBlockReason.None;
    public ActionAdmissionResult(ActionBlockReason reason) => Reason = reason;
}
