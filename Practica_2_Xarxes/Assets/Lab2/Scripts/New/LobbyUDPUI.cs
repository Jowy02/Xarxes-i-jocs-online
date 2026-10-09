using UnityEngine;
using System.Net;
using System.Net.Sockets;

public class LobbyUDPUI : MonoBehaviour
{
    [SerializeField] private NewSocketsUDPClient client;
    [SerializeField] private NewSocketsUDPServer server;
    [SerializeField] private TMPro.TMP_Text roleText;
    [SerializeField] private TMPro.TMP_InputField nameInput;
    [SerializeField] private TMPro.TMP_InputField ipInput;
    [SerializeField] private TMPro.TMP_Text playersText;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject toggleLobbyButton;

    private bool isHost = false;

    void Start()
    {
        lobbyPanel.SetActive(true);
        toggleLobbyButton.SetActive(false);
    }

    public void ToggleLobby()
    {
        lobbyPanel.SetActive(!lobbyPanel.activeSelf);
    }
    public void JoinGame()
    {
        if (client.IsRunning())
            return;

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

        isHost = false;
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

            if (isHost)
            {
                server.Disconnect();
                isHost = false;
            }
        }
        else if (isHost)
        {
            roleText.text = "Host";
        }
        else
        {
            roleText.text = "Client";
        }

        playersText.text = "Players:\n" + client.GetPlayerNames();
    }

    public void HostGame()
    {
        if (client.IsRunning()) return;

        string playerName = nameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName) || playerName.Contains(","))
        {
            Debug.Log("Invalid name (empty or contains a comma)");
            return;
        }

        if (!server.StartNetwork()) return;

        isHost = true;
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
        isHost = false;
        client.Disconnect();
        server.Disconnect();
        lobbyPanel.SetActive(true);
        toggleLobbyButton.SetActive(false);
    }
}