using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageExitInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private int priority = 20;
    [SerializeField] private string prompt = "Return to Basement";

    private MapRunController runController;
    private bool used;

    public int Priority => priority;

    public void Configure(MapRunController run)
    {
        runController = run;
        used = false;
    }

    public string GetPrompt(Interactor interactor)
    {
        return prompt;
    }

    public bool CanInteract(Interactor interactor)
    {
        return !used && runController != null && runController.CanCompleteStageRun;
    }

    public void Interact(Interactor interactor)
    {
        if (!CanInteract(interactor))
            return;

        used = true;
        runController.CompleteStageRunAndReturn();
    }
}
