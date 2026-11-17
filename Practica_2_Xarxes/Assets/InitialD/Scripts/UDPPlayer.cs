using UnityEngine;
using System.Net;
using System.Collections.Generic;
using System;
using System.IO;

public class UDPPlayer
{
    public string name;
    public EndPoint endpoint;
    public int id;

    public Vector3 position;
    public Vector3 rotation;
    public bool isDrifting;

    public UDPPlayer(string name, EndPoint endpoint, int id = 0)
    {
        this.name = name;
        this.endpoint = endpoint;
        this.id = id;
    }
    // Binary network state for vehicle synchronization (30 bytes total)
    public struct CarNetworkState
    {
        public int objectId;
        public Vector3 position;
        public Vector3 rotation;
        public bool isDrifting;
    }
    public virtual void Write(BinaryWriter w)
    {
        w.Write(position.x);                             // Bytes 5-8: Position X
        w.Write(position.y);                             // Bytes 9-12: Position Y
        w.Write(position.z);                             // Bytes 13-16: Position Z
        w.Write(rotation.x);                             // Bytes 17-20: Pitch Inclination
        w.Write(rotation.y);                             // Bytes 21-24: Yaw Turn
        w.Write(rotation.z);                             // Bytes 25-28: Roll Banked curve
        w.Write(isDrifting);                        // Byte 29: Drift
    }
    [Serializable]
    public enum MsgType : byte{ Text = 1, Transform = 2, Replication = 5 }
    public enum ReplicationAction : byte { Create = 1, Update = 2, Destroy = 3 }
}