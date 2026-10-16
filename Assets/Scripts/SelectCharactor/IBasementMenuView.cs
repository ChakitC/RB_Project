using UnityEngine;

public interface IBasementMenuView
{
    CanvasGroup PresentationGroup { get; }
    CharacterSelectManager CharacterSelection { get; }
    GameObject[] BackgroundRoots { get; }
    RectTransform[] LetterboxBars { get; }
    void PresentationClosing();
    void PresentationClosed();
}
