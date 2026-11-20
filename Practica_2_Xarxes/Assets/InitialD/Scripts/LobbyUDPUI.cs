using UnityEngine;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUDPUI : MonoBehaviour
{
    // =============================================================================================
    // PLAYER - SERVER
    // =============================================================================================

    [SerializeField] private NewSocketsUDPClient client;
    [SerializeField] private NewSocketsUDPServer server;
    [SerializeField] private TMPro.TMP_Text roleText;
    [SerializeField] private TMPro.TMP_InputField nameInput;
    [SerializeField] private TMPro.TMP_InputField ipInput;
    [SerializeField] private TMPro.TMP_Text playersText;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject startRaceButton;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // =============================================================================================
    // CHAT
    // =============================================================================================

    [SerializeField] private GameObject toggleLobbyButton;
    [SerializeField] private GameObject toggleChatButton;
    [SerializeField] private TMPro.TMP_Text chatLog;
    [SerializeField] private ScrollRect chatScroll;
    [SerializeField] private TMPro.TMP_InputField chatInput;
    [SerializeField] private GameObject chatPanel;

    void Start()
    {
        client = NewSocketsUDPClient.Instance;
        server = NewSocketsUDPServer.Instance;

        bool connected = client.IsRunning();

        lobbyPanel.SetActive(!connected);
        toggleLobbyButton.SetActive(connected);
        toggleChatButton.SetActive(connected);
        chatPanel.SetActive(connected);
    }

    public void ToggleLobby()
    {
        lobbyPanel.SetActive(!lobbyPanel.activeSelf);
    }

    public void ToggleChat()
    {
        chatPanel.SetActive(!chatPanel.activeSelf);
    }

    public void SendChatMessage()
    {
        string message = chatInput.text.Trim();
        client.SendString("CHAT:" + client.userName + ": " + message);
    }

    public void JoinGame()
    {
        if (client.IsRunning())
            return;

        toggleChatButton.SetActive(true);

        string playerName = nameInput.text.Trim();
        string ip = ipInput.text.Trim();

        if (string.IsNullOrEmpty(playerName) || playerName.Contains(","))
        {
            Debug.Log("Invalid name (empty or contains a comma)");
            return;
        }

        IPAddress address;

        if (!IPAddress.TryParse(ip, out address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            Debug.Log("Invalid IP");
            return;
        }

        client.isHost = false;
        client.userName = playerName;
        client.serverIp = address.ToString();

        client.StartNetwork();

        lobbyPanel.SetActive(false);
        toggleLobbyButton.SetActive(true);
    }

    void Update()
    {
        if (!client.IsRunning())
        {
            roleText.text = "Not Connected";
            lobbyPanel.SetActive(true);
            toggleLobbyButton.SetActive(false);

            if (client.isHost)
            {
                server.Disconnect();
                client.isHost = false;
            }
        }
        else if (client.isHost)
        {
            roleText.text = "Host";
        }
        else
        {
            roleText.text = "Client";
        }

        playersText.text = "Players:\n" + client.GetPlayerNames();
        UpdateChatLog();

        if (client.isHost && client.IsRunning())
        {
            startRaceButton.SetActive(true);
        }
        else
        {
            startRaceButton.SetActive(false);
        }
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

    public void HostGame()
    {
        if (client.IsRunning())
            return;

        toggleChatButton.SetActive(true);

        string playerName = nameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName) || playerName.Contains(","))
        {
            Debug.Log("Invalid name (empty or contains a comma)");
            return;
        }

        if (!server.StartNetwork())
            return;

        client.isHost = true;
        client.userName = playerName;
        client.serverIp = "127.0.0.1";
        client.port = server.port;

        ipInput.text = "127.0.0.1";

        client.StartNetwork();

        lobbyPanel.SetActive(false);
        toggleLobbyButton.SetActive(true);
    }

    public void LeaveGame()
    {
        toggleChatButton.SetActive(false);
        chatPanel.SetActive(false);

        client.isHost = false;

        client.Disconnect();
        server.Disconnect();

        lobbyPanel.SetActive(true);
        toggleLobbyButton.SetActive(false);
    }

    // =============================================================================================
    // SCENE NAVIGATION
    // =============================================================================================

    public void ReturnToMainMenu()
    {
        Debug.Log("[LOBBY] Returning to Main Menu...");

        LeaveGame();

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // =============================================================================================
    // RACE
    // =============================================================================================

    public void OnClickStartRace()
    {
        client.SendString("START_RACE:");
    }
}