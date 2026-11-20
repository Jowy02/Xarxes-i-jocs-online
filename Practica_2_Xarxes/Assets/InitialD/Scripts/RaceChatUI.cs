using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RaceChatUI : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text chatLog;
    [SerializeField] private ScrollRect chatScroll;
    [SerializeField] private TMPro.TMP_InputField chatInput;
    [SerializeField] private GameObject chatPanel;

    private NewSocketsUDPClient client;

    void Start()
    {
        client = FindFirstObjectByType<NewSocketsUDPClient>();
        chatPanel.SetActive(false);
        chatInput.onSubmit.AddListener(_ => SendChatMessage());
    }

    void Update()
    {
        if (client == null) return;
        UpdateChatLog();
    }

    // Refreshes the chat text and keeps the view at the bottom, unless the player has scrolled up
    void UpdateChatLog()
    {
        string text = "Chat:\n" + client.GetChatLog();
        if (chatLog.text == text) return;

        bool atBottom = true;

        if (chatScroll != null)
        {
            RectTransform viewport = chatScroll.viewport != null ? chatScroll.viewport : (RectTransform)chatScroll.transform;
            atBottom = chatScroll.content.rect.height <= viewport.rect.height + 1f
                       || chatScroll.verticalNormalizedPosition <= 0.01f;
        }

        chatLog.text = text;

        if (chatScroll != null && atBottom)
        {
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    public void ToggleChat()
    {
        chatPanel.SetActive(!chatPanel.activeSelf);
    }

    public void SendChatMessage()
    {
        string message = chatInput.text.Trim();
        if (client == null || string.IsNullOrEmpty(message)) return;

        client.SendString("CHAT:" + client.userName + ": " + message);
        chatInput.text = "";
        chatInput.ActivateInputField();
    }
}