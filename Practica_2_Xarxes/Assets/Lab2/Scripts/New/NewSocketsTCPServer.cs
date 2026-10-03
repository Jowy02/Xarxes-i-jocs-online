using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NewSocketsTCPServer : MonoBehaviour
{
    public int port = 9050;
    public string serverName = "MyServer";
    public bool autoStart = true;

    const int MaxPacketSize = 64 * 1024;

    struct Packet
    {
        public byte[] data;
        public Socket from;
    }

    Socket m_listener;

    readonly List<Socket> m_clients = new List<Socket>();
    readonly List<Thread> m_threads = new List<Thread>();
    readonly ConcurrentQueue<Packet> m_inbox = new ConcurrentQueue<Packet>();
    readonly List<string> m_log = new List<string>();
    readonly List<string> m_messages =  new List<string>();
    readonly Dictionary<Socket, string> m_userNames = new Dictionary<Socket, string>();
    readonly HashSet<int> m_warned = new HashSet<int>();

    volatile bool m_running;

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

        StartThread(ServerThread);
    }

    public void Disconnect()
    {
        if (!m_running) return;
        m_running = false;

        Socket[] clients;

        lock (m_clients)
        {
            clients = m_clients.ToArray();
            m_clients.Clear();
        }

        foreach (Socket client in clients)
        {
            CloseSocket(client);
        }

        // Clear usernames.
        lock (m_userNames)
        {
            m_userNames.Clear();
        }

        // Close listener.
        CloseSocket(m_listener);
        m_listener = null;

        // Wait for threads.
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
            OnPacketReceived(packet.data,packet.from);
    }

    // =============================================================================================
    // SERVER THREAD
    // =============================================================================================

    void ServerThread()
    {
        try
        {
            m_listener = StartServer();
        }
        catch (SocketException e)
        {
            Log("[SERVER] Could not start: " + e.SocketErrorCode +
                (e.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? " (port " + port + " is still used by another instance)": "")
            );

            m_running = false;
            return;
        }

        if (m_listener == null)
        {
            WarnTodo(1,"StartServer() returned null");

            m_running = false;
            return;
        }

        Log("[SERVER] Listening on " + port);

        while (m_running)
        {
            Socket client;

            try
            {
                client = AcceptClient();
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (client == null)
            {
                WarnTodo(2,"AcceptClient() returned null");

                continue;
            }

            lock (m_clients)
            {
                m_clients.Add(client);
            }

            Log("[SERVER] Client connected: " +client.RemoteEndPoint);

            Socket captured = client;

            StartThread(delegate{ClientThread(captured);});
        }
    }

    // =============================================================================================
    // CLIENT THREAD
    // =============================================================================================

    void ClientThread(Socket client)
    {
        ReceiveLoop(client);

        string username = null;

        // Get username before removing the client.
        lock (m_userNames)
        {
            if (m_userNames.TryGetValue(client, out username))
                m_userNames.Remove(client);
        }

        lock (m_clients)
            m_clients.Remove(client);

        Log("[SERVER] Client disconnected" +
            (string.IsNullOrEmpty(username)? "": ": " + username));

        CloseSocket(client);

        if (!string.IsNullOrEmpty(username) && m_running)   
            BroadcastPlayers();
    }

    // =============================================================================================
    // SEND
    // =============================================================================================

    public void SendPacket( byte[] payload,Socket to)
    {
        if (to == null || payload == null) return;

        byte[] framed = new byte[4 + payload.Length];
        BitConverter.GetBytes(payload.Length).CopyTo(framed, 0);
        payload.CopyTo(framed,4);

        try
        {
            int sent = SendRaw( to,framed);

            if (sent < 0) WarnTodo(3, "SendRaw() is not implemented yet");
        }
        catch (SocketException e)
        {
            Log("[SERVER] Send failed: " + e.SocketErrorCode);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void SendString(string text,Socket to)
    {
        SendPacket(Encoding.UTF8.GetBytes(text),to);
    }

    // =============================================================================================
    // BROADCAST
    // =============================================================================================

    void BroadcastMessage(string message,  Socket sender)
    {
        Socket[] clients;

        // Make a copy so the list can safely change while sending.
        lock (m_clients)
            clients = m_clients.ToArray();

        foreach (Socket client in clients)
            SendString(message,client);
    }

    void BroadcastPlayers()
    {
        string list;

        lock (m_userNames)
            list = string.Join(",", m_userNames.Values);

        BroadcastMessage("PLAYERS|" + list, null);
    }

    // =============================================================================================
    // RECEIVE
    // =============================================================================================

    void ReceiveLoop(Socket socket)
    {
        byte[] header = new byte[4];

        while (m_running)
        {
            if (!ReadExactly(socket,header,4)) return;

            int size = BitConverter.ToInt32(header, 0);

            if (size <= 0 || size > MaxPacketSize)
            {
                Log("[SERVER] Invalid packet size: " + size);
                return;
            }

            byte[] payload = new byte[size];

            if (!ReadExactly(socket,payload,size)) return;

            // Network thread -> Unity main thread.
            m_inbox.Enqueue(new Packet {data = payload,from = socket});
        }
    }

    bool ReadExactly(Socket socket,byte[] buffer, int count)
    {
        int total = 0;

        while (total < count)
        {
            int read;

            try
            {
                read = ReceiveRaw(socket,buffer,total,count - total);
            }
            catch (SocketException e)
            {
                Log("[SERVER] Connection lost: " +e.SocketErrorCode);
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (read < 0)
            {
                WarnTodo(4,"ReceiveRaw() is not implemented yet");
                return false;
            }

            // 0 = client closed the connection.
            if (read == 0) return false;

            total += read;
        }

        return true;
    }

    // =============================================================================================
    // MESSAGE HANDLING
    // =============================================================================================

    void OnPacketReceived(byte[] data, Socket from)
    {
        string message = Encoding.UTF8.GetString(data);

        // =========================================================================================
        // PLAYER JOIN
        // =========================================================================================

        if (message.StartsWith("JOIN|"))
        {
            string username = message.Substring(5).Trim();
            lock (m_userNames)
            {
                if (string.IsNullOrEmpty(username) || m_userNames.ContainsValue(username))
                {
                    Log("[SERVER] Player join with empty username");
                    SendString("DISC|Invalid user name", from);

                    return;
                }
                else m_userNames[from] = username;
            }

            Log("[SERVER] Player joined: " + username);
            BroadcastPlayers();

            //Send all old messages to the new player.
            lock (m_messages)
            {
                foreach (string oldmessage in m_messages)
                    SendString(oldmessage, from);
            }

            return;
        }

        // =========================================================================================
        // CHAT MESSAGE
        // =========================================================================================

        if (message.StartsWith("CHAT|"))
        {
            string chatMessage = message.Substring(5).Trim();

            if (string.IsNullOrEmpty(chatMessage)) return;

            string username = "Unknown";

            lock (m_userNames) m_userNames.TryGetValue(from,out username);

            string formattedMessage = username + ": " + chatMessage;

            Log("[SERVER] Received: " + formattedMessage);

            // Store the formatted chat message.
            lock (m_messages) m_messages.Add(formattedMessage);

            // Send to everyone EXCEPT the sender.
            BroadcastMessage(formattedMessage, from);

            return;
        }

        Log("[SERVER] Unknown message: " +message);
    }

    // =============================================================================================
    // THREADING
    // =============================================================================================

    void StartThread(ThreadStart work)
    {
        Thread t = new Thread(work);

        t.IsBackground = true;

        lock (m_threads)m_threads.Add(t);

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
        }

        try
        {
            socket.Close();
        }
        catch
        {
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

    void WarnTodo(int number,string detail)
    {
        lock (m_warned)
        {
            if (!m_warned.Add(number)) return;
        }

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
            if (args[i] == "-port" && i + 1 < args.Length)
            {
                int.TryParse(args[++i],out port);
            }
            else if (args[i] == "-name" && i + 1 < args.Length)
            {
                serverName = args[++i];
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
    // SERVER DEBUG UI
    // =============================================================================================

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10,10, Screen.width - 20, Screen.height - 20));
        GUILayout.Label("TCP SERVER");
        GUILayout.Label("Port: " + port);
        GUILayout.Space(10);

        if (!m_running)
        {
            if (GUILayout.Button("Start Server",GUILayout.Width(150)))
                StartNetwork();
        }
        else
        {
            GUILayout.Label("Server running");

            if (GUILayout.Button("Stop Server",GUILayout.Width(150)))
                Disconnect();
        }

        GUILayout.Space(20);
        GUILayout.Label("Stored messages:");

        lock (m_messages)
        {
            for (int i = 0;i < m_messages.Count;i++)
                GUILayout.Label(i + ": " +m_messages[i]);
        }

        GUILayout.Space(20);
        GUILayout.Label("Server log:");

        lock (m_log)
        {
            for (int i = Mathf.Max( 0,m_log.Count - 10); i < m_log.Count; i++)
                GUILayout.Label(m_log[i]);
        }

        GUILayout.EndArea();
    }

    // =============================================================================================
    // TCP IMPLEMENTATION
    // =============================================================================================

    Socket StartServer()
    {
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any,port));
        socket.Listen(10);

        return socket;
    }

    Socket AcceptClient()
    {
        return m_listener.Accept();
    }

    int SendRaw( Socket socket,byte[] data)
    {
        return socket.Send(data);
    }

    int ReceiveRaw(Socket socket,byte[] buffer, int offset, int count)
    {
        return socket.Receive(buffer, offset, count, SocketFlags.None);
    }
}