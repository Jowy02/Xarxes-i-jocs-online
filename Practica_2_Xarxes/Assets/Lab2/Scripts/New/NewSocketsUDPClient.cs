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

public class NewSocketsUDPClient : MonoBehaviour
{
    public string serverIp = "127.0.0.1";
    public int port = 9050;
    public bool autoStart = true;

    const int MaxPacketSize = 64 * 1024;

    Socket m_socket;
    EndPoint m_serverEndPoint;

    readonly List<Thread> m_threads = new List<Thread>();
    readonly ConcurrentQueue<byte[]> m_inbox = new ConcurrentQueue<byte[]>();
    volatile bool m_running;

    // Minigame variables
    float m_pingTimer = 0f;
    readonly Dictionary<string, GameObject> m_playerCapsules = new Dictionary<string, GameObject>();
    string posMsg = "POS:{0:0},{0:0},{0:0}";

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

        Thread t = new Thread(ClientThread);
        t.IsBackground = true;
        m_threads.Add(t);
        t.Start();
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

        foreach (GameObject capsule in m_playerCapsules.Values)
        {
            if (capsule != null)
            {
                Destroy(capsule);
            }
        }
        m_playerCapsules.Clear();

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
            string msg = Encoding.UTF8.GetString(data);
            ProcessGameMessage(msg);
        }

        if (!m_running) return;

        // Send PING every 1 second exact
        m_pingTimer -= Time.deltaTime;
        if (m_pingTimer <= 0)
        {
            SendString("PING:");
            SendString(posMsg);
            m_pingTimer = 1f;
        }

        // Click-to-move system
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            float rayDistance;

            if (groundPlane.Raycast(ray, out rayDistance))
            {
                Vector3 point = ray.GetPoint(rayDistance);

                // Format using InvariantCulture to force DOT as decimal separator
                posMsg = string.Format(CultureInfo.InvariantCulture, "POS:{0:F2},{1:F2},{2:F2}", point.x, point.y, point.z);
                //SendString(posMsg);
            }
        }
    }

    // =============================================================================================
    // PACKET HANDLING
    // =============================================================================================

    void ProcessGameMessage(string message)
    {
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

        if (message.StartsWith("POS:"))
        {
            string[] parts = message.Split(':');
            // [0]="POS", [1]="127.0.0.1", [2]="54321", [3]="10.5,0,4.2"
            if (parts.Length < 4) return;

            string id = parts[1] + ":" + parts[2]; // IP and Port as unique ID
            string coords = parts[3]; // Real coordinates

            string[] axis = coords.Split(',');
            if (axis.Length < 3) return;

            // Parse coordinates
            float x = float.Parse(axis[0], CultureInfo.InvariantCulture);
            float y = float.Parse(axis[1], CultureInfo.InvariantCulture);
            float z = float.Parse(axis[2], CultureInfo.InvariantCulture);
            Vector3 targetPos = new Vector3(x, y, z);

            // Create a capsule if this is a new player
            if (!m_playerCapsules.ContainsKey(id))
            {
                GameObject newCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                newCapsule.GetComponent<Renderer>().material.color = Color.red;
                m_playerCapsules[id] = newCapsule;
            }

            // Move the capsule to the target position
            m_playerCapsules[id].transform.position = targetPos;
        }
    }

    // =============================================================================================
    // SEND
    // =============================================================================================

    void SendString(string text)
    {
        if (m_socket == null || m_serverEndPoint == null) return;

        byte[] payload = Encoding.UTF8.GetBytes(text);
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

        Debug.Log("[CLIENT] UDP Ready, connecting to " + m_serverEndPoint);

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
}