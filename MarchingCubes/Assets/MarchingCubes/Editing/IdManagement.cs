using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

public class IdManagement
{

    const float sqrt05 = 0.70710678118f; // sqrt(0.5)

    static public readonly Quaternion[] id_to_rotation = new Quaternion[24]
    {
        new Quaternion(0, 0, 0, 1),

        new Quaternion(0, 0, +sqrt05, sqrt05),
        new Quaternion(0, 0, -sqrt05, sqrt05),
        new Quaternion(0, +sqrt05, 0, sqrt05),
        new Quaternion(0, -sqrt05, 0, sqrt05),
        new Quaternion(+sqrt05, 0, 0, sqrt05),
        new Quaternion(-sqrt05, 0, 0, sqrt05),

        new Quaternion(0, 0, 1, 0),
        new Quaternion(0, 1, 0, 0),
        new Quaternion(1, 0, 0, 0),

        new Quaternion(+0.5f, +0.5f, +0.5f, 0.5f),
        new Quaternion(-0.5f, +0.5f, +0.5f, 0.5f),
        new Quaternion(+0.5f, -0.5f, +0.5f, 0.5f),
        new Quaternion(-0.5f, -0.5f, +0.5f, 0.5f),
        new Quaternion(+0.5f, +0.5f, -0.5f, 0.5f),
        new Quaternion(-0.5f, +0.5f, -0.5f, 0.5f),
        new Quaternion(+0.5f, -0.5f, -0.5f, 0.5f),
        new Quaternion(-0.5f, -0.5f, -0.5f, 0.5f),

        new Quaternion(sqrt05, +sqrt05, 0, 0),
        new Quaternion(sqrt05, -sqrt05, 0, 0),
        new Quaternion(0, sqrt05, +sqrt05, 0),
        new Quaternion(0, sqrt05, -sqrt05, 0),
        new Quaternion(+sqrt05, 0, sqrt05, 0),
        new Quaternion(-sqrt05, 0, sqrt05, 0),
    };

    static public readonly Dictionary<Quaternion, int> rotation_to_id = ReverseArray.BuildReverse(id_to_rotation);

    static private float NormalizedQuaternionCord(float cord)
    {
        if (cord < -0.85)
        {
            return -1;
        }
        else if (cord < -0.3)
        {
            return -sqrt05;
        }
        else if (cord < 0.3)
        {
            return 0;
        }
        else if (cord < 0.85)
        {
            return sqrt05;
        }
        else
        {
            return 1;
        }
    }

    static public Quaternion NormalisedQuaternion(Quaternion quaternion)
    {
        quaternion.x = NormalizedQuaternionCord(quaternion.x);
        quaternion.y = NormalizedQuaternionCord(quaternion.y);
        quaternion.z = NormalizedQuaternionCord(quaternion.z);
        quaternion.w = NormalizedQuaternionCord(quaternion.w);

        if (quaternion.w < 0 || (quaternion.w == 0 && !rotation_to_id.ContainsKey(quaternion)))
        {
            quaternion.x *= -1;
            quaternion.y *= -1;
            quaternion.z *= -1;
            quaternion.w *= -1;
        }

        if (!rotation_to_id.ContainsKey(quaternion))
        {
            throw new ArgumentException("can't repair quaternion, it is not similar to anything from table");
        }

        return quaternion;
    }

    public static bool[] CubeIdToFlags(int cube_id)
    {
        Assert.IsTrue(cube_id >= 0 && cube_id < 256);
        bool[] result = new bool[8];
        for (int i = 0; i < 8; ++i)
        {
            result[i] = ((cube_id & (1 << i)) != 0);
        }

        return result;
    }

    public static int BoolsToCubeId(bool[] material_flags)
    {
        Assert.IsTrue(material_flags.Length == 8);
        int result = 0;
        for (int i = 0; i < 8; ++i)
        {
            if (material_flags[i])
            {
                result |= 1 << i;
            }
        }
        return result;
    }

    public static int RotateCubeId(int cube_id, int rot_id)
    {
        bool[] material_flags = CubeIdToFlags(cube_id);
        bool[] rot_flags = new bool[8];
        for (int i = 0; i < 8; ++i)
        {
            if (material_flags[i])
            {
                rot_flags[RotateNode(i, rot_id)] = true;
            }
        }
        return BoolsToCubeId(rot_flags);
    }

