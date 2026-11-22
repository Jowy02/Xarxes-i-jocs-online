using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NewSocketsUDPClient : MonoBehaviour
{
    public struct RosterEntry
    {
        public int id;
        public string name;
        public string ip;
    }

    public string serverIp = "127.0.0.1";
    public int port = 9050;
    public string userName = "Player";
    public bool autoStart = true;
    public bool showDebugUI = true;
    public bool isHost = false;

    public string lobbySceneName = "LobbyUDP";
    public static event Action<int> OnFinishCountdown;

    public static NewSocketsUDPClient Instance;

    private int m_localPlayerId = -1;

    private List<string> m_players = new List<string>();
    readonly List<string> m_chatlog = new List<string>();
    private List<int> m_racePlayerIds = new List<int>();
    private readonly List<RosterEntry> m_roster = new List<RosterEntry>();
    private int m_currentHostId = 0;

    const int MaxPacketSize = 64 * 1024;

    Socket m_socket;
    EndPoint m_serverEndPoint;

    readonly List<Thread> m_threads = new List<Thread>();
    readonly ConcurrentQueue<byte[]> m_inbox = new ConcurrentQueue<byte[]>();
    volatile bool m_running;

    float m_pingTimer = 0f;
    private float m_lastServerResponse;

    private bool m_isMigrating = false;
    private bool m_waitingWelcome = false;
    private float m_rejoinRetryTimer = 0f;
    private float m_migrationDeadline = 0f;

    public static event Action<UDPPlayer.CarNetworkState> OnCarTransformReceived;
    public static event Action<int> OnCarDestroyed;
    public static event Action OnRaceRestarted;
    public static event Action OnStartCountdownReceived;
    public static event Action<string, bool> OnHostMigrationStarted;

    public LinkingContext LinkingContext { get; } = new LinkingContext();
    private int m_lastSequence = -1;

    NetSim m_sim;

    // =============================================================================================
    // START / STOP
    // =============================================================================================

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        m_sim = gameObject.AddComponent<NetSim>();
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (Instance != this) return;
        Application.runInBackground = true;
        if (autoStart) StartNetwork();
    }

    public void StartNetwork(bool isMigrationRejoin = false)
    {
        if (m_running) return;

        if (!isMigrationRejoin)
        {
            m_localPlayerId = -1;
            m_racePlayerIds.Clear();
            m_roster.Clear();
            m_currentHostId = 0;
        }

        m_lastSequence = -1;
        m_waitingWelcome = isMigrationRejoin;
        m_rejoinRetryTimer = 0.5f;
        m_running = true;
        m_lastServerResponse = Time.realtimeSinceStartup;
        m_pingTimer = 0f;

        Thread t = new Thread(() => ClientThread(isMigrationRejoin));
        t.IsBackground = true;
        m_threads.Add(t);
        t.Start();
    }

    public void Disconnect()
    {
        DisconnectInternal(true);
    }

    void DisconnectInternal(bool sendLeave)
    {
        if (!m_running && m_socket == null) return;

        // By sending Leave here before closing the socket, we can skip the wainting timeout
        if (sendLeave) SendDirect("LEAVE:");
        m_running = false;

        if (m_socket != null)
        {
            try { m_socket.Close(); } catch { }
            m_socket = null;
        }

        while (m_inbox.TryDequeue(out _)) { }
        m_lastSequence = -1;

        if (!m_isMigrating)
        {
            m_players.Clear();
        }

        Debug.Log("[CLIENT] Stopped");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DisconnectInternal(true);
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
            if (data == null || data.Length == 0) continue;

            UDPPlayer.MsgType type = (UDPPlayer.MsgType)data[0];

            switch (type)
            {
                case UDPPlayer.MsgType.Text:
                    string content = Encoding.UTF8.GetString(data, 1, data.Length - 1);
                    ProcessGameMessage(content);
                    break;

                //case UDPPlayer.MsgType.Transform:
                //    UDPPlayer.CarNetworkState state = NetworkSerializer.DeserializeCar(data);
                //    OnCarTransformReceived?.Invoke(state);
                //break;

                case UDPPlayer.MsgType.Replication:
                    var r = new BinaryReader(new MemoryStream(data));
                    r.ReadByte();
                    ReadPacket(r);
                    break;

                default:
                    Debug.Log("[SERVER] Uknown message type: " + data[0]);
                    break;
            }
        }

        if (!m_running) return;

        // Retry REJOIN during host migration until YOUR_ID confirms connection
        if (m_isMigrating && m_waitingWelcome)
        {
            m_rejoinRetryTimer -= Time.deltaTime;
            if (m_rejoinRetryTimer <= 0f)
            {
                m_rejoinRetryTimer = 0.5f;
                SendDirect("REJOIN:" + m_localPlayerId + ":" + userName);
            }
        }

        if (m_isMigrating && Time.realtimeSinceStartup >= m_migrationDeadline)
        {
            Debug.Log("[CLIENT] Host migration timed out. Returning to Lobby...");
            m_isMigrating = false;
            ReturnToLobbyFallback();
            return;
        }

        // Stop the client after five seconds without a server response
        if (!m_isMigrating && Time.realtimeSinceStartup - m_lastServerResponse >= 5f)
        {
            Debug.Log("[CLIENT] Server connection timed out. Attempting host migration or returning to Lobby...");
            HandleHostLost();
            return;
        }

        // Send PING every 1 second exact
        m_pingTimer -= Time.deltaTime;
        if (m_pingTimer <= 0)
        {
            SendString("PING:");
            m_pingTimer = 1f;
        }
    }

    void HandleHostLost()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "RaceTrack" || isHost)
        {
            ReturnToLobbyFallback();
            return;
        }

        RosterEntry? nextHost = null;
        int bestId = int.MaxValue;
        for (int i = 0; i < m_roster.Count; i++)
        {
            if (m_roster[i].id == m_currentHostId) continue;
            if (m_roster[i].id < bestId)
            {
                bestId = m_roster[i].id;
                nextHost = m_roster[i];
            }
        }

        if (!nextHost.HasValue)
        {
            ReturnToLobbyFallback();
            return;
        }

        StartCoroutine(HostMigrationRoutine(nextHost.Value));
    }

    IEnumerator HostMigrationRoutine(RosterEntry newHost)
    {
        m_isMigrating = true;
        m_migrationDeadline = Time.realtimeSinceStartup + 12f;

        int lostHostId = m_currentHostId;
        m_roster.RemoveAll(r => r.id == lostHostId);
        m_racePlayerIds.Remove(lostHostId);

        GameObject oldHostGo = LinkingContext.Find(lostHostId);
        if (oldHostGo != null)
        {
            LinkingContext.Unregister(lostHostId);
            Destroy(oldHostGo);
            OnCarDestroyed?.Invoke(lostHostId);
        }

        AddLocalChatMessage("[SYSTEM] Host connection lost! Searching for a new host...");
        AddLocalChatMessage("[SYSTEM] Assigning new host: " + newHost.name + "...");

        DisconnectInternal(false);

        bool iAmNewHost = (newHost.id == m_localPlayerId);
        OnHostMigrationStarted?.Invoke(newHost.name, iAmNewHost);

        if (iAmNewHost)
        {
            isHost = true;
            m_currentHostId = m_localPlayerId;
            serverIp = "127.0.0.1";

            NewSocketsUDPServer srv = NewSocketsUDPServer.Instance;
            if (srv == null || !srv.StartMigratedServer(m_localPlayerId, userName, lostHostId, m_roster.Count))
            {
                Debug.LogError("[CLIENT] Could not start migrated server.");
                m_isMigrating = false;
                ReturnToLobbyFallback();
                yield break;
            }

            StartNetwork(true);
        }
        else
        {
            isHost = false;
            m_currentHostId = newHost.id;
            serverIp = newHost.ip;

            // Wait to the new host finishes binding port 9050
            yield return new WaitForSecondsRealtime(0.5f);
            StartNetwork(true);
        }
    }

    void ReturnToLobbyFallback()
    {
        DisconnectInternal(false);
        isHost = false;
        if (NewSocketsUDPServer.Instance != null) NewSocketsUDPServer.Instance.Disconnect();

        // Return to lobby if dropped during a race
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "RaceTrack")
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
        }
    }

    public void AddLocalChatMessage(string msg)
    {
        lock (m_chatlog)
        {
            m_chatlog.Add(msg);
            if (m_chatlog.Count > 100) m_chatlog.RemoveAt(0);
        }
    }

    // =============================================================================================
    // PACKET HANDLING
    // =============================================================================================

    public void ReadPacket(BinaryReader reader)
    {
        int sequence = reader.ReadInt32();
        if (sequence <= m_lastSequence) return;
        m_lastSequence = sequence;

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            UDPPlayer.ReplicationAction action = (UDPPlayer.ReplicationAction)reader.ReadByte();

            GameObject go = LinkingContext.Find(id);

            if (action == UDPPlayer.ReplicationAction.Destroy)
            {
                if (go != null)
                {
                    LinkingContext.Unregister(id);
                    Destroy(go);
                    m_racePlayerIds.Remove(id);
                    OnCarDestroyed?.Invoke(id);
                }
                continue;
            }

            byte type = reader.ReadByte(); // type 0 = Car

            if (id == m_localPlayerId || go == null)
            {
                reader.ReadBytes(29); // skip its data: 6 floats + bool + int
                continue;
            }

            // Call to Read of NetObject (CarNetObject)
            NetObject netObj = go.GetComponent<NetObject>();
            if (netObj != null)
            {
                netObj.Read(reader); // Read own position, rotation, drift and skin

                // If its not local car, trigger the event to apply visual lerp
                if (id != m_localPlayerId)
                {
                    CarNetObject carNet = go.GetComponent<CarNetObject>();
                    if (carNet != null)
                    {
                        UDPPlayer.CarNetworkState state = new UDPPlayer.CarNetworkState
                        {
                            objectId = id,
                            position = go.transform.position,
                            rotation = go.transform.eulerAngles,
                            isDrifting = carNet.isDrifting,
                            skinId = carNet.skinId
                        };
                        OnCarTransformReceived?.Invoke(state);
                    }
                }
            }
            else
            {
                reader.ReadBytes(29);
            }
        }
    }

    void ProcessGameMessage(string message)
    {
        if (!m_running) return;

        if (message.StartsWith("YOUR_ID:"))
        {
            int receivedId;

            if (int.TryParse(message.Substring(8), out receivedId))
            {
                m_localPlayerId = receivedId;
                m_waitingWelcome = false;
                m_lastServerResponse = Time.realtimeSinceStartup;
                Debug.Log("[CLIENT] My player ID: " + m_localPlayerId);
            }

            return;
        }
        if (message == "SERVER_ACTIVE")
        {
            m_lastServerResponse = Time.realtimeSinceStartup;
            return;
        }

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

        if (message.StartsWith("ROSTER:"))
        {
            string payload = message.Substring(7);
            int semi = payload.IndexOf(';');
            if (semi > 0)
            {
                int hostId;
                if (int.TryParse(payload.Substring(0, semi), out hostId))
                    m_currentHostId = hostId;

                m_roster.Clear();
                string listPart = payload.Substring(semi + 1);
                if (!string.IsNullOrEmpty(listPart))
                {
                    string[] entries = listPart.Split(',');
                    for (int i = 0; i < entries.Length; i++)
                    {
                        string[] fields = entries[i].Split('|');
                        int pid;
                        if (fields.Length == 3 && int.TryParse(fields[0], out pid))
                        {
                            m_roster.Add(new RosterEntry { id = pid, name = fields[1], ip = fields[2] });
                        }
                    }
                }
            }
            return;
        }

        if (message.StartsWith("DISC:"))
        {
            string playerText = message.Substring(5);
            string[] parts = playerText.Split(',');
            if (m_racePlayerIds.Count > 0)
            {
                GameObject temp = GameObject.Find(parts[0]);
                GameObject.Destroy(temp);
                m_racePlayerIds.Remove(int.Parse(parts[1]));
            }
        }

        if (message.StartsWith("CHAT:"))
        {
            lock (m_chatlog)
            {
                message = message.Substring(5).Trim();
                m_chatlog.Add(message);
                if (m_chatlog.Count > 100) m_chatlog.RemoveAt(0);
            }
            return;
        }

        if (message.StartsWith("START_RACE:"))
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "RaceTrack")
            {
                return;
            }

            m_lastSequence = -1;
            string idsText = message.Substring(11);
            string[] receivedIds = idsText.Split(',');

            m_racePlayerIds.Clear();

            for (int i = 0; i < receivedIds.Length; i++)
            {
                int playerId;

                if (!int.TryParse(receivedIds[i], out playerId))
                {
                    m_racePlayerIds.Clear();
                    Debug.LogError("[CLIENT] Invalid race player list");
                    return;
                }

                m_racePlayerIds.Add(playerId);
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("RaceTrack");
            return;
        }

        if (message.StartsWith("COUNTDOWN:"))
        {
            int secs;
            if (int.TryParse(message.Substring(10), out secs)) OnFinishCountdown?.Invoke(secs);
            return;
        }

        if (message == "RETURN_LOBBY:")
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "RaceTrack") return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
            return;
        }

        if (message.StartsWith("RESTART_RACE:"))
        {
            Debug.Log("[CLIENT] Race restarted by host.");
            m_lastSequence = -1;
            OnRaceRestarted?.Invoke();
            return;
        }

        if (message.StartsWith("START_COUNTDOWN:"))
        {
            m_isMigrating = false;
            m_waitingWelcome = false;
            m_lastSequence = -1;
            OnStartCountdownReceived?.Invoke();
            return;
        }
    }

    // =============================================================================================
    // SEND
    // =============================================================================================

    public void SendString(string text)
    {
        Send(UDPPlayer.MsgType.Text, text);
    }

    public void SendCarTransform(int id, Vector3 pos, Vector3 rot, bool isDrifting, int skinId)
    {
        if (m_socket == null || m_serverEndPoint == null || !m_running || m_waitingWelcome) return;

        byte[] payload = NetworkSerializer.SerializeCar(id, pos, rot, isDrifting, skinId);
        try
        {
            m_sim.Send(payload, m_serverEndPoint);
        }
        catch { }
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
            m_sim.Send(payload, m_serverEndPoint);
        }
        catch { }
    }

    // =============================================================================================
    // CLIENT NETWORK THREAD
    // =============================================================================================

    void ClientThread(bool isMigrationRejoin)
    {
        m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        m_sim.m_socket = m_socket;
        m_serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), port);

        if (isMigrationRejoin && m_localPlayerId >= 0)
            SendDirect("REJOIN:" + m_localPlayerId + ":" + userName);
        else
            SendDirect("JOIN:" + userName);

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

    //Used for JOIN and LEAVE, they do not go through the simulator
    void SendDirect(string text)
    {
        if (m_socket == null || m_serverEndPoint == null) return;

        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] payload = new byte[body.Length + 1];
        payload[0] = (byte)UDPPlayer.MsgType.Text;
        Buffer.BlockCopy(body, 0, payload, 1, body.Length);

        try { m_socket.SendTo(payload, m_serverEndPoint); } catch { }
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

        GUILayout.EndArea();
    }

    // =============================================================================================
    // Getters
    // =============================================================================================

    public string GetPlayerNames()
    {
        return string.Join("\n", m_players);
    }
    public string GetChatLog()
    {
        return string.Join("\n", m_chatlog);
    }
    public bool IsRunning()
    {
        return m_running;
    }
    public int GetLocalPlayerId()
    {
        return m_localPlayerId;
    }
    public int[] GetRacePlayerIds()
    {
        return m_racePlayerIds.ToArray();
    }
    public bool IsMigrating()
    {
        return m_isMigrating;
    }
}