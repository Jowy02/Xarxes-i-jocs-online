using UnityEngine;
using System.Net;
using System.IO;
using System;

public class UDPPlayer
{
    public string name;
    public EndPoint endpoint;
    public int id;

    public UDPPlayer(string name, EndPoint endpoint, int id = 0)
    {
        this.name = name;
        this.endpoint = endpoint;
        this.id = id;
    }

    public struct CarNetworkState
    {
        public int objectId;
        public Vector3 position;
        public Vector3 rotation;
        public bool isDrifting;
        public int skinId;
    }

    public CarNetworkState carNetworkState;

    public bool hasState;

    public virtual void Write(BinaryWriter w)
    {
        w.Write(carNetworkState.position.x);
        w.Write(carNetworkState.position.y);
        w.Write(carNetworkState.position.z);
        w.Write(carNetworkState.rotation.x);
        w.Write(carNetworkState.rotation.y);
        w.Write(carNetworkState.rotation.z);
        w.Write(carNetworkState.isDrifting);
        w.Write(carNetworkState.skinId);
    }

    [Serializable]
    public enum MsgType : byte { Text = 1, Transform = 2, Replication = 5 }
    public enum ReplicationAction : byte { Create = 1, Update = 2, Destroy = 3 }
    public enum ObjectType : byte { Car = 0, BoostPickup = 1 }
}