using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveSkillTreeConnectionView : MonoBehaviour
{
    [SerializeField] Image image;

    public void Bind(Vector2 from, Vector2 to, bool active, SkillScreenTheme theme)
    {
        if (transform is not RectTransform rect)
            return;

        Vector2 delta = to - from;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = from;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        rect.sizeDelta = new Vector2(delta.magnitude, Mathf.Max(3f, rect.sizeDelta.y));

        if (image != null && theme != null)
        {
            if (theme.connectionSprite != null)
                image.sprite = theme.connectionSprite;
            image.color = active ? theme.activeConnectionColor : theme.lockedConnectionColor;
        }
    }
}
