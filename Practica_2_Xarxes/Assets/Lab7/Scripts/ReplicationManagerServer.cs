// =================================================================================================
//  Lab 7 - World state replication: REPLICATION MANAGER, SERVER
//  XJO - Xarxes per a Jocs Online
// =================================================================================================
//
//  THE EXERCISE
//    Your game marks what happened to each object (Create, Update, Destroy, slide 10). Once per
//    tick, WritePacket turns everything marked into one replication packet (slides 8 and 11):
//      [MsgType = Replication][sequence][count]
//          [id][Create ][type][fields...]
//          [id][Update ][type][fields...]
//          [id][Destroy]
//    The Mark methods and the 1 s repeat of Create and Destroy (slide 15) are given.
//
//  HOW TO RUN IT
//    Open the scene S_ReplicationTest and press Play. Each check says PASS, FAIL or SKIPPED.
//
//  WHAT YOU HAVE TO DO
//    TODO 1 and 2 below. Delete the Lab7Todo.NotDone(...) line and write yours.
//    Then TODO 3 to 5 in ReplicationManagerClient.cs.
//    Stuck? Disable the GameObject "ReplicationTest", enable "ReplicationTestSolution", and compare.
//    Its code is in ReplicationManagerServerSolution.cs.
//
//  THEN, IN YOUR GAME
//    One ReplicationManagerServer on the server, built with the server's linking context.
//    MarkCreate right after Register, MarkDestroy right after Unregister, MarkUpdate every tick
//    (slide 12), then send the bytes of WritePacket to every ClientProxy.
//    Add Replication to your own MsgType (Lab 3) and use yours instead of the one at the end of
//    this file.
//
// =================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Lab7
{
    public class ReplicationManagerServer
    {
        readonly Lab7LinkingContext m_linking;
        readonly Dictionary<int, ReplicationAction> m_pending = new Dictionary<int, ReplicationAction>();
        readonly Dictionary<int, (ReplicationAction action, float until)> m_repeat =
            new Dictionary<int, (ReplicationAction, float)>();
        int m_sequence;

        // The time in seconds, Time.time on slide 15. The test scene replaces it to replay exact
        // situations (a lost Create, a lost Destroy). In your game, leave it as it is.
        public Func<float> clock = () => Time.time;

        public ReplicationManagerServer(Lab7LinkingContext linking) { m_linking = linking; }

        // ------------------------------------------------------------- given, slides 10 and 15 ---
        public void MarkCreate(int id)
        {
            m_pending[id] = ReplicationAction.Create;
            m_repeat[id] = (ReplicationAction.Create, clock() + 1f);       // repeated for 1 s
        }

        public void MarkDestroy(int id)
        {
            m_pending[id] = ReplicationAction.Destroy;
            m_repeat[id] = (ReplicationAction.Destroy, clock() + 1f);
        }

        public void MarkUpdate(int id)
        {
            if (!m_pending.ContainsKey(id))                                 // a Create or Destroy wins
                m_pending[id] = ReplicationAction.Update;
        }

        public byte[] WritePacket()                                         // once per tick
        {
            foreach (int id in m_repeat.Keys.ToList())                      // given, slide 15
            {
                if (clock() > m_repeat[id].until) m_repeat.Remove(id);
                else m_pending[id] = m_repeat[id].action;
            }

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            // ----------------------------------------------------------------------------- TODO 1 ---
            //  Goal: the header, 9 bytes. Mark the packet as Replication, then the sequence number,
            //  then how many entries follow (slides 8 and 11). Each packet's sequence is one more.
            //  Test scene when it works:  (a) N=0, the empty packet is 9 bytes
            // your code here
            Lab7Todo.NotDone(1, "WritePacket() writes no header yet");

            foreach (KeyValuePair<int, ReplicationAction> entry in m_pending)
            {
                // ------------------------------------------------------------------------- TODO 2 ---
                //  Goal: one entry. Identify the object (ID, action). A Destroy says nothing else.
                //  Otherwise its type, then its own fields. The object is in m_linking, NetObject.Write writes it (slide 11).
                //  Test scene when it works:  (c) Update of a player and a bullet is 9 + 26 + 22 = 57 bytes
                // your code here
                Lab7Todo.NotDone(2, "WritePacket() writes no entries yet");
            }
            m_pending.Clear();
            return stream.ToArray();
        }
    }

    // ===================================================================== shared by the handout ===

    // Lab 3's MsgType with Replication added (slide 8). In your game, add Replication to yours.
    public enum MsgType : byte { Text = 1, Player = 2, Transform = 3, Replication = 4 }

    // Slide 10.
    public enum ReplicationAction : byte { Create = 1, Update = 2, Destroy = 3 }

    // Thrown by a TODO you have not done yet. The test scene catches it and reports SKIPPED.
    // Not called Todo or Lab6Todo, so it does not clash with the Lab 3 or Lab 6 handouts.
    public class Lab7TodoException : Exception
    {
        public readonly int number;
        public Lab7TodoException(int number, string detail) : base(detail) { this.number = number; }
    }

    public static class Lab7Todo
    {
        public static void NotDone(int number, string detail) { throw new Lab7TodoException(number, detail); }
    }
}
