using System.Collections.Generic;
using UnityEngine;

public class LinkingContext
{
    private readonly Dictionary<int, GameObject> m_objects = new Dictionary<int, GameObject>();
    private int m_nextId = 1;

    public int Register(GameObject go)
    {
        int id = m_nextId++;
        m_objects[id] = go;
        return id;
    }

    public void Register(GameObject go, int id)
    {
        m_objects[id] = go;
    }

    public GameObject Find(int id)
    {
        GameObject go;
        return m_objects.TryGetValue(id, out go) ? go : null;
    }

    public void Unregister(int id)
    {
        m_objects.Remove(id);
    }

    public int Count => m_objects.Count;
}