    static public Vector3 RotateNode(Vector3 vect, int rot_id)
    {
        Vector3 offset = new Vector3(0.5f, 0.5f, 0.5f);
        vect -= offset;
        vect = id_to_rotation[rot_id] * vect;
        vect += offset;
        return Round(vect);
    }

    static public Vector3 Rotate(Vector3 vect, int rot_id)
    {
        vect = id_to_rotation[rot_id] * vect;
        return Round(vect);
    }

    static public int[] GetCubeIdRotations(int cube_id, bool add_mirrored = false)
    {
        HashSet<int> result_set = new HashSet<int>();
        for (int rot_id = 0; rot_id < 24; ++rot_id)
        {
            int variation = RotateCubeId(cube_id, rot_id);
            result_set.Add(variation);
            if (add_mirrored)
            {
                result_set.Add(MirrorCubeId(variation));
            }
        }
        int[] result = new int[result_set.Count];
        int i = 0;
        foreach (int id in result_set)
        {
            result[i++] = id;
        }
        return result;
    }

    static public int[][] GetGroupClassification()
    {
        bool[] is_sorted = new bool[256];
        List<int[]> result = new List<int[]>();
        for (int id = 0; id < 256; ++id)
        {
            if (!is_sorted[id])
            {
                int[] to_add = GetCubeIdRotations(id, true);
                foreach (int id_added in to_add)
                {
                    is_sorted[id_added] = true;
                }
                result.Add(to_add);
            }
        }
        return result.ToArray();
    }

    public static int MirrorCubeId(int cube_id)
    {
        bool[] material_flags = CubeIdToFlags(cube_id);
        bool[] mir_flags = new bool[8];
        for (int i = 0; i < 8; ++i)
        {
            if (material_flags[i])
            {
                mir_flags[MirrorNode(i)] = true;
            }
        }
        return BoolsToCubeId(mir_flags);
    }

    static public Vector3 MirrorNode(Vector3 vect) // mirror relative to central point
    {
        return id_to_cube_node[MirrorNode(cube_node_to_id[vect])];

    }

    static public int MirrorNode(int node_id) // mirror relative to central point
    {
        return 0b111 & (~node_id);
    }

    static public int RotateNode(int node_id, int rot_id)
    {
        return cube_node_to_id[RotateNode(id_to_cube_node[node_id], rot_id)];
    }

    static public readonly Vector3[] id_to_cube_node = new Vector3[8]
    {
        new Vector3(0,0,0),
        new Vector3(1,0,0),
        new Vector3(0,1,0),
        new Vector3(1,1,0),
        new Vector3(0,0,1),
        new Vector3(1,0,1),
        new Vector3(0,1,1),
        new Vector3(1,1,1),
    };

    static public readonly Dictionary<Vector3, int> cube_node_to_id = ReverseArray.BuildReverse(id_to_cube_node);

    static public Vector3 Round(Vector3 input)
    {
        return new Vector3(
            Mathf.Round(input.x),
            Mathf.Round(input.y),
            Mathf.Round(input.z));
    }

    [System.Serializable]
    public enum Dimention
    {
        X, Y, Z
    }

    static public Vector3 GetShift(int id, Dimention dimention)
    {
        switch (dimention)
        {
            case Dimention.X:
                {
                    float x;
                    if (id_to_cube_node[id].x == 1)
                    {
                        x = -1;
                    }
                    else
                    {
                        x = 1;
                    }
                    return new Vector3(x, 0, 0);
                }
            case Dimention.Y:
                {
                    float y;
                    if (id_to_cube_node[id].y == 1)
                    {
                        y = -1;
                    }
                    else
                    {
                        y = 1;
                    }
                    return new Vector3(0, y, 0);
                }
            case Dimention.Z:
                {
                    float z;
                    if (id_to_cube_node[id].z == 1)
                    {
                        z = -1;
                    }
                    else
                    {
                        z = 1;
                    }
                    return new Vector3(0, 0, z);
                }
            default:
                throw new ArgumentException("unknown dimention");
        }
    }
}