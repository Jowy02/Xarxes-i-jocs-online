using UnityEngine;
using System.Net;

public class UDPPlayer
{
    public string name;
    public EndPoint endpoint;

    public UDPPlayer(string name, EndPoint endpoint)
    {
        this.name = name;
        this.endpoint = endpoint;
    }
}