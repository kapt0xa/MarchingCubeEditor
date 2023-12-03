using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReverseArray
{
    static public Dictionary<T, int> BuildReverse<T>(T[] values)
    {
        Dictionary<T, int> result = new Dictionary<T, int>();
        for (int i = 0; i < values.Length; ++i) 
        {
            result.Add(values[i], i);
        }
        return result;
    }
}
