// =================================================================================================
//  Lab 7 - World state replication: REPLICATION MANAGER, CLIENT
//  XJO - Xarxes per a Jocs Online
// =================================================================================================
//
//  THE EXERCISE
//    Read the packet that ReplicationManagerServer wrote, in the same order (slide 13).
//    Create and Update are read the same way, find the object or create it, then read its fields.
//    A Create for a known ID is an Update, a Destroy for an unknown ID is ignored. That is what
//    heals lost packets and late joiners (slides 14 and 15).
//
//  HOW TO RUN IT
//    Open the scene S_ReplicationTest and press Play. Each check says PASS, FAIL or SKIPPED.
//
//  WHAT YOU HAVE TO DO
//    TODO 3, 4 and 5 below, in that order. TODO 5 sits above TODO 4 in the code because a Destroy
//    has nothing more to read, but the tests need objects to exist before they can be destroyed.
//    Delete the Lab7Todo.NotDone(...) line and write yours.
//    Stuck? Disable the GameObject "ReplicationTest", enable "ReplicationTestSolution", and compare.
//    Its code is in ReplicationManagerClientSolution.cs.
//
//  THEN, IN YOUR GAME
//    Add this component on the client, fill m_prefabs in the Inspector (index 0 player, 1 bullet...
//    the same order as 'type' on the server), and call Init with your client's linking context.
//    From your OnPacketReceived, on the main thread: read the MsgType byte, and if it is
//    Replication, call ReadPacket with the same reader.
//
// =================================================================================================

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lab7
{
    public class ReplicationManagerClient : MonoBehaviour
    {
        [SerializeField] List<GameObject> m_prefabs = new List<GameObject>();   // index = type
        Lab7LinkingContext m_linking = new Lab7LinkingContext();
        int m_lastSequence = -1;

        // The client's linking context, and optionally the prefab list if not set in the Inspector.
        public void Init(Lab7LinkingContext linking, List<GameObject> prefabs = null)
        {
            m_linking = linking;
            if (prefabs != null) m_prefabs = prefabs;
        }

        public void ReadPacket(BinaryReader reader)              // the MsgType byte is already read
        {
            // --------------------------------------------------------------------------------- TODO 3 ---
            //  Goal: read the sequence number. If it is not newer than the last one you kept, this
            //  packet is old, ignore all of it. Otherwise remember it in m_lastSequence (slide 15).
            //  Test scene when it works:  (h) old packet after a newer one is dropped
            // your code here
            Lab7Todo.NotDone(3, "ReadPacket() does not read the sequence yet");

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                ReplicationAction action = (ReplicationAction)reader.ReadByte();
                GameObject go = m_linking.Find(id);              // null if this client does not know it

                if (action == ReplicationAction.Destroy)
                {
                    // ----------------------------------------------------------------------------- TODO 5 ---
                    //  Goal: if this client has the object, destroy it and forget its ID. If not, it is
                    //  already gone, nothing to do (slide 13).
                    //  Test scene when it works:  (d) Destroy, the client has 2 objects
                    // your code here
                    Lab7Todo.NotDone(5, "ReadPacket() destroys nothing yet");
                    continue;                                    // unknown ID, already gone
                }

                // --------------------------------------------------------------------------------- TODO 4 ---
                //  Goal: Create or Update. Read the type. No object yet? Create it from m_prefabs, give it
                //  its ID, register it with that ID. Then let the object read its own fields (slide 13).
                //  Test scene when it works:  (b) 3 Creates, the client has the same world
                // your code here
                Lab7Todo.NotDone(4, "ReadPacket() creates and updates nothing yet");
            }
        }
    }
}
