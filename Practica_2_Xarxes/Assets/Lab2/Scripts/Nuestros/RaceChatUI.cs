using UnityEngine;
using TMPro;

public class RaceChatUI : MonoBehaviour
{
    [SerializeField] private TMP_Text chatLog;
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private GameObject chatPanel;

    private NewSocketsUDPClient client;

    void Start()
    {
        client = FindFirstObjectByType<NewSocketsUDPClient>();
        chatPanel.SetActive(false);
        chatInput.onSubmit.AddListener(_ => SendChatMessage()); // Enter envía
    }

    void Update()
    {
        if (client == null) return;
        chatLog.text = "Chat:\n" + client.GetChatLog();
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