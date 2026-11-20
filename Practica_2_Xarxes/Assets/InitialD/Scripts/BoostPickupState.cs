using System.IO;
using UnityEngine;

public class BoostPickupState
{
    public int id;
    public Vector3 position;

    public BoostPickupState(int id, Vector3 position)
    {
        this.id = id;
        this.position = position;
    }

    public void Write(BinaryWriter w)
    {
        w.Write(position.x);
        w.Write(position.y);
        w.Write(position.z);
    }
}
