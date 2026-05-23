using UnityEngine;

[DisallowMultipleComponent]
public class RoomExitInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("ลำดับความสำคัญเมื่อ Interactor เจอ interactable หลายตัวพร้อมกัน")]
    [SerializeField] private int priority = 10;
    [SerializeField] private RoomExitDirection direction = RoomExitDirection.Up;

    [Tooltip("ข้อความ prompt เมื่อประตูพร้อมใช้งาน ใส่ {0} เพื่อแทนชื่อห้องปลายทาง")]
    [SerializeField] private string promptFormat = "Enter {0}";
    [SerializeField] private string returnPromptFormat = "Return to {0}";

    [Tooltip("ข้อความ prompt เมื่อประตูถูกล็อกระหว่าง encounter")]
    [SerializeField] private string lockedPrompt = "Exit Locked";

    [Tooltip("ข้อความ prompt เมื่อประตูยังไม่มี node ปลายทางหรือเดินทางไม่ได้")]
    [SerializeField] private string unavailablePrompt = "Unavailable";

    [Tooltip("root ของ visual ประตูที่ต้องการซ่อนเมื่อ exit socket นี้ไม่ได้ถูกใช้")]
    [SerializeField] private GameObject visualRoot;

    private MapRunController runController;
    private RoomController roomController;
    private string targetNodeId;
    private string targetName;
    private bool configured;
    private bool visible = true;
    private bool isReturnExit;
    private RoomExitDirection activeDirection;
    private bool hasActiveDirection;

    public int Priority => priority;
    public RoomExitDirection AuthoredDirection => direction;
    public RoomExitDirection Direction => hasActiveDirection ? activeDirection : direction;
    public string TargetNodeId => targetNodeId;

    public void ApplyRoomRotation(int rotationSteps)
    {
        activeDirection = RoomExitDirectionUtility.Rotate(direction, rotationSteps);
        hasActiveDirection = true;
    }

    public void Configure(MapRunController run, RoomController room, MapNode targetNode, bool isReturnExit = false)
    {
        runController = run;
        roomController = room;
        targetNodeId = targetNode != null ? targetNode.Id : null;
        targetName = targetNode != null ? targetNode.GetDisplayName() : string.Empty;
        configured = targetNode != null;
        this.isReturnExit = configured && isReturnExit;
        SetVisible(configured);
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (visualRoot != null)
            visualRoot.SetActive(value);
        else
            gameObject.SetActive(value);
    }

    public string GetPrompt(Interactor interactor)
    {
        if (!visible || !configured)
            return unavailablePrompt;

        if (roomController != null && roomController.ExitsLocked)
            return lockedPrompt;

        if (runController == null || !runController.CanTravelTo(targetNodeId, Direction))
            return unavailablePrompt;

        string format = isReturnExit ? returnPromptFormat : promptFormat;
        return string.Format(format, string.IsNullOrWhiteSpace(targetName) ? "Room" : targetName);
    }

    public bool CanInteract(Interactor interactor)
    {
        return visible &&
               configured &&
               roomController != null &&
               !roomController.ExitsLocked &&
               runController != null &&
               runController.CanTravelTo(targetNodeId, Direction);
    }

    public void Interact(Interactor interactor)
    {
        if (!CanInteract(interactor))
            return;

        runController.RequestTravelTo(targetNodeId, Direction);
    }
}
