using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class RaceChatUI : MonoBehaviour
{
    [SerializeField] private TMP_Text chatLog;
    [SerializeField] private ScrollRect chatScroll;
    [SerializeField] private TMP_InputField chatInput;

    private NewSocketsUDPClient client;
    private string previousChatText = "";
    private bool chatInputActive = false;

    void Start()
    {
        client = FindFirstObjectByType<NewSocketsUDPClient>();

        if (chatInput != null)
        {
            chatInput.onSubmit.AddListener(OnChatSubmit);
            chatInput.interactable = false;
            chatInput.DeactivateInputField();
        }
    }

    void Update()
    {
        if (client == null) return;

        UpdateChatLog();
    }

    void UpdateChatLog()
    {
        if (chatLog == null) return;

        string text = "Chat:\n" + client.GetChatLog();

        if (previousChatText == text) return;

        bool atBottom = true;

        if (chatScroll != null && chatScroll.content != null)
        {
            RectTransform viewport = chatScroll.viewport != null
                ? chatScroll.viewport
                : (RectTransform)chatScroll.transform;

            atBottom =
                chatScroll.content.rect.height <= viewport.rect.height + 1f ||
                chatScroll.verticalNormalizedPosition <= 0.01f;
        }

        chatLog.text = text;
        previousChatText = text;

        if (atBottom)
            StartCoroutine(ScrollToBottom());
    }

    IEnumerator ScrollToBottom()
    {
        yield return null;

        Canvas.ForceUpdateCanvases();

        if (chatScroll != null)
        {
            chatScroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }
    }

    // CHAT INPUT CONTROL

    public void SetChatInputActive(bool active)
    {
        chatInputActive = active;

        if (chatInput == null) return;

        chatInput.interactable = active;

        if (active)
        {
            chatInput.ActivateInputField();
            chatInput.Select();
        }
        else
        {
            chatInput.DeactivateInputField();

            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == chatInput.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    void OnChatSubmit(string value)
    {
        if (!chatInputActive) return;

        SendChatMessage();
    }

    public void SendChatMessage()
    {
        if (!chatInputActive || chatInput == null || client == null)
            return;

        string message = chatInput.text.Trim();

        if (string.IsNullOrEmpty(message))
            return;

        client.SendString("CHAT:" + client.userName + ": " + message);

        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    void OnDestroy()
    {
        if (chatInput != null)
            chatInput.onSubmit.RemoveListener(OnChatSubmit);
    }
}