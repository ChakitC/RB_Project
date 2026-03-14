using UnityEngine;

public class FollowWorldToScreenUI : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Canvas")]
    public Canvas canvas;
    public Camera uiCamera;      // ใส่เฉพาะ Screen Space - Camera
    public Camera worldCamera;   // กล้องที่มองโลก (ส่วนใหญ่ = Camera.main / กล้อง Cinemachine)

    [Header("Offset")]
    public bool keepInitialOffset = true;   
    private Vector2 _initialOffset;
    private bool _offsetReady;

    [Header("Fade")]
    public GameObject visualRoot; // แนะนำให้เป็น “ลูก” ของ UI ที่มี TMP/Images ทั้งหมด
    public CanvasGroup canvasGroup;
    public float fadeSpeed = 8f;
    public float viewportMargin = 0.02f;

    RectTransform rect;
    RectTransform canvasRect;

    void Awake()
    {
        rect = (RectTransform)transform;

        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (canvas) canvasRect = (RectTransform)canvas.transform;

        if (!visualRoot) visualRoot = gameObject;

        if (!canvasGroup) canvasGroup = visualRoot.GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = visualRoot.AddComponent<CanvasGroup>();

        if (!worldCamera) worldCamera = Camera.main;

        if (canvas && canvas.renderMode == RenderMode.ScreenSpaceCamera && !uiCamera)
            uiCamera = canvas.worldCamera;
    }

    void OnEnable()
    {
        InitOffsetAndSnap(); // กันวาปก่อนเฟรมแรก
    }

    void Start()
    {
        InitOffsetAndSnap(); // กันกรณี target/camera มาทีหลัง
    }

    void LateUpdate()
    {
        if (!target || !canvasRect || !canvas) return;

        var cam = worldCamera ? worldCamera : Camera.main;
        if (!cam) return;

        // 1) เช็คอยู่ในจอไหม (Viewport)
        Vector3 vp = cam.WorldToViewportPoint(target.position);
        bool visible = vp.z > 0f
            && vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin
            && vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin;

        // 2) Fade
        float targetAlpha = visible ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // 3) ตำแหน่งบน Canvas (ถ้าซ่อนสนิทแล้ว จะไม่ต้องคำนวณต่อ)
        if (canvasGroup.alpha <= 0.001f && !visible) return;

        Vector2 targetLocal = GetTargetLocalOnCanvas(cam);

        if (keepInitialOffset && !_offsetReady)
        {
            _initialOffset = rect.anchoredPosition - targetLocal;
            _offsetReady = true;
        }

        rect.anchoredPosition = targetLocal + (keepInitialOffset ? _initialOffset : Vector2.zero);
    }

    void InitOffsetAndSnap()
    {
        _offsetReady = false;

        if (!target || !canvasRect || !canvas) return;

        var cam = worldCamera ? worldCamera : Camera.main;
        if (!cam) return;

        Vector2 targetLocal = GetTargetLocalOnCanvas(cam);

        if (keepInitialOffset)
        {
            _initialOffset = rect.anchoredPosition - targetLocal;
            _offsetReady = true;
        }

        rect.anchoredPosition = targetLocal + (keepInitialOffset ? _initialOffset : Vector2.zero);
    }

    Vector2 GetTargetLocalOnCanvas(Camera worldCam)
    {
        Vector3 screenPos = worldCam.WorldToScreenPoint(target.position);

        Camera camForCanvas = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, camForCanvas, out Vector2 localPos);

        return localPos;
    }
}
