using System.IO;
using UnityEngine;

public static class NetworkSerializer
{
    // =============================================================================================
    // SERIALIZATION
    // =============================================================================================

    // Serializes car transform and gameplay state into a 30-byte binary payload
    public static byte[] SerializeCar(int objectId, Vector3 pos, Vector3 rot, bool isDrifting)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((byte)UDPPlayer.MsgType.Transform); // Byte 0: Type identifier
            writer.Write(objectId);                          // Bytes 1-4: Object / Player ID
            writer.Write(pos.x);                             // Bytes 5-8: Position X
            writer.Write(pos.y);                             // Bytes 9-12: Position Y
            writer.Write(pos.z);                             // Bytes 13-16: Position Z
            writer.Write(rot.x);                             // Bytes 17-20: Pitch Inclination
            writer.Write(rot.y);                             // Bytes 21-24: Yaw Turn
            writer.Write(rot.z);                             // Bytes 25-28: Roll Banked curve
            writer.Write(isDrifting);                        // Byte 21: Drift

            return stream.ToArray();
        }
    }

    // =============================================================================================
    // DESERIALIZATION
    // =============================================================================================

    // Reads the 30-byte binary payload back into a CarNetworkState structure
    public static UDPPlayer.CarNetworkState DeserializeCar(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            byte type = reader.ReadByte();

            UDPPlayer.CarNetworkState state = new UDPPlayer.CarNetworkState
            {
                objectId = reader.ReadInt32(),
                position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                isDrifting = reader.ReadBoolean()
            };

            return state;
        }
    }
}