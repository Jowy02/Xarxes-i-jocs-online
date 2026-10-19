using UnityEngine;
using System.Net;
using System.Collections.Generic;
using System;

public class UDPPlayer
{
    public string name;
    public EndPoint endpoint;

    public UDPPlayer(string name, EndPoint endpoint)
    {
        this.name = name;
        this.endpoint = endpoint;
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
        Player = 2, 
    }
}
