using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class BubbleSortSolution : MonoBehaviour
{
    float[] array;
    List<GameObject> mainObjects;
    public GameObject prefab;

    Thread sortThread;
    //Written by the main thread, read by the sort thread, so it must be volatile
    volatile bool stopSorting = false;
    //Once the thread has finished and no heights changed, Update stops calling updateHeights()
    bool heightsDirty = true;

    void Start()
    {
        mainObjects = new List<GameObject>();
        array = new float[30000];
        for (int i = 0; i < 30000; i++)
        {
            array[i] = (float)Random.Range(0, 1000)/100;
        }

        //TO DO 4
        //Call the three previous functions in order to set up the exercise
        logArray();
        spawnObjs();
        updateHeights();

        //TO DO 5
        //Create a new thread using the function "bubbleSort" and start it.
        sortThread = new Thread(bubbleSort);
        sortThread.IsBackground = true;
        sortThread.Start();
    }

    void Update()
    {
        //TO DO 6
        //Call updateHeights() in order to update our object list.
        //Since we'll be calling UnityEngine functions to retrieve and change some data,
        //we can't call this function inside a Thread
        if (heightsDirty)
        {
            bool changed = updateHeights();
            //Only stop once the thread is done: a frame with no changes does not mean the sort has finished
            if (!changed && !sortThread.IsAlive)
            {
                heightsDirty = false;
                Debug.Log("Sorting finished, heights no longer updated");
            }
        }
    }

    //Stop the thread when leaving Play Mode, otherwise it keeps running inside the Editor
    void OnDestroy()
    {
        stopSorting = true;
        if (sortThread != null && sortThread.IsAlive)
        {
            sortThread.Join();
        }
    }

    //TO DO 5
    //Create a new thread using the function "bubbleSort" and start it.
    void bubbleSort()
    {
        int i, j;
        int n = array.Length;
        bool swapped;
        for (i = 0; i < n- 1; i++)
        {
            if (stopSorting)
                return;

            swapped = false;
            for (j = 0; j < n - i - 1; j++)
            {
                if (array[j] > array[j + 1])
                {
                    (array[j], array[j+1]) = (array[j+1], array[j]);
                    swapped = true;
                }
            }
            if (swapped == false)
                break;
        }
        //You may debug log your Array here in case you want to. It will only be called one the bubble algorithm has finished sorting the array
    }

    void logArray()
    {
        string text = "";

        //TO DO 1
        //Simply show in the console what's inside our array.
        //string.Join avoids concatenating 30000 strings one by one
        text = string.Join(", ", array);

        Debug.Log(text);
    }

    void spawnObjs()
    {
        //TO DO 2
        //We should be storing our objects in a list so we can access them later on.

        for (int i = 0; i < array.Length; i++)
        {
            //We have to separate the objs accordingly to their width, in which case we divide their position by 1000.
            //If you decide to make your objs wider, don't forget to up this value

            GameObject obj = Instantiate(prefab, new Vector3((float)i / 1000,
                this.gameObject.GetComponent<Transform>().position.y, 0), Quaternion.identity);
            mainObjects.Add(obj);
        }

    }

    //TO DO 3
    //We'll just change the height of every obj in our list to match the values of the array.
    //To avoid calling this function once everything is sorted, keep track of new changes to the list.
    //If there weren't, you might as well stop calling this function

    bool updateHeights()
    {

        bool changed = false;
        for (int i = 0; i < array.Length; i++)
        {
            //The sort thread may be writing this value right now (no lock), so read it once
            float value = array[i];
            Vector3 currentScale = mainObjects[i].transform.localScale;
            if (currentScale.y != value)
            {
                mainObjects[i].transform.localScale = new Vector3(currentScale.x, value, currentScale.z);
                changed = true;
            }
        }
        return changed;
    }
}
