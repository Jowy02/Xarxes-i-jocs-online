// =================================================================================================
//  Lab 7 - World state replication: NET PLAYER
//  XJO - Xarxes per a Jocs Online
// =================================================================================================
//
//  Given. Nothing to modify. Same code as slide 9.
//    A NetObject with one extra field, 4 more bytes. Use it as the model for your own subclasses,
//    one file per class, named like the class, so Unity lets you add it to a prefab.
//
// =================================================================================================

using System.IO;

namespace Lab7
{
    public class NetPlayer : NetObject
    {
        public int health = 100;
        public override void Write(BinaryWriter w) { base.Write(w); w.Write(health); }
        public override void Read(BinaryReader r)  { base.Read(r); health = r.ReadInt32(); }
    }
}
