using UnityEngine;
using System.Net;
using System.Collections.Generic;
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
    // Binary network state for vehicle synchronization (30 bytes total)
    public struct CarNetworkState
    {
        public int objectId;
        public Vector3 position;
        public Vector3 rotation;
        public bool isDrifting;
    }

    [Serializable]
    public struct PlayerDTO
    {
        public string playerName;
        public string msgType;
        public Vector3 pos;
        public string id;
    }
    public enum MsgType : byte
    {
        Text = 1,
        Transform = 2
    }
}