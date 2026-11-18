using System.IO;
using UnityEngine;

public class CarNetObject : NetObject
{
    public bool isDrifting;

    void Awake()
    {
        type = 0; // 0 = Car
    }

    public override void Write(BinaryWriter w)
    {
        base.Write(w);
        w.Write(isDrifting);
    }

    public override void Read(BinaryReader r)
    {
        base.Read(r);
        isDrifting = r.ReadBoolean();
    }
}