using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NewSocketsUDPServer : MonoBehaviour
{
    public int port = 9050;
    public bool autoStart = true;
    public bool showDebugUI = true;

    private List<UDPPlayer> m_players = new List<UDPPlayer>();

    const int MaxPacketSize = 64 * 1024;

    struct Packet
    { 
        public byte[] data; 
        public EndPoint from; 
    }

    Socket m_socket;

    readonly HashSet<EndPoint> m_knownClients = new HashSet<EndPoint>();
    readonly Dictionary<EndPoint, float> m_lastSeen = new Dictionary<EndPoint, float>();
    readonly List<Thread> m_threads = new List<Thread>();
    readonly ConcurrentQueue<Packet> m_inbox = new ConcurrentQueue<Packet>();
    readonly List<string> m_log = new List<string>();

    volatile bool m_running;

    // =============================================================================================
    // START / STOP
    // =============================================================================================

    void Start()
    {
        Application.runInBackground = true;
        if (autoStart) StartNetwork();
    }
    public bool StartNetwork()
    {
        if (m_running) return true;

        try
        {
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        catch (SocketException error)
        {
            Log("[SERVER] Couldn't start: " + error.SocketErrorCode);

            if (m_socket != null) m_socket.Close();
            m_socket = null;
            return false;
        }

        m_running = true;

        Thread t = new Thread(ServerThread);
        t.IsBackground = true;
        m_threads.Add(t);
        t.Start();

        return true;
    }
    public void Disconnect()
    {
        if (!m_running) return;
        m_running = false;

        if (m_socket != null)
        {
            try { m_socket.Close(); } catch { }
            m_socket = null;
        }

        lock (m_knownClients) m_knownClients.Clear();
        lock (m_lastSeen) m_lastSeen.Clear();
        m_players.Clear();

        Log("[SERVER] Stopped");
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
        Packet packet;
        while (m_inbox.TryDequeue(out packet))
        {
            ProcessMessage(packet.data, packet.from);
        }

        // TIMEOUT
        if (m_running)
        {
            List<EndPoint> toRemove = new List<EndPoint>();
            float currentTime = Time.time;

            lock (m_lastSeen)
            {
                foreach (var kvp in m_lastSeen)
                {
                    if (currentTime - kvp.Value > 5f) // 5 seconds passed
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                foreach (EndPoint deadClient in toRemove)
                {
                    Log("[SERVER] Timeout: " + deadClient);
                    RemovePlayer(deadClient);
                }


            }
        }
    }

    // =============================================================================================
    // MESSAGE HANDLING
    // =============================================================================================

    void ProcessMessage(byte[] data, EndPoint from)
    {
        string text = Encoding.UTF8.GetString(data);

        if (text == "LEAVE:")
        {
            RemovePlayer(from);
            return;
        }

        // Check if it is a new player to log
        bool isNewPlayer = false;
        lock (m_lastSeen)
        {
            if (!m_lastSeen.ContainsKey(from)) isNewPlayer = true;
            m_lastSeen[from] = Time.time;
        }

        if (isNewPlayer)
        {
            Log("[SERVER] NEW PLAYER CONNECTED: " + from.ToString());
            
            lock (m_knownClients) m_knownClients.Add(from);
        }

        // =========================================================================================
        // PLAYER JOIN
        // =========================================================================================

        if (text.StartsWith("JOIN:"))
        {
            string playerName = text.Substring(5).Trim();

            if (string.IsNullOrEmpty(playerName)) return;

            for (int i = 0; i < m_players.Count; i++)
            {
                if (m_players[i].endpoint.Equals(from)) return;
            }

            UDPPlayer player = new UDPPlayer(playerName, from);
            m_players.Add(player);

            Log("[SERVER] Player registered: " + player.name);
            Log("[SERVER] Total players: " + m_players.Count);
            SendPlayerList();

            return;
        }

        if (text.StartsWith("PING:"))
        {
            Log("[SERVER] Heartbeat (PING) received from: " + from.ToString());
            return;
        }

        if (text.StartsWith("POS:"))
        {
            string coords = text.Substring(4);
            Log("[SERVER] Movement from " + from.ToString() + " to coordinates: " + coords);

            string broadcastMsg = "POS:" + from.ToString() + ":" + coords;

            Broadcast(broadcastMsg);
        }
    }

    // =============================================================================================
    // BROADCAST
    // =============================================================================================

    void Broadcast(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        lock (m_knownClients)
        {
            foreach (EndPoint client in m_knownClients)
            {
                if (m_socket != null) m_socket.SendTo(data, client);
            }
        }
    }

    // =============================================================================================
    // SERVER NETWORK THREAD
    // =============================================================================================

    void ServerThread()
    {
        Log("[SERVER] Listening UDP on " + port);

        byte[] buffer = new byte[MaxPacketSize];

        while (m_running)
        {
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                int received = m_socket.ReceiveFrom(buffer, ref from);
                if (received > 0)
                {
                    byte[] payload = new byte[received];
                    Array.Copy(buffer, payload, received);
                    m_inbox.Enqueue(new Packet { data = payload, from = from });
                }
            }
            catch (SocketException) { continue; }
            catch (ObjectDisposedException) { break; }
        }
    }

    // =============================================================================================
    // LOGGING
    // =============================================================================================

    public void Log(string message)
    {
        Debug.Log(message);
        lock (m_log)
        {
            m_log.Add(message);
            // Keep only the last 100 messages to prevent infinite memory usage
            if (m_log.Count > 100) m_log.RemoveAt(0);
        }
    }

    // =============================================================================================
    // SERVER DEBUG UI
    // =============================================================================================

    void OnGUI()
    {
        if (!showDebugUI) return;
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
        GUILayout.Label("UDP MINIGAME SERVER - Port: " + port);
        GUILayout.Label("Connected Players: " + m_knownClients.Count);
        GUILayout.Space(10);

        lock (m_log)
        {
            for (int i = Mathf.Max(0, m_log.Count - 15); i < m_log.Count; i++)
                GUILayout.Label(m_log[i]);
        }
        GUILayout.EndArea();
    }

    // =============================================================================================
    // List
    // =============================================================================================
    void SendPlayerList()
    {
        string message = "PLAYERS:";

        for (int i = 0; i < m_players.Count; i++)
        {
            if (i > 0)
                message += ",";

            message += m_players[i].name;
        }

        Broadcast(message);
    }

    // =============================================================================================
    // Remove Players
    // =============================================================================================
    void RemovePlayer(EndPoint endpoint)
    {
        lock (m_lastSeen) m_lastSeen.Remove(endpoint);

        lock (m_knownClients) m_knownClients.Remove(endpoint);

        for (int i = 0; i < m_players.Count; i++)
        {
            if (m_players[i].endpoint.Equals(endpoint))
            {
                Log("[SERVER] Player left: " + m_players[i].name);
                m_players.RemoveAt(i);
                break;
            }
        }

        Broadcast("DISC:" + endpoint.ToString());
        SendPlayerList();
    }
}