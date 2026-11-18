using System.IO;
using UnityEngine;

public class NetObject : MonoBehaviour
{
    public int id;
    public byte type; // 0 = Car, 1 = Pickup

    public virtual void Write(BinaryWriter w)
    {
        w.Write(transform.position.x);
        w.Write(transform.position.y);
        w.Write(transform.position.z);
        w.Write(transform.eulerAngles.x);
        w.Write(transform.eulerAngles.y);
        w.Write(transform.eulerAngles.z);
    }

    public virtual void Read(BinaryReader r)
    {
        transform.position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        transform.rotation = Quaternion.Euler(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}