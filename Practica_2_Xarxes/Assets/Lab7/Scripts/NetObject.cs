// =================================================================================================
//  Lab 7 - World state replication: NET OBJECT
//  XJO - Xarxes per a Jocs Online
// =================================================================================================
//
//  Given. Nothing to modify. Same code as slide 9.
//    Every networked object writes and reads itself. The replication manager does not know what a
//    player or a bullet is, it only calls Write and Read.
//    Read must read exactly what Write wrote, in the same order. One byte off and the rest of the
//    packet is garbage.
//
//  THEN, IN YOUR GAME
//    Add NetObject (or your own subclass, like NetPlayer.cs) to every networked prefab. Set 'type' to the index of
//    that prefab in the client's prefab list, the same order on both sides (slide 13).
//    Everything in this handout is in the namespace Lab7, so it does not clash with your own
//    classes. Add "using Lab7;" at the top of your files, or remove the namespace when you copy.
//
// =================================================================================================

using System.IO;
using UnityEngine;

namespace Lab7
{
    public class NetObject : MonoBehaviour
    {
        public int id;          // network ID, from the linking context
        public byte type;       // which prefab, the same table on both sides

        public virtual void Write(BinaryWriter w)
        {
            w.Write(transform.position.x);
            w.Write(transform.position.y);
            w.Write(transform.position.z);
            w.Write(transform.eulerAngles.y);
        }

        public virtual void Read(BinaryReader r)
        {
            transform.position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            transform.rotation = Quaternion.Euler(0f, r.ReadSingle(), 0f);
        }
    }
}
