using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NewSocketsTCPClient : MonoBehaviour
{
    public string serverIp = "127.0.0.1";
    public int port = 9050;
    public string userName = "Player";
    public bool autoStart = true;

    const int MaxPacketSize = 64 * 1024;

    Socket m_connection;

    readonly List<Thread> m_threads = new List<Thread>();
    readonly ConcurrentQueue<byte[]> m_inbox = new ConcurrentQueue<byte[]>();

    readonly List<string> m_log = new List<string>();
    readonly HashSet<int> m_warned = new HashSet<int>();

    volatile bool m_running;

    // Message typed into the chat input field.
    string m_messageInput = "";
    readonly List<string> m_players = new List<string>();

    public bool IsRunning
    {
        get { return m_running; }
    }

    // =============================================================================================
    // START / STOP
    // =============================================================================================

    void Start()
    {
        Application.runInBackground = true;

        ParseCommandLine();

        if (autoStart)
            StartNetwork();
    }

    public void StartNetwork()
    {
        if (m_running) return;
        m_running = true;
        StartThread(ClientThread);
    }

    public void Disconnect()
    {
        if (!m_running) return;

        m_running = false;

        if (m_connection != null) SendString("LEAVE:");

        CloseSocket(m_connection);
        m_connection = null;

        Thread[] threads;

        lock (m_threads)
        {
            threads = m_threads.ToArray();
            m_threads.Clear();
        }

        foreach (Thread t in threads)
        {
            if (t != Thread.CurrentThread)
                t.Join(500);
        }

        m_log.Clear();
        m_players.Clear();

        Log("[CLIENT] Disconnected");
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
        byte[] data;

        while (m_inbox.TryDequeue(out data))
            OnPacketReceived(data);
    }

    // =============================================================================================
    // CLIENT NETWORK THREAD
    // =============================================================================================

    void ClientThread()
    {
        try
        {
            m_connection = StartClient();
        }
        catch (SocketException e)
        {
            Log("[CLIENT] Could not connect: " + e.SocketErrorCode + 
                (e.SocketErrorCode == SocketError.ConnectionRefused
                ? " (nobody is listening on " + serverIp + ":" + port + ")": "")
            );
            m_running = false;
            return;
        }

        if (m_connection == null)
        {
            WarnTodo(1, "StartClient() returned null");
            m_running = false;
            return;
        }

        Log("[CLIENT] Connected to " + serverIp + ":" + port);
        // Introduce ourselves to the server.
        OnConnected();
        // Wait for packets from the server.
        ReceiveLoop(m_connection);
        Log("[CLIENT] Connection closed");
        m_running = false;
    }

    // =============================================================================================
    // SEND
    // =============================================================================================

    public void SendPacket(byte[] payload)
    {
        if (m_connection == null || payload == null)
            return;

        byte[] framed = new byte[4 + payload.Length];

        // First 4 bytes = message length.
        BitConverter.GetBytes(payload.Length).CopyTo(framed, 0);

        // Remaining bytes = message.
        payload.CopyTo(framed, 4);

        try
        {
            int sent = SendRaw(m_connection, framed);

            if (sent < 0) WarnTodo(3, "SendRaw() is not implemented yet");
        }
        catch (SocketException e)
        {
            Log("[CLIENT] Send failed: " + e.SocketErrorCode);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void SendString(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        SendPacket(Encoding.UTF8.GetBytes(text));
    }

    // =============================================================================================
    // CHAT
    // =============================================================================================
    void SendMessageToServer()
    {
        if (!m_running)
            return;

        if (string.IsNullOrWhiteSpace(m_messageInput))
            return;

        string message = m_messageInput.Trim();

        // Tell the server that this is a chat message.
        SendString("CHAT:" + message);

        // Clear input field.
        m_messageInput = "";
    }

    // =============================================================================================
    // RECEIVE
    // =============================================================================================

    void ReceiveLoop(Socket socket)
    {
        byte[] header = new byte[4];

        while (m_running)
        {
            if (!ReadExactly(socket, header, 4)) return;

            int size = BitConverter.ToInt32(header, 0);

            if (size <= 0 || size > MaxPacketSize)
            {
                Log("[CLIENT] Invalid packet size: " + size);
                return;
            }

            byte[] payload = new byte[size];

            if (!ReadExactly(socket, payload, size)) return;

            // Network thread 
            m_inbox.Enqueue(payload);
        }
    }

    bool ReadExactly(Socket socket, byte[] buffer, int count)
    {
        int total = 0;

        while (total < count)
        {
            int read;

            try
            {
                read = ReceiveRaw(socket, buffer,total,count - total);
            }
            catch (SocketException e)
            {
                Log("[CLIENT] Connection lost: " + e.SocketErrorCode);
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (read < 0)
            {
                WarnTodo(4, "ReceiveRaw() is not implemented yet");
                return false;
            }

            // Server closed the connection.
            if (read == 0) return false;

            total += read;
        }
        return true;
    }

    // =============================================================================================
    // PACKET HANDLING
    // =============================================================================================

    void OnConnected()
    {
        // Tell the server who we are.
        SendString("JOIN:" + userName);
    }

    void OnPacketReceived(byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);

        if (message.StartsWith("PLAYERS:"))
        {
            string list = message.Substring(8);

            m_players.Clear();
            if (list.Length > 0)
                m_players.AddRange(list.Split(','));

            return;
        }

        if (message.StartsWith("DISC:"))
        {
            message = message.Substring(5).Trim();
            Disconnect();
        }
        Log(message);
    }

    // =============================================================================================
    // THREADING
    // =============================================================================================

    void StartThread(ThreadStart work)
    {
        Thread t = new Thread(work);
        t.IsBackground = true;

        lock (m_threads) m_threads.Add(t);
        t.Start();
    }

    // =============================================================================================
    // SOCKET HELPERS
    // =============================================================================================

    void CloseSocket(Socket socket)
    {
        if (socket == null) return;

        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            Log("Shutdown Socket: ERROR");
        }

        try
        {
            socket.Close();
        }
        catch
        {
            Log("Close Socket: ERROR");
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

            if (m_log.Count > 100) m_log.RemoveAt(0);
        }
    }

    void WarnTodo(int number, string detail)
    {
        lock (m_warned)
            if (!m_warned.Add(number)) return;

        Log("[TODO " + number + "] not done yet: " + detail);
    }

    // =============================================================================================
    // COMMAND LINE
    // =============================================================================================

    void ParseCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-ip" && i + 1 < args.Length)
            {
                serverIp = args[++i];
            }
            else if (args[i] == "-port" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out port);
            }
            else if (args[i] == "-name" && i + 1 < args.Length)
            {
                userName = args[++i];
            }
            else if (args[i] == "-autostart")
            {
                autoStart = true;
            }
            else if (args[i] == "-noautostart")
            {
                autoStart = false;
            }
        }
    }

    // =============================================================================================
    // DEBUG / WAITING ROOM UI
    // =============================================================================================

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10,10,Screen.width - 20,Screen.height - 20));
        GUILayout.Label("TCP CLIENT / WAITING ROOM");
        GUILayout.Label("Server: " + serverIp + ":" + port);
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label("User name: ", GUILayout.Width(75));
        userName = GUILayout.TextField(userName, GUILayout.Width(100));
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        // Connection controls.
        if (!m_running)
        {
            if (GUILayout.Button("Connect",GUILayout.Width(150)))
                StartNetwork();
        }
        else
        {
            GUILayout.Label("Connected");
            if (GUILayout.Button("Disconnect",GUILayout.Width(150)))
                Disconnect();
        }

        GUILayout.Space(20);
        GUILayout.Label("Message:");
        GUILayout.BeginHorizontal();

        if (m_running)
        {
            m_messageInput = GUILayout.TextField(m_messageInput,GUILayout.Width(300));
            if (GUILayout.Button("Send", GUILayout.Width(100)))
                SendMessageToServer();
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(20);
        GUILayout.Label("Players (" + m_players.Count + "):");
        foreach (string p in m_players)
            GUILayout.Label("  - " + p);

        GUILayout.Space(20);
        GUILayout.Label("Chat:");

        lock (m_log)
        {
            for (int i = Mathf.Max(0, m_log.Count - 15); i < m_log.Count; i++)
                GUILayout.Label(m_log[i]);
        }
        GUILayout.EndArea();
    }

    // =============================================================================================
    // TCP IMPLEMENTATION
    // =============================================================================================

    Socket StartClient()
    {
        Socket socket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
        socket.Connect(new IPEndPoint(IPAddress.Parse(serverIp),port));
        return socket;
    }

    int SendRaw(Socket socket, byte[] data)
    {
        return socket.Send(data);
    }

    int ReceiveRaw(Socket socket,byte[] buffer, int offset,int count)
    {
        return socket.Receive(buffer,offset,count,SocketFlags.None);
    }
}