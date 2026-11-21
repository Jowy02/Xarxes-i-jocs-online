
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUDPUI : MonoBehaviour
{
    // NETWORK
    [SerializeField] private NewSocketsUDPClient client;
    [SerializeField] private NewSocketsUDPServer server;
    [SerializeField] private TMPro.TMP_Text roleText;
    [SerializeField] private TMPro.TMP_InputField nameInput;
    [SerializeField] private TMPro.TMP_InputField ipInput;
    [SerializeField] private TMPro.TMP_Text playersText;

    [Header("Role Colors")]
    [SerializeField] private Color disconnectedColor = Color.gray;
    [SerializeField] private Color connectingColor = new Color(1f, 0.65f, 0f);
    [SerializeField] private Color failedColor = Color.red;
    [SerializeField] private Color connectedColor = Color.green;
    [SerializeField] private float connectionTimeout = 5f;

    private bool connectionAttempted = false;
    private float connectionAttemptTime;
    private bool connectionFailed = false;

    // CONNECTION BUTTONS
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject leaveLobbyButton;
    [SerializeField] private GameObject startRaceButton;

    // CHAT
    [SerializeField] private TMPro.TMP_Text chatLog;
    [SerializeField] private ScrollRect chatScroll;
    [SerializeField] private TMPro.TMP_InputField chatInput;
    [SerializeField] private Button sendButton;

    private const string ChatDisabledMessage = "Chat is disabled until you join or create a lobby.";
    private const string ChatWelcomeMessage = "Welcome to your lobby's chat! Say hello!";
    private const string ChatLeftMessage = "You left the lobby! Join or create a new one to use the chat!";
    private const string SystemColor = "#E45B5B";

    private readonly List<string> chatMessages = new List<string>();
    private string[] previousPlayerMessages = new string[0];
    private bool isInLobby = false;

    // SCENE NAVIGATION
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // START / UPDATE
    void Start()
    {
        client = NewSocketsUDPClient.Instance;
        server = NewSocketsUDPServer.Instance;

        if (client == null)
        {
            Debug.LogError("[LOBBY] UDP Client not found!");
            return;
        }

        AddSystemMessage(ChatDisabledMessage);
        UpdateLobbyUI();
        UpdateChatUI();
        UpdateChatLog();
        UpdateRoleText();
    }

    void Update()
    {
        if (client == null) return;

        bool connected = IsInLobby();

        if (connected != isInLobby)
        {
            isInLobby = connected;
            UpdateChatUI();

            if (isInLobby)
            {
                connectionAttempted = false;
                connectionFailed = false;
                AddSystemMessage(ChatWelcomeMessage);
                Debug.Log("[LOBBY] Successfully entered lobby!");
            }
            else
            {
                AddSystemMessage(ChatLeftMessage);
                Debug.Log("[LOBBY] Left lobby.");
            }
        }

        if (connectionAttempted && !isInLobby && !client.IsRunning() && Time.time - connectionAttemptTime >= connectionTimeout)
            connectionFailed = true;

        UpdateLobbyUI();
        playersText.text = client.GetPlayerNames();
        UpdateChatLog();

        startRaceButton.SetActive(isInLobby && client.isHost);
        leaveLobbyButton.SetActive(isInLobby);

        if (!client.IsRunning() && client.isHost)
        {
            if (server != null) server.Disconnect();
            client.isHost = false;
        }

        UpdateRoleText();
    }

    // LOBBY UI
    bool IsInLobby()
    {
        return client != null && client.IsRunning() && client.GetLocalPlayerId() >= 0;
    }

    void UpdateLobbyUI()
    {
        if (client == null) return;

        bool attemptingConnection = client.IsRunning();
        hostButton.interactable = !attemptingConnection;
        joinButton.interactable = !attemptingConnection;
        nameInput.interactable = !attemptingConnection;
        ipInput.interactable = !attemptingConnection;
    }

    void UpdateRoleText()
    {
        if (roleText == null || client == null) return;

        if (isInLobby)
        {
            roleText.text = client.isHost ? "Host" : "Client";
            roleText.color = connectedColor;
        }
        else if (connectionFailed)
        {
            roleText.text = "Connection Failed";
            roleText.color = failedColor;
        }
        else if (connectionAttempted)
        {
            roleText.text = "Connecting...";
            roleText.color = connectingColor;
        }
        else
        {
            roleText.text = "Not Connected";
            roleText.color = disconnectedColor;
        }
    }

    // CHAT UI
    void UpdateChatUI()
    {
        bool enabled = isInLobby;
        chatInput.interactable = enabled;
        sendButton.interactable = enabled;

        if (chatInput.placeholder != null)
        {
            var placeholder = chatInput.placeholder.GetComponent<TMPro.TMP_Text>();
            if (placeholder != null)
                placeholder.text = enabled ? "Type your message..." : "Chat disabled...";
        }

        if (!enabled) chatInput.text = "";
        UpdateChatLog();
    }

    public void SendChatMessage()
    {
        if (!IsInLobby()) return;

        string message = chatInput.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        client.SendString("CHAT:" + client.userName + ": " + message);
        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    // CHAT MESSAGES
    // Adds a system message at the end of the chat history
    void AddSystemMessage(string message)
    {
        chatMessages.Add("<color=" + SystemColor + ">" + EscapeRichText(message) + "</color>");
        UpdateChatLog();
    }

    // Detects new UDP messages and appends them in chronological order
    void UpdatePlayerMessages()
    {
        string currentChat = client.GetChatLog();
        string[] currentMessages = string.IsNullOrEmpty(currentChat) ? new string[0] : currentChat.Split('\n');
        int overlap = 0;
        int maxOverlap = Mathf.Min(previousPlayerMessages.Length, currentMessages.Length);

        for (int i = maxOverlap; i > 0; i--)
        {
            bool matches = true;
            for (int j = 0; j < i; j++)
            {
                if (previousPlayerMessages[previousPlayerMessages.Length - i + j] != currentMessages[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                overlap = i;
                break;
            }
        }

        for (int i = overlap; i < currentMessages.Length; i++)
            chatMessages.Add(EscapeRichText(currentMessages[i]));

        previousPlayerMessages = currentMessages;
    }

    // Refreshes the chat and preserves the player's scroll position
    void UpdateChatLog()
    {
        if (client == null || chatLog == null) return;

        UpdatePlayerMessages();
        string formattedChat = string.Join("\n", chatMessages);
        if (chatLog.text == formattedChat) return;

        bool atBottom = true;
        if (chatScroll != null)
        {
            RectTransform viewport = chatScroll.viewport != null ? chatScroll.viewport : (RectTransform)chatScroll.transform;
            atBottom = chatScroll.content.rect.height <= viewport.rect.height + 1f || chatScroll.verticalNormalizedPosition <= 0.01f;
        }

        chatLog.text = formattedChat;

        if (chatScroll != null && atBottom)
        {
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    // Prevents player messages from interpreting unintended Rich Text tags
    string EscapeRichText(string message)
    {
        return message.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    // JOIN GAME
    public void JoinGame()
    {
        if (client.IsRunning()) return;

        string playerName = nameInput.text.Trim();
        string ip = ipInput.text.Trim();

        if (string.IsNullOrEmpty(playerName) || playerName.Contains(","))
        {
            Debug.Log("[LOBBY] Invalid name (empty or contains a comma).");
            return;
        }

        IPAddress address;
        if (!IPAddress.TryParse(ip, out address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            Debug.Log("[LOBBY] Invalid IP.");
            return;
        }

        client.isHost = false;
        client.userName = playerName;
        client.serverIp = address.ToString();
        connectionAttempted = true;
        connectionFailed = false;
        connectionAttemptTime = Time.time;
        client.StartNetwork();

        UpdateRoleText();
        Debug.Log("[LOBBY] Joining lobby at " + client.serverIp);
    }

    // HOST GAME
    public void HostGame()
    {
        if (client.IsRunning()) return;

        string playerName = nameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName) || playerName.Contains(","))
        {
            Debug.Log("[LOBBY] Invalid name (empty or contains a comma).");
            return;
        }

        if (server == null)
        {
            Debug.LogError("[LOBBY] UDP Server not found!");
            return;
        }

        if (!server.StartNetwork())
        {
            Debug.LogError("[LOBBY] Could not start server.");
            return;
        }

        client.isHost = true;
        client.userName = playerName;
        client.serverIp = "127.0.0.1";
        client.port = server.port;
        ipInput.text = "127.0.0.1";
        connectionAttempted = true;
        connectionFailed = false;
        connectionAttemptTime = Time.time;
        client.StartNetwork();

        UpdateRoleText();
        Debug.Log("[LOBBY] Hosting lobby...");
    }

    // LEAVE GAME
    public void LeaveGame()
    {
        if (client == null) return;

        bool wasInLobby = isInLobby;
        client.isHost = false;
        client.Disconnect();
        if (server != null) server.Disconnect();

        isInLobby = false;
        connectionAttempted = false;
        connectionFailed = false;
        UpdateLobbyUI();
        UpdateChatUI();

        playersText.text = "";
        roleText.text = "Not Connected";
        roleText.color = disconnectedColor;
        startRaceButton.SetActive(false);
        leaveLobbyButton.SetActive(false);

        if (wasInLobby) AddSystemMessage(ChatLeftMessage);
        Debug.Log("[LOBBY] Left lobby.");
    }

    // SCENE NAVIGATION
    public void ReturnToMainMenu()
    {
        Debug.Log("[LOBBY] Returning to Main Menu...");
        LeaveGame();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // RACE
    public void StartRace()
    {
        if (!IsInLobby() || !client.isHost) return;
        client.SendString("START_RACE:");
    }
}