using System.IO;
using UnityEngine;

public class BoostPickupNetObject : NetObject
{
    public const int DataSize = 12; // 3 floats for position

    void Awake()
    {
        type = (byte)UDPPlayer.ObjectType.BoostPickup;
    }

    public override void Write(BinaryWriter w)
    {
        w.Write(transform.position.x);
        w.Write(transform.position.y);
        w.Write(transform.position.z);
    }

    public override void Read(BinaryReader r)
    {
        transform.position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}