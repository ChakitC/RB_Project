public interface IHoldInteractable
{
    float HoldDuration { get; }
    void BeginHold(Interactor interactor);
    void CancelHold(Interactor interactor);
    void CompleteHold(Interactor interactor);
}