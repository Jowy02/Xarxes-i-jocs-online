using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using static UDPPlayer;

public class NewSocketsUDPClient : MonoBehaviour
{
    public string serverIp = "127.0.0.1";
    public int port = 9050;
    public string userName = "Player";
    public bool autoStart = true;
    public bool showDebugUI = true;

    private List<string> m_players = new List<string>();

    const int MaxPacketSize = 64 * 1024;

    Socket m_socket;
    EndPoint m_serverEndPoint;

    readonly List<Thread> m_threads = new List<Thread>();
    readonly ConcurrentQueue<byte[]> m_inbox = new ConcurrentQueue<byte[]>();
    volatile bool m_running;

    // Minigame variables
    float m_pingTimer = 0f;
    readonly Dictionary<string, GameObject> m_playerCapsules = new Dictionary<string, GameObject>();
    private float m_lastServerResponse;
    UDPPlayer.PlayerDTO m_playerDTO;
    // =============================================================================================
    // START / STOP
    // =============================================================================================

    void Start()
    {
        Application.runInBackground = true;
        if (autoStart) StartNetwork();
    }

    public void StartNetwork()
    {
        if (m_running) return;
        m_running = true;
        m_lastServerResponse = Time.realtimeSinceStartup;
        m_pingTimer = 0f;

        Thread t = new Thread(ClientThread);
        t.IsBackground = true;
        m_threads.Add(t);
        t.Start();
    }

    public void Disconnect()
    {
        if (!m_running) return;

        // By sending Leave here before closing the socket, we can skip the wainting timeout
        SendString("LEAVE:");
        m_running = false;

        if (m_socket != null)
        {
            try { m_socket.Close(); } catch { }
            m_socket = null;
        }

        foreach (GameObject capsule in m_playerCapsules.Values)
        {
            if (capsule != null)
            {
                Destroy(capsule);
            }
        }
        m_playerCapsules.Clear();
        m_players.Clear();

        Debug.Log("[CLIENT] Stopped");
    }

    void OnDestroy()
    {
        Disconnect();
    }

    // =============================================================================================
    // UNITY UPDATE
    // =============================================================================================

