using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveSkillConfirmationDialog : MonoBehaviour
{
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;

    Action _confirm;

    void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(Hide);
    }

    public void Show(string message, Action confirm)
    {
        _confirm = confirm;
        if (messageText != null)
            messageText.text = message;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _confirm = null;
        gameObject.SetActive(false);
    }

    void Confirm()
    {
        Action callback = _confirm;
        Hide();
        callback?.Invoke();
    }
}
