// =================================================================================================
//  Lab 7 - World state replication: LINKING CONTEXT
//  XJO - Xarxes per a Jocs Online
// =================================================================================================
//
//  Given. Nothing to modify. The finished linking context of Lab 6 (slide 15), network ID <->
//  GameObject, one per machine. Here so this handout works whether or not you did Lab 6.
//    Server:  int id = linking.Register(go);      a new ID, never reused
//    Client:  linking.Register(go, id);           the ID the server sent
//    Both:    linking.Find(id)                    null if unknown, never throws
//             linking.Unregister(id)              when the object is destroyed
//
//  THEN, IN YOUR GAME
//    If you have your own LinkingContext from Lab 6, keep it. It does the same.
//
// =================================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Lab7
{
    public class Lab7LinkingContext
    {
        readonly Dictionary<int, GameObject> m_objects = new Dictionary<int, GameObject>();
        int m_nextId = 1;                              // 0 means "no ID"

        public int Register(GameObject go)             // server, creates a new ID
        {
            int id = m_nextId++;
            m_objects[id] = go;
            return id;
        }

        public void Register(GameObject go, int id)    // client, uses the ID it was given
        {
            m_objects[id] = go;
        }

        public GameObject Find(int id)                 // both, null if unknown
        {
            GameObject go;
            return m_objects.TryGetValue(id, out go) ? go : null;
        }

        public void Unregister(int id)                 // both, when the object is destroyed
        {
            m_objects.Remove(id);
        }

        // How many objects have a network ID right now, and which. For the debug overlay and the test.
        public int Count { get { return m_objects.Count; } }
        public IEnumerable<int> Ids { get { return m_objects.Keys; } }
    }
}
