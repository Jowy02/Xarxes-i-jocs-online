using UnityEngine;
using System.Net;
using System.Net.Sockets;

public class LobbyUDPUI : MonoBehaviour
{
    [SerializeField] private NewSocketsUDPClient client;
    [SerializeField] private NewSocketsUDPServer server;
    [SerializeField] private TMPro.TMP_InputField nameInput;
    [SerializeField] private TMPro.TMP_InputField ipInput;
    [SerializeField] private TMPro.TMP_Text playersText;

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

        client.userName = playerName;
        client.serverIp = address.ToString();
        client.StartNetwork();
    }

    void Update()
    {
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

        client.userName = playerName;
        client.serverIp = "127.0.0.1";
        client.port = server.port;

        ipInput.text = "127.0.0.1";
        client.StartNetwork();
    }

    public void LeaveGame()
    {
        client.Disconnect();
        server.Disconnect();
    }
}