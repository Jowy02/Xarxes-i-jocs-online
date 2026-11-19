using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ReplicationManagerServer
{
    private readonly NewSocketsUDPServer m_server;
    private readonly Dictionary<int, UDPPlayer.ReplicationAction> m_pending = new Dictionary<int, UDPPlayer.ReplicationAction>();
    private readonly Dictionary<int, (UDPPlayer.ReplicationAction action, float until)> m_repeat = new Dictionary<int, (UDPPlayer.ReplicationAction, float)>();
    private int m_sequence = 0;

    public ReplicationManagerServer(NewSocketsUDPServer server)
    {
        m_server = server;
    }

    public void MarkCreate(int id)
    {
        m_pending[id] = UDPPlayer.ReplicationAction.Create;
        m_repeat[id] = (UDPPlayer.ReplicationAction.Create, Time.time + 1f);
    }

    public void MarkDestroy(int id)
    {
        m_pending[id] = UDPPlayer.ReplicationAction.Destroy;
        m_repeat[id] = (UDPPlayer.ReplicationAction.Destroy, Time.time + 1f);
    }

    public void MarkUpdate(int id)
    {
        if (!m_pending.ContainsKey(id))
            m_pending[id] = UDPPlayer.ReplicationAction.Update;
    }

    public byte[] WritePacket()
    {
        // Redundancy loop
        foreach (int id in m_repeat.Keys.ToList())
        {
            if (Time.time > m_repeat[id].until) m_repeat.Remove(id);
            else m_pending[id] = m_repeat[id].action;
        }

        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);

        // Header: MsgType 1b + Sequence 4b + Count 4b = 9 bytes
        writer.Write((byte)UDPPlayer.MsgType.Replication);
        writer.Write(m_sequence++);
        writer.Write(m_pending.Count);

        // Body
        foreach (KeyValuePair<int, UDPPlayer.ReplicationAction> entry in m_pending)
        {
            writer.Write(entry.Key);         // ID 4b
            writer.Write((byte)entry.Value); // Action 1b

            if (entry.Value == UDPPlayer.ReplicationAction.Destroy)
                continue;

            UDPPlayer targetPlayer = null;
            for (int i = 0; i < m_server.m_players.Count; i++)
            {
                if (m_server.m_players[i].id == entry.Key)
                {
                    targetPlayer = m_server.m_players[i];
                    break;
                }
            }

            if (targetPlayer != null)
            {
                writer.Write((byte)0); // Type 0 = Car
                targetPlayer.Write(writer);
            }
        }

        m_pending.Clear();
        return stream.ToArray();
    }
}