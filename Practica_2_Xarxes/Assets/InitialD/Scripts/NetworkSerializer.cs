using System.IO;
using UnityEngine;

public static class NetworkSerializer
{
    public static byte[] SerializeCar(int objectId, Vector3 pos, Vector3 rot, bool isDrifting, int skinId)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((byte)UDPPlayer.MsgType.Transform);
            writer.Write(objectId);
            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(pos.z);
            writer.Write(rot.x);
            writer.Write(rot.y);
            writer.Write(rot.z);
            writer.Write(isDrifting);
            writer.Write(skinId);

            return stream.ToArray();
        }
    }

    public static UDPPlayer.CarNetworkState DeserializeCar(BinaryReader reader, int objectID)
    {
        return new UDPPlayer.CarNetworkState
        {
            objectId = objectID,
            position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            isDrifting = reader.ReadBoolean(),
            skinId = reader.ReadInt32()
        };
    }
}