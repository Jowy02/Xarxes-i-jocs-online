using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewSocketsUDPServer : MonoBehaviour
{
    public int port = 9050;
    public bool autoStart = true;
    public bool showDebugUI = true;

    public List<UDPPlayer> m_players = new List<UDPPlayer>();
    public List<UDPPlayer> m_standings = new List<UDPPlayer>();

    public static NewSocketsUDPServer Instance;

    NetSim m_sim;

    private int m_nextPlayerId = 0;
    private int m_hostPlayerId = 0;
    private bool m_waitingMigrationRejoin = false;
    private int m_expectedSurvivorsCount = 0;
    private bool m_raceCountdownSent = false;
    private int m_countdownToken = 0;
    private int m_restartToken = 0;

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
    private bool race_start = false;

    private float m_endRaceTime = -1f;
    private string m_bestLapName = "";
    private float m_bestLapTime = float.MaxValue;
    public class ServerOps
    {
        public Action<int> markCreate, markUpdate, markDestroy;
        public Func<byte[]> write;
    }
    public static ServerOps m_srv;
    private ReplicationManagerServer m_replicationManager;
    float m_nextTick;

    PickupManagerServer m_pickups;

    public BoostPickupState FindPickup(int id)
    {
        return m_pickups.Find(id);
    }

    // =============================================================================================
    // START / STOP
    // =============================================================================================

    void Start()
    {
        InitReplicationManager();

        if (Instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.runInBackground = true;
        if (autoStart) StartNetwork();
    }

    void InitReplicationManager()
    {
        if (m_replicationManager != null) return;
        m_replicationManager = new ReplicationManagerServer(this);
        m_pickups = new PickupManagerServer(m_replicationManager, this);
        m_srv = new ServerOps
        {
            markCreate = m_replicationManager.MarkCreate,
            markUpdate = m_replicationManager.MarkUpdate,
            markDestroy = m_replicationManager.MarkDestroy,
            write = m_replicationManager.WritePacket
        };
    }

    public bool StartNetwork()
    {
        if (m_running) return true;

        try
        {
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_socket.Bind(new IPEndPoint(IPAddress.Any, port));
            m_sim.m_socket = m_socket;
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

    public bool StartMigratedServer(int newHostId, string newHostName, int oldHostId, int expectedSurvivors)
    {
        Disconnect();
        InitReplicationManager();
        m_replicationManager.Reset();

        m_standings.Clear();
        m_endRaceTime = -1f;
        m_bestLapName = "";
        m_bestLapTime = float.MaxValue;

        m_hostPlayerId = newHostId;
        m_nextPlayerId = Mathf.Max(m_nextPlayerId, newHostId + 1);
        race_start = true;
        m_waitingMigrationRejoin = true;
        m_expectedSurvivorsCount = Mathf.Max(1, expectedSurvivors);

        if (!StartNetwork())
        {
            m_waitingMigrationRejoin = false;
            race_start = false;
            return false;
        }

        // Destroy the lost host car
        m_srv.markDestroy(oldHostId);
        SpawnPickupsFromScene();

        StopCoroutine(nameof(MigrationResumeRoutine));
        StartCoroutine(MigrationResumeRoutine(newHostName));
        Log("[SERVER] Host migrated! New host: " + newHostName + " (ID: " + newHostId + ")");
        return true;
    }

    IEnumerator MigrationResumeRoutine(string newHostName)
    {
        float pauseDeadline = Time.realtimeSinceStartup + 5.0f;
        while (Time.realtimeSinceStartup < pauseDeadline)
        {
            yield return null;
        }

        m_waitingMigrationRejoin = false;
        SendPlayerList();
        SendRoster();
        Broadcast("CHAT:[SYSTEM] New host assigned: " + newHostName + ". Resuming race!");
        m_countdownToken++;
        BroadcastReliable("START_COUNTDOWN:" + m_countdownToken);
    }

    public void Disconnect()
    {
        if (!m_running) return;
        m_running = false;
        race_start = false;
        m_waitingMigrationRejoin = false;
        m_raceCountdownSent = false;

        if (m_socket != null)
        {
            try { m_socket.Close(); } catch { }
            m_socket = null;
        }

        lock (m_knownClients) m_knownClients.Clear();
        lock (m_lastSeen) m_lastSeen.Clear();
        m_players.Clear();
        m_standings.Clear();

        if (m_replicationManager != null) m_replicationManager.Reset();

        if (m_sim != null)
        {
            m_sim.m_socket = null;
            m_sim.Clear();
        }

        Log("[SERVER] Stopped");
    }

    public bool IsRunning()
    {
        return m_running;
    }

    public void NotifyHostLeavingAndStop(string hostName)
    {
        if (!m_running || m_socket == null) return;

        byte[] body = Encoding.UTF8.GetBytes("HOST_LEFT:" + hostName);
        byte[] payload = new byte[body.Length + 1];
        payload[0] = (byte)UDPPlayer.MsgType.Text;
        Buffer.BlockCopy(body, 0, payload, 1, body.Length);

        lock (m_knownClients)
        {
            foreach (EndPoint client in m_knownClients)
            {
                try { m_socket.SendTo(payload, client); } catch { }
            }
        }

        Disconnect();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Disconnect();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        m_sim = gameObject.AddComponent<NetSim>();
        DontDestroyOnLoad(gameObject);
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

            if (Time.time >= m_nextTick)
            {
                m_nextTick = Time.time + 0.05f;
                Tick();
            }
        }
        if (m_endRaceTime > 0f && Time.time >= m_endRaceTime) EndRace();
    }

    void Tick()
    {
        if (!race_start) return;

        foreach (UDPPlayer play in m_players)
        {
            if (play.hasReceivedTransform)
                m_srv.markUpdate(play.id);
        }
        m_pickups.Tick(m_players);

        byte[] pack = m_srv.write();
        foreach (EndPoint client in m_knownClients)
        {
            try { m_sim.Send(pack, client); } catch { }
        }
    }

    // =============================================================================================
    // MESSAGE HANDLING
    // =============================================================================================

    void ProcessMessage(byte[] data, EndPoint from)
    {
        if (data == null || data.Length == 0) return;

        UDPPlayer.MsgType type = (UDPPlayer.MsgType)data[0];

        switch (type)
        {
            case UDPPlayer.MsgType.Text:
                string content = Encoding.UTF8.GetString(data, 1, data.Length - 1);
                HandleText(content, from);
                break;

            case UDPPlayer.MsgType.Transform:
                lock (m_lastSeen) m_lastSeen[from] = Time.time;
                UpdateUDPCar(data);
                break;

            default:
                Log("[SERVER] Uknown message type: " + data[0]);
                break;
        }
    }

    void HandleText(string text, EndPoint from)
    {
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
        // PLAYER JOIN & HOST MIGRATION
        // =========================================================================================

        if (text.StartsWith("JOIN:"))
        {
            string playerName = text.Substring(5).Trim();

            if (string.IsNullOrEmpty(playerName)) return;

            for (int i = 0; i < m_players.Count; i++)
            {
                if (m_players[i].endpoint.Equals(from)) return;
            }

            int assignedId = m_nextPlayerId;
            m_nextPlayerId++;
            UDPPlayer player = new UDPPlayer(playerName, from, assignedId);
            m_players.Add(player);

            Send(UDPPlayer.MsgType.Text, "YOUR_ID:" + assignedId, from);
            Log("[SERVER] Player registered: " + player.name + " with ID: " + assignedId);
            Log("[SERVER] Total players: " + m_players.Count);
            SendPlayerList();
            SendRoster();

            return;
        }

        if (text.StartsWith("REJOIN:"))
        {
            string body = text.Substring(7);
            int sep = body.IndexOf(':');
            if (sep <= 0) return;

            int rejoinId;
            if (!int.TryParse(body.Substring(0, sep), out rejoinId)) return;
            string playerName = body.Substring(sep + 1).Trim();
            if (string.IsNullOrEmpty(playerName)) return;

            UDPPlayer existing = null;
            for (int i = 0; i < m_players.Count; i++)
            {
                if (m_players[i].id == rejoinId)
                {
                    existing = m_players[i];
                    break;
                }
            }

            if (existing == null)
            {
                existing = new UDPPlayer(playerName, from, rejoinId);
                m_players.Add(existing);
                m_nextPlayerId = Mathf.Max(m_nextPlayerId, rejoinId + 1);
            }
            else
            {
                existing.endpoint = from;
                existing.name = playerName;
            }

            Send(UDPPlayer.MsgType.Text, "YOUR_ID:" + rejoinId, from);
            Send(UDPPlayer.MsgType.Text, "SERVER_ACTIVE", from);
            SendPlayerList();
            SendRoster();
            return;
        }

        if (text == "PING:")
        {
            Send(UDPPlayer.MsgType.Text, "SERVER_ACTIVE", from);
            return;
        }

        if (text.StartsWith("CHAT:"))
        {
            Broadcast(text);
            return;
        }

        if (text.StartsWith("LAPTIME:"))
        {
            float t;
            if (float.TryParse(text.Substring(8), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out t) && t < m_bestLapTime)
            {
                for (int i = 0; i < m_players.Count; i++)
                {
                    if (m_players[i].endpoint.Equals(from))
                    {
                        m_bestLapTime = t;
                        m_bestLapName = m_players[i].name;
                        break;
                    }
                }
            }
            return;
        }

        if (text.StartsWith("LAP:"))
        {
            for (int i = 0; i < m_players.Count; i++)
            {
                if (m_players[i].endpoint.Equals(from))
                {
                    m_standings.Add(m_players[i]);
                    if (m_standings.Count <= 1)
                    {
                        Broadcast("CHAT:" + m_players[i].name + " WINS THE RACE");
                        m_endRaceTime = Time.time + 30f;
                        Broadcast("COUNTDOWN:30");
                    }
                    else Broadcast("CHAT:" + m_players[i].name + " ends " + m_standings.Count.ToString());
                    break;
                }
            }
            return;
        }

        if (text.StartsWith("START_RACE:"))
        {
            if (race_start) return;
            if (m_replicationManager != null) m_replicationManager.Reset();
            SendRoster();

            string startMessage = "START_RACE:";

            for (int i = 0; i < m_players.Count; i++)
            {
                if (i > 0)
                {
                    startMessage += ",";
                }

                startMessage += m_players[i].id;
            }

            m_raceCountdownSent = false;
            BroadcastReliable(startMessage);
            foreach (UDPPlayer play in m_players) play.hasReceivedTransform = false;
            race_start = true;
            return;
        }

        if (text.StartsWith("READY_FOR_COUNTDOWN:"))
        {
            if (!m_waitingMigrationRejoin && !m_raceCountdownSent)
            {
                m_raceCountdownSent = true;
                m_countdownToken++;
                BroadcastReliable("START_COUNTDOWN:" + m_countdownToken);
            }
            return;
        }

        if (text.StartsWith("RESTART_RACE:"))
        {
            Log("[SERVER] Restarting race for all players!");

            m_standings.Clear();
            if (m_replicationManager != null) m_replicationManager.Reset();

            foreach (GameObject car in GameObject.FindGameObjectsWithTag("Car_"))
            {
                LapManager lapManager = car.GetComponent<LapManager>();
                if (lapManager != null) lapManager.RestartLaps();
            }

            m_endRaceTime = -1f;
            m_bestLapName = "";
            m_bestLapTime = float.MaxValue;
            m_restartToken++;
            m_countdownToken++;
            BroadcastReliable("RESTART_RACE:" + m_restartToken);
            BroadcastReliable("START_COUNTDOWN:" + m_countdownToken);
            foreach (UDPPlayer play in m_players) play.hasReceivedTransform = false;
            m_pickups.Restart();
            race_start = true;

            return;
        }
    }

    public void UpdateUDPCar(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            byte type = reader.ReadByte();
            int objectId = reader.ReadInt32();
            foreach (UDPPlayer play in m_players)
            {
                if (play.id == objectId)
                {
                    play.carNetworkState.position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    play.carNetworkState.rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    play.carNetworkState.isDrifting = reader.ReadBoolean();
                    play.carNetworkState.skinId = reader.ReadInt32();
                    play.hasReceivedTransform = true;
                    break;
                }
            }
        }
    }

    // =============================================================================================
    // BROADCAST
    // =============================================================================================

    void Broadcast(string message)
    {
        lock (m_knownClients)
        {
            foreach (EndPoint client in m_knownClients)
            {
                if (m_socket != null) Send(UDPPlayer.MsgType.Text, message, client);
            }
        }
    }

    void BroadcastRaw(byte[] rawData, EndPoint sender)
    {
        lock (m_knownClients)
        {
            foreach (EndPoint client in m_knownClients)
            {
                if (m_socket != null && !client.Equals(sender))
                {
                    try { m_sim.Send(rawData, client); } catch { }
                }
            }
        }
    }

    void BroadcastReliable(string message, int repeats = 5, float interval = 0.2f)
    {
        StartCoroutine(BroadcastRepeatedRoutine(message, repeats, interval));
    }

    IEnumerator BroadcastRepeatedRoutine(string message, int repeats, float interval)
    {
        for (int i = 0; i < repeats; i++)
        {
            if (!m_running) yield break;
            Broadcast(message);
            if (i < repeats - 1) yield return new WaitForSecondsRealtime(interval);
        }
    }

    void Send(UDPPlayer.MsgType type, string text, EndPoint client)
    {
        if (m_socket == null) return;

        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] payload = new byte[body.Length + 1];
        payload[0] = (byte)type;
        Buffer.BlockCopy(body, 0, payload, 1, body.Length);

        try
        {
            m_sim.Send(payload, client);
        }
        catch { }
    }

    // Tells the player that they have picked up a boost
    public void SendBoost(UDPPlayer player, int pickupId)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)UDPPlayer.MsgType.Boost);
        writer.Write(pickupId);

        m_sim.Send(stream.ToArray(), player.endpoint);
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
            // Keep only the last 10 messages to prevent infinite memory usage
            if (m_log.Count > 10) m_log.RemoveAt(0);
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

    string GetServerOutboundIP()
    {
        try
        {
            using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect("8.8.8.8", 65530);
                IPEndPoint ep = s.LocalEndPoint as IPEndPoint;
                if (ep != null) return ep.Address.ToString();
            }
        }
        catch { }

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addresses[i]))
                    return addresses[i].ToString();
            }
        }
        catch { }

        return "127.0.0.1";
    }

    void SendRoster()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("ROSTER:");
        sb.Append(m_hostPlayerId);
        sb.Append(';');

        for (int i = 0; i < m_players.Count; i++)
        {
            if (i > 0) sb.Append(',');
            IPEndPoint ipEp = m_players[i].endpoint as IPEndPoint;
            string ipStr = "127.0.0.1";
            if (ipEp != null)
            {
                ipStr = IPAddress.IsLoopback(ipEp.Address)
                    ? GetServerOutboundIP()
                    : ipEp.Address.ToString();
            }

            string safeName = m_players[i].name.Replace("|", "").Replace(";", "").Replace(",", "");
            sb.Append(m_players[i].id).Append('|').Append(safeName).Append('|').Append(ipStr);
        }

        Broadcast(sb.ToString());
    }

    // =============================================================================================
    // Remove Players
    // =============================================================================================

    void RemovePlayer(EndPoint endpoint)
    {
        lock (m_lastSeen) m_lastSeen.Remove(endpoint);
        lock (m_knownClients) m_knownClients.Remove(endpoint);

        string leavingName = null;
        for (int i = 0; i < m_players.Count; i++)
        {
            if (m_players[i].endpoint.Equals(endpoint))
            {
                leavingName = m_players[i].name;
                Log("[SERVER] Player left: " + leavingName);
                if (race_start)
                {
                    m_srv.markDestroy(m_players[i].id);
                }
                m_players.RemoveAt(i);
                break;
            }
        }

        if (!string.IsNullOrEmpty(leavingName) && race_start)
        {
            Broadcast("CHAT:[SYSTEM] " + leavingName + " left the race.");
        }

        SendPlayerList();
        SendRoster();
    }

    void EndRace()
    {
        m_endRaceTime = -1f;

        Broadcast("CHAT:===STANDINGS:");
        for (int i = 0; i < m_standings.Count; i++)
            Broadcast("CHAT:" + (i + 1) + ". " + m_standings[i].name);

        if (m_bestLapName != "")
            Broadcast("CHAT:Fastest lap: " + m_bestLapName + " (" +
                m_bestLapTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s)");

        Broadcast("RETURN_LOBBY:");

        m_standings.Clear();
        m_bestLapName = "";
        m_bestLapTime = float.MaxValue;
        m_pickups.Clear();
        race_start = false;
    }

    void SpawnPickupsFromScene()
    {
        List<Vector3> positions = new List<Vector3>();

        foreach (GameObject spawn in GameObject.FindGameObjectsWithTag("PickupSpawn"))
        {
            positions.Add(spawn.transform.position);
        }

        m_pickups.StartRound(positions);
        Log("[SERVER] Pickups spawned: " + positions.Count);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "RaceTrack") return;
        SpawnPickupsFromScene();
    }
}