public interface IInteractable
{
    int Priority { get; } // เอาไว้เลือกอันเด่นสุดเวลามีหลายอันบน Link เดียว
    string GetPrompt(Interactor interactor);
    bool CanInteract(Interactor interactor);
    void Interact(Interactor interactor); // แบบกดครั้งเดียว (instant)
}

