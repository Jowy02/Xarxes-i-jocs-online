using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PickupManagerServer
{
    const int FirstPickupId = 1000; // 0-999 for players, 1000+ for pickups 
    const float PickupRadius = 3f;
    const float RespawnDelay = 8f;

    private readonly ReplicationManagerServer m_replication;
    private readonly NewSocketsUDPServer m_server;
    private readonly Dictionary<int, BoostPickupState> m_pickups = new Dictionary<int, BoostPickupState>();
    private readonly List<(Vector3 position, float time)> m_respawns = new List<(Vector3, float)>();
    private int m_nextId = FirstPickupId;
    private List<Vector3> m_spawnPositions = new List<Vector3>();

    public PickupManagerServer(ReplicationManagerServer replication, NewSocketsUDPServer server)
    {
        m_replication = replication;
        m_server = server;
    }

    public BoostPickupState Find(int id)
    {
        BoostPickupState pickup;
        return m_pickups.TryGetValue(id, out pickup) ? pickup : null;
    }

    public void StartRound(List<Vector3> positions)
    {
        m_spawnPositions = new List<Vector3>(positions);
        Restart();
    }

    public void Clear()
    {
        foreach (int id in m_pickups.Keys) m_replication.MarkDestroy(id);
        m_pickups.Clear();
        m_respawns.Clear();
    }

    public void Restart()
    {
        Clear();
        foreach (Vector3 position in m_spawnPositions) Spawn(position);
    }

    public void Tick(List<UDPPlayer> players)
    {
        // Respawn pickups
        for (int i = m_respawns.Count - 1; i >= 0; i--)
        {
            if (Time.time >= m_respawns[i].time)
            {
                Spawn(m_respawns[i].position);
                m_respawns.RemoveAt(i);
            }
        }

        // Pickup logic
        foreach (BoostPickupState pickup in m_pickups.Values.ToList())
        {
            foreach (UDPPlayer player in players)
            {
                if (!player.hasReceivedTransform) continue;

                if (Vector3.Distance(player.carNetworkState.position, pickup.position) < PickupRadius)
                {
                    Collect(pickup, player);
                    break;
                }
            }
        }

        foreach (int id in m_pickups.Keys) m_replication.MarkUpdate(id);
    }

    void Spawn(Vector3 position)
    {
        int id = m_nextId++;
        m_pickups[id] = new BoostPickupState(id, position);
        m_replication.MarkCreate(id);
        Debug.Log("[SERVER] Pickup " + id + " created");
    }

    void Collect(BoostPickupState pickup, UDPPlayer player)
    {
        m_replication.MarkDestroy(pickup.id);
        m_pickups.Remove(pickup.id);
        m_respawns.Add((pickup.position, Time.time + RespawnDelay));
        Debug.Log("[SERVER] Pickup " + pickup.id + " collected by " + player.name);
        m_server.SendBoost(player, pickup.id);
    }
}
