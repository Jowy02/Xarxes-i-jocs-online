using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

// Wraps SendTo. Delays, reorders and drops packets on purpose (lab slide 21)
public class NetSim : MonoBehaviour
{
    public float latency = 0f, jitter = 0f, dropChance = 0f; // all 0 = simulator transparent, set values in the Inspector

    // The socket of whoever owns this simulator (client or server)
    public Socket m_socket;

    readonly List<(float at, byte[] data, EndPoint to)> m_queue = new List<(float, byte[], EndPoint)>();

    public void Clear()
    {
        m_queue.Clear();
    }

    public void Send(byte[] data, EndPoint to)
    {
        // lost
        if (Random.value < dropChance) return;

        // jitter, may reorder
        float delay = Mathf.Max(0f, latency + Random.Range(-jitter, jitter));
        m_queue.Add((Time.time + delay, data, to));
    }

    void Update()
    {
        // in order, so no jitter means no reordering
        for (int i = 0; i < m_queue.Count;)
        {
            if (Time.time >= m_queue[i].at)
            {
                if (m_socket != null)
                {
                    try { m_socket.SendTo(m_queue[i].data, m_queue[i].to); } catch { }
                }
                m_queue.RemoveAt(i); // next moves to i
            }
            else i++;
        }
    }
}