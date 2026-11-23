using System.IO;
using UnityEngine;

public class CarNetObject : NetObject
{
    public const int DataSize = 29; // 6 floats + bool + int

    public bool isDrifting;
    public int skinId;

    void Awake()
    {
        type = 0; // 0 = Car
    }

    public override void Write(BinaryWriter w)
    {
        base.Write(w);
        w.Write(isDrifting);
        w.Write(skinId);
    }

    public override void Read(BinaryReader r)
    {
        base.Read(r);
        isDrifting = r.ReadBoolean();
        int newSkinId = r.ReadInt32();

        if (newSkinId != skinId)
        {
            skinId = newSkinId;
            PrometeoSkinManager skinManager = GetComponent<PrometeoSkinManager>();
            if (skinManager != null)
            {
                skinManager.ApplySkin(skinId);
            }
        }
    }
}