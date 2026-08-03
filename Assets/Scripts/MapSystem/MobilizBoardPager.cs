using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MobilizBoardPager : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField, Min(0)] private int initialPage;

    private int currentPage;

    void Awake()
    {
        currentPage = Mathf.Clamp(initialPage, 0, Mathf.Max(0, PageCount - 1));
        Refresh();
    }

    public void ShowPreviousPage()
    {
        SetPage(currentPage - 1);
    }

    public void ShowNextPage()
    {
        SetPage(currentPage + 1);
    }

    public void SetPage(int pageIndex)
    {
        int clamped = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, PageCount - 1));
        if (clamped == currentPage)
        {
            RefreshButtons();
            return;
        }

        currentPage = clamped;
        Refresh();
    }

    void Refresh()
    {
        if (pages != null)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                    pages[i].SetActive(i == currentPage);
            }
        }

        RefreshButtons();
    }

    void RefreshButtons()
    {
        if (previousButton != null)
            previousButton.interactable = currentPage > 0;
        if (nextButton != null)
            nextButton.interactable = currentPage < PageCount - 1;
    }

    int PageCount => pages != null ? pages.Length : 0;
}