    void Update()
    {
        // Read incoming messages
        byte[] data;
        while (m_inbox.TryDequeue(out data))
        {
            UDPPlayer.MsgType type = (UDPPlayer.MsgType)data[0];
            string content = Encoding.UTF8.GetString(data, 1, data.Length - 1);

            switch (type)
            {
                case UDPPlayer.MsgType.Text:
                    ProcessGameMessage(content);
                break;

                case UDPPlayer.MsgType.Player:
                    try
                    {
                        UDPPlayer.PlayerDTO dto = JsonUtility.FromJson<UDPPlayer.PlayerDTO>(content);
                        ProcessPlayerMessage(dto);
                    }
                    catch (ArgumentException e)
                    {
                        Debug.Log("[Player] Invalid JSON: " + e.Message);
                    }
                break;

                default:
                    Debug.Log("[SERVER] Uknown message type: " + data[0]);
                break;
            }
        }

        if (!m_running) return;

        // Stop the client after five seconds without a server response
        if (Time.realtimeSinceStartup - m_lastServerResponse >= 5f)
        {
            Debug.Log("[CLIENT] Server timed out.");
            Disconnect();
            return;
        }

        // Send PING every 1 second exact
        m_pingTimer -= Time.deltaTime;
        if (m_pingTimer <= 0)
        {
            SendString("PING:");
            m_playerDTO.msgType = "POS:";
            SendJson(m_playerDTO);
            m_pingTimer = 1f;
        }

        // Click-to-move system
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Prevents UI clicks from moving player capsule
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            float rayDistance;

            if (groundPlane.Raycast(ray, out rayDistance))
            {
                m_playerDTO.pos = ray.GetPoint(rayDistance);
                m_playerDTO.msgType = "POS:";
                SendJson(m_playerDTO);
            }
        }
    }

    // =============================================================================================
    // PACKET HANDLING
    // =============================================================================================

    void ProcessGameMessage(string message)
    {

        if (!m_running) return;

        if (message == "SERVER_ACTIVE")
        {
            m_lastServerResponse = Time.realtimeSinceStartup;
            return;
        }

        // PLAYERS:name1,name2,... -> replaces the local list with the one sent by the server
        if (message.StartsWith("PLAYERS:"))
        {
            string names = message.Substring(8);

            m_players.Clear();

            if (names.Length > 0)
            {
                string[] receivedNames = names.Split(',');

                for (int i = 0; i < receivedNames.Length; i++)
                {
                    m_players.Add(receivedNames[i]);
                }
            }

            Debug.Log("[CLIENT] Players: " + names);
            return;
        }

        // The server notifies the Timeout us that someone disconnected (Timeout)
        if (message.StartsWith("DISC:"))
        {
            string id = message.Substring(5);
            if (m_playerCapsules.ContainsKey(id))
            {
                Destroy(m_playerCapsules[id]);
                m_playerCapsules.Remove(id);
            }
            return;
        }
    }
    void ProcessPlayerMessage(UDPPlayer.PlayerDTO dto)
    {
        if (dto.msgType == "POS:")
        {
            string id = dto.id;

            // Create a capsule if this is a new player
            if (!m_playerCapsules.ContainsKey(dto.id))
            {
                GameObject newCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                newCapsule.GetComponent<Renderer>().material.color = Color.red;
                m_playerCapsules[dto.id] = newCapsule;
            }

            // Move the capsule to the target position
            m_playerCapsules[dto.id].transform.position = dto.pos;
        }
    }
    // =============================================================================================
    // SEND
    // =============================================================================================

    void SendString(string text)
    {
        Send(UDPPlayer.MsgType.Text, text);
    }
    void SendJson(UDPPlayer.PlayerDTO dto)
    {
        Send(UDPPlayer.MsgType.Player, JsonUtility.ToJson(dto));
    }
    void Send(UDPPlayer.MsgType type, string text)
    {
        if (m_socket == null || m_serverEndPoint == null) return;

        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] payload = new byte[body.Length + 1];
        payload[0] = (byte)type;
        Buffer.BlockCopy(body, 0, payload, 1, body.Length);

        try
        {
            m_socket.SendTo(payload, m_serverEndPoint);
        }
        catch { }
    }

    // =============================================================================================
    // CLIENT NETWORK THREAD
    // =============================================================================================

    void ClientThread()
    {
        m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        m_serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), port);
        SendString("JOIN:" + userName);
        m_playerDTO.playerName = userName;
        m_playerDTO.pos = Vector3.zero;

        Debug.Log("[CLIENT] UDP Ready, connecting to " + m_serverEndPoint);

        byte[] buffer = new byte[MaxPacketSize];
        while (m_running)
        {
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                int received = m_socket.ReceiveFrom(buffer, ref from);
                if (received > 0 && from.Equals(m_serverEndPoint))
                {
                    byte[] payload = new byte[received];
                    Array.Copy(buffer, payload, received);
                    m_inbox.Enqueue(payload);
                }
            }
            catch { if (!m_running) break; }
        }
    }

    // =============================================================================================
    // DEBUG UI
    // =============================================================================================

    void OnGUI()
    {
        if (!showDebugUI) return;
        GUILayout.BeginArea(new Rect(10, 10, 350, 120), GUI.skin.box);

        GUILayout.Label("--- MINIGAME UDP CLIENT ---");
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (!m_running)
        {
            GUILayout.Label("IP:", GUILayout.Width(30));
            serverIp = GUILayout.TextField(serverIp, GUILayout.Width(110));
            if (GUILayout.Button("Connect", GUILayout.Width(90))) StartNetwork();
        }
        else
        {
            GUILayout.Label("CONNECTED TO: " + serverIp);
            if (GUILayout.Button("Disconnect", GUILayout.Width(100))) Disconnect();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (m_running)
        {
            GUILayout.Label("Click anywhere to move!");
        }
        else
        {
            GUILayout.Label("Waiting to connect...");
        }

        GUILayout.EndArea();
    }

    // =============================================================================================
    // Getters
    // =============================================================================================

    public string GetPlayerNames()
    {
        return string.Join("\n", m_players);
    }

    public bool IsRunning()
    {
        return m_running;
    }
}