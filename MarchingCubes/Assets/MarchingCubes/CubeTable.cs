using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using static CubeTable;

//
// the table is got from here: https://gist.github.com/dwilliamson/c041e3454a713e58baf6e4f8e5fffecd
// by https://gist.github.com/dwilliamson
//
// Lookup Tables for Marching Cubes
//
// These tables differ from the original paper (Marching Cubes: A High Resolution 3D Surface Construction Algorithm)
//
// The co-ordinate system has the more convenient properties:
//
//    i = cube index [0, 7]
//    x = (i & 1) >> 0
//    y = (i & 2) >> 1
//    z = (i & 4) >> 2
//
// Axes are:
//
//      y
//      |     z
//      |   /
//      | /
//      +----- x
//
// Vertex and edge layout:
//
//            6             7
//            +-------------+               +-----6-------+   
//          / |           / |             / |            /|   
//        /   |         /   |          11   7         10   5
//    2 +-----+-------+  3  |         +-----+2------+     |   
//      |   4 +-------+-----+ 5       |     +-----4-+-----+   
//      |   /         |   /           3   8         1   9
//      | /           | /             | /           | /       
//    0 +-------------+ 1             +------0------+         
//
// Triangulation cases are generated prioritising rotations over inversions, which can introduce non-manifold geometry.
//

public struct RawMesh
{
    public Vector3[] vertices;
    public int[] triangles;
}

public class CubeTable
{
    struct Edge
    {
        static readonly Vector3[] nodes = new Vector3[8]
        {
        new Vector3{ x = 0, y = 0, z = 0}, // 0
        new Vector3{ x = 1, y = 0, z = 0}, // 1
        new Vector3{ x = 0, y = 1, z = 0}, // 2
        new Vector3{ x = 1, y = 1, z = 0}, // 3
        new Vector3{ x = 0, y = 0, z = 1}, // 4
        new Vector3{ x = 1, y = 0, z = 1}, // 5
        new Vector3{ x = 0, y = 1, z = 1}, // 6
        new Vector3{ x = 1, y = 1, z = 1}, // 7
        };

        static readonly Dictionary<Vector3, int> node_to_id = ReverseArray.BuildReverse(nodes);

        public Vector3 from;
        public int from_id;
        public Vector3 shift;
        public int to_id;

        public Edge(int from_val, int to_val)
        {
            from = nodes[from_val];
            from_id = from_val;
            shift = nodes[to_val] - from;
            to_id = to_val;
            Debug.Assert(
                (to_id - from_id == 1) ||
                (to_id - from_id == 2) ||
                (to_id - from_id == 4) );
        }

        public Vector3 SplitEdge(float[] weights)
        {
            float weight_from = weights[from_id];
            float weight_to = weights[to_id];
            return from + shift * (weight_from / (weight_from - weight_to));
        }

        public Vector3 SplitEdge(float ratio)
        {
            return from + shift * ratio;
        }

        public override int GetHashCode()
        {
            return new Tuple<int, int>(from_id, to_id).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return
                ((Edge)obj).from_id == from_id &&
                ((Edge)obj).to_id == to_id;
        }
    };

    static readonly Edge[] edges = new Edge[12]
    {
        new Edge(0, 1), // 00
        new Edge(1, 3), // 01
        new Edge(2, 3), // 02
        new Edge(0, 2), // 03

        new Edge(4, 5), // 04
        new Edge(5, 7), // 05
        new Edge(6, 7), // 06
        new Edge(4, 6), // 07

        new Edge(0, 4), // 08
        new Edge(1, 5), // 09
        new Edge(3, 7), // 10
        new Edge(2, 6), // 11
    };

    static readonly Dictionary<Edge, int> edges_to_id = ReverseArray.BuildReverse(edges);

    static int RotateEdge(int edge_it, int rot_id)
    {
        int one_id = IdManagement.RotateNode(edges[edge_it].from_id, rot_id);
        int other_id = IdManagement.RotateNode(edges[edge_it].to_id, rot_id);
        if (one_id > other_id)
        {
            (one_id, other_id) = (other_id, one_id);
        }
        return edges_to_id[new Edge(one_id, other_id)];
    }

    static int MirrorEdge(int edge_it)
    {
        int one_id = IdManagement.MirrorNode(edges[edge_it].from_id);
        int other_id = IdManagement.MirrorNode(edges[edge_it].to_id);

        Debug.Assert(one_id > other_id);

        return edges_to_id[new Edge(other_id, one_id)];
    }

    public struct Cube
    {
        public int[] splited_edges;
        public int[] triangles;

        public RawMesh GetMesh(float[] weights, float scale = 1)
        {
            Vector3[] vertices = new Vector3[splited_edges.Length];
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i] = scale * edges[splited_edges[i]].SplitEdge(weights);
            }

            return new RawMesh { vertices = vertices, triangles = triangles };
        }
    }

    static Cube RotateCube(Cube cube, int rot_id)
    {
        int[] splited_edges = new int[cube.splited_edges.Length];
        int[] triangles = new int[cube.triangles.Length];
        cube.triangles.CopyTo(triangles, 0);
        for (int i = 0; i < splited_edges.Length; ++i)
        {
            splited_edges[i] = RotateEdge(cube.splited_edges[i], rot_id);
        }
        return new Cube { splited_edges = splited_edges, triangles = triangles };
    }
    static Cube MirrorCube(Cube cube)
    {
        int[] splited_edges = new int[cube.splited_edges.Length];
        int[] triangles = new int[cube.triangles.Length];
        for(int i = 0, j = splited_edges.Length - 1; i < splited_edges.Length; ++i, --j)
        {
            triangles[i] = cube.triangles[j];
        }
        for (int i = 0; i < splited_edges.Length; ++i)
        {
            splited_edges[i] = MirrorEdge(cube.splited_edges[i]);
        }
        return cube;
    }

    public void FillVariations(Cube example, int id_hint)
    {
        HashSet<int> indices = new HashSet<int>();

        for(int rot_id = 0; rot_id < 24; ++rot_id)
        {
            int cube_id = IdManagement.RotateCubeId(id_hint, rot_id);
            if(indices.Contains(cube_id))
            {
                continue;
            }
            indices.Add(cube_id);
            table[cube_id] = RotateCube(example, rot_id);
        }

        int mirrored = IdManagement.MirrorCubeId(id_hint);
        if(indices.Contains(mirrored))
        {
            return;
        }

        for (int rot_id = 0; rot_id < 24; ++rot_id)
        {
            int cube_id = IdManagement.RotateCubeId(mirrored, rot_id);
            if (indices.Contains(cube_id))
            {
                continue;
            }
            indices.Add(cube_id);
            table[cube_id] = RotateCube(example, rot_id);
        }
    }

    Cube[] table = new Cube[256]
    {
        new Cube{
            splited_edges = new int[]{ },
            triangles = new int[]{ } },
        new Cube{
            splited_edges = new int[]{  0,  3,  8, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  0,  9,  1, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  3,  8,  1,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  3, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  0, 11,  2, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  3,  2, 11,  1,  0,  9, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 11,  1,  2,  9,  8, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  1, 10,  2, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  8,  2,  1, 10, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  9,  0, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  2,  3, 10,  9, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{ 11,  3, 10,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{ 10,  0,  1,  8, 11, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  9,  3,  0, 11, 10, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  9, 11, 10, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  7, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  7,  4,  3,  0, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  7,  0,  9,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  1,  4,  9,  7,  3, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  7,  4, 11,  3,  2, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  4, 11,  7,  2,  0, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  0,  9,  1,  8,  7,  4, 11,  3,  2, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  7,  4, 11,  2,  9,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  7,  2,  1, 10, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  7,  4,  3,  0, 10,  2,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  9,  0,  7,  4,  8, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  3,  4,  7,  9, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  1, 10,  3, 11,  4,  8,  7, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{ 10, 11,  1,  7,  4,  0, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  7,  4,  8,  9,  3,  0, 11, 10, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  7,  4, 11,  9, 10, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  9,  4,  5, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  9,  4,  5,  8,  0,  3, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  4,  5,  0,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  5,  8,  4,  3,  1, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  9,  4,  5, 11,  3,  2, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  0,  8,  5,  9,  4, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  4,  5,  0,  1, 11,  3,  2, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  5,  1,  4,  2, 11,  8, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  1, 10,  2,  5,  9,  4, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  9,  4,  5,  0,  3,  8,  2,  1, 10, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  2,  5, 10,  4,  0, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  5,  4,  3,  8, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 11,  3, 10,  1,  4,  5,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  4,  5,  9, 10,  0,  1,  8, 11, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 11,  3,  0,  5,  4, 10, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  4,  5,  8, 10, 11, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  7,  9,  5, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  3,  9,  0,  5,  7, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  7,  0,  8,  1,  5, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  7,  5,  3,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  5,  9,  7,  8,  2, 11,  3, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  7,  9,  5,  0, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  3,  7,  0,  8,  1,  5, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  1,  7,  5, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  7,  9,  5,  2,  1, 10, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  1,  3,  9,  0,  5,  7, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  7,  5,  8, 10,  2,  0, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  5,  3,  7, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  7,  5,  9, 11,  3, 10,  1, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  4,  5,  6,  5,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  5, 11,  7, 10,  1,  9,  0, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 11,  5, 10,  7,  8,  3,  0, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  5, 11,  7, 10, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  6,  7, 11, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  6,  3,  8,  0, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  6,  7, 11,  0,  9,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  9,  1,  8,  3,  6,  7, 11, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  3,  2,  7,  6, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  0,  7,  8,  6,  2, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  6,  7,  2,  3,  9,  1,  0, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  6,  7,  8,  1,  9,  2, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  7, 10,  2,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  3,  8,  0, 11,  6,  7, 10,  2,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  0,  9,  2, 10,  7, 11,  6, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  6,  7, 11,  8,  2,  3, 10,  9, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  7, 10,  6,  1,  3, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  0,  7,  6,  1, 10, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  7,  3,  6,  0,  9, 10, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  6,  7, 10,  8,  9, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  8,  4, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  6,  3, 11,  0,  4, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  8,  4,  1,  0,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  1,  3,  9, 11,  6,  4, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  2,  8,  3,  4,  6, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  4,  0,  6,  2, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  9,  1,  0,  2,  8,  3,  4,  6, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  9,  1,  4,  2,  6, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  6, 11,  1, 10,  2, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  1, 10,  2,  6,  3, 11,  0,  4, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  4,  8, 10,  2,  9,  0, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  4,  5,  6,  5,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 10,  4,  9,  6, 11,  2,  3, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  3, 10,  1,  6, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  1, 10,  0,  6,  4, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  4, 10,  6,  9,  0,  8,  3, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  4, 10,  6,  9, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  6,  7, 11,  4,  5,  9, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  4,  5,  9,  7, 11,  6,  3,  8,  0, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  1,  0,  5,  4, 11,  6,  7, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  7,  5,  8,  4,  3,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  3,  2,  7,  6,  9,  4,  5, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  5,  9,  4,  0,  7,  8,  6,  2, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  3,  2,  6,  7,  1,  0,  5,  4, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  4,  5,  6,  5,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  6,  1,  2,  5,  4,  7,  8, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 10,  2,  1,  6,  7, 11,  4,  5,  9, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  8,  4,  5,  9, 11,  6,  7, 10,  2,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  6,  2,  5, 10,  4,  0, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  8,  4,  7,  5, 10,  6,  3, 11,  2, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{  9,  4,  5,  7, 10,  6,  1,  3, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 10,  6,  5,  7,  8,  4,  1,  9,  0, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{  4,  3,  0,  7,  6,  5, 10, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 10,  6,  5,  8,  4,  7, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  9,  6,  5, 11,  8, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  3,  0,  5,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  5,  0,  1,  8, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{ 11,  6,  3,  5,  1, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  9,  8,  5,  3,  2,  6, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  5,  9,  6,  0,  2, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  1,  6,  5,  2,  3,  0,  8, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  1,  6,  5,  2, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  2,  1, 10,  9,  6,  5, 11,  8, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  9,  0,  1,  3, 11,  2,  5, 10,  6, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{ 11,  0,  8,  2, 10,  6,  5, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  3, 11,  2,  5, 10,  6, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  1,  8,  3,  9,  5, 10,  6, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  6,  5, 10,  0,  1,  9, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  8,  3,  0,  5, 10,  6, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  6,  5, 10, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 10,  5,  6, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  8,  6, 10,  5, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 10,  5,  6,  9,  1,  0, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  3,  8,  1,  9,  6, 10,  5, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  3,  6, 10,  5, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  8,  0, 11,  2,  5,  6, 10, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  1,  0,  9,  2, 11,  3,  6, 10,  5, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  5,  6, 10, 11,  1,  2,  9,  8, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  5,  6,  1,  2, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  5,  6,  1,  2,  8,  0,  3, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  6,  9,  5,  0,  2, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  6,  2,  5,  3,  8,  9, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  3,  6, 11,  5,  1, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  0,  1,  6,  5, 11, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{ 11,  3,  6,  5,  0,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  5,  6,  9, 11,  8, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  5,  6, 10,  7,  4,  8, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  4,  7, 10,  5,  6, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  5,  6, 10,  4,  8,  7,  0,  9,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{  6, 10,  5,  1,  4,  9,  7,  3, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  7,  4,  8,  6, 10,  5,  2, 11,  3, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8, } },
        new Cube{
            splited_edges = new int[]{ 10,  5,  6,  4, 11,  7,  2,  0, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  7,  6, 10,  5,  3,  2, 11,  1,  0,  9, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, } },
        new Cube{
            splited_edges = new int[]{ 11,  7,  6,  4,  9,  5,  2, 10,  1, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{  2,  1,  6,  5,  8,  7,  4, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  7,  4,  2,  1,  6,  5, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  4,  5,  6,  5,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  8,  7,  4,  6,  9,  5,  0,  2, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  7,  2,  3,  6,  5,  4,  9, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  7,  3,  6, 11,  5,  1, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  5,  0,  1,  4,  7,  6, 11, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  9,  5,  4,  6, 11,  7,  0,  8,  3, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{ 11,  7,  6,  9,  5,  4, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  6, 10,  4,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  6, 10,  4,  9,  3,  8,  0, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  0, 10,  1,  6,  4, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  6, 10,  1,  8,  3,  4, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  9,  4, 10,  6,  3,  2, 11, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  2, 11,  8,  0,  6, 10,  4,  9, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  4,  5,  6,  5,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 11,  3,  2,  0, 10,  1,  6,  4, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  6,  8,  4, 11,  2, 10,  1, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  4,  1,  9,  2,  6, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  3,  8,  0,  4,  1,  9,  2,  6, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  6,  2,  4,  0, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  3,  8,  2,  4,  6, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  4,  6,  9, 11,  3,  1, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  8,  6, 11,  4,  9,  0,  1, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 11,  3,  6,  0,  4, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  6, 11,  4, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{ 10,  7,  6,  8,  9, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  3,  7,  0,  6, 10,  9, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  6, 10,  7,  8,  1,  0, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  6, 10,  7,  1,  3, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  3,  2, 11, 10,  7,  6,  8,  9, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  2,  9,  0, 10,  6, 11,  7, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  0,  8,  3,  7,  6, 11,  1,  2, 10, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{  1,  2, 10,  7,  6, 11, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  2,  1,  9,  7,  8,  6, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  2,  7,  6,  3,  0,  1,  9, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  7,  0,  6,  2, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  7,  2,  3,  6, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  8,  1,  9,  3, 11,  7,  6, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 11,  7,  6,  1,  9,  0, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  6, 11,  7,  0,  8,  3, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{ 11,  7,  6, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  5, 10, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{ 10,  5, 11,  7,  0,  3,  8, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  5, 10,  0,  9,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  4,  5,  6, } },
        new Cube{
            splited_edges = new int[]{  7, 11, 10,  5,  3,  8,  1,  9, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  4,  5,  6,  5,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  5,  2, 10,  3,  7, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  5,  7, 10,  8,  0,  2, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  0,  9,  1,  5,  2, 10,  3,  7, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  9,  7,  8,  5, 10,  1,  2, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  1, 11,  2,  7,  5, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  0,  3,  1, 11,  2,  7,  5, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  2,  9,  0,  5, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  7,  9,  5,  8,  3, 11,  2, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  3,  1,  7,  5, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  8,  0,  7,  1,  5, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  0,  9,  3,  5,  7, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  9,  7,  8,  5, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  8,  5,  4, 10, 11, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  0,  3, 11,  5, 10,  4, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  1,  0,  9,  8,  5,  4, 10, 11, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 10,  3, 11,  1,  9,  5,  4, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  3,  2,  8,  4, 10,  5, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 10,  5,  2,  4,  0, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  3,  0,  2, 10,  1,  4,  9,  5, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{  2, 10,  1,  4,  9,  5, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  8, 11,  4,  2,  1,  5, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  0,  5,  4,  1,  2,  3, 11, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  0, 11,  2,  8,  4,  9,  5, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  5,  4,  9,  2,  3, 11, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  4,  8,  5,  3,  1, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  0,  5,  4,  1, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  5,  4,  9,  3,  0,  8, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  5,  4,  9, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 11,  4,  7,  9, 10, },
            triangles = new int[]{  0,  1,  2,  0,  3,  1,  0,  4,  3, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  8, 11,  4,  7,  9, 10, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5,  3,  6,  4,  3,  7,  6, } },
        new Cube{
            splited_edges = new int[]{ 11, 10,  7,  1,  0,  4, },
            triangles = new int[]{  0,  1,  2,  1,  3,  4,  2,  1,  4,  2,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  3, 10,  1, 11,  7,  8,  4, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  3,  2, 10,  4,  9,  7, },
            triangles = new int[]{  0,  1,  2,  0,  2,  3,  2,  4,  3,  5,  0,  3, } },
        new Cube{
            splited_edges = new int[]{  9,  2, 10,  0,  8,  4,  7, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  3,  4,  7,  0,  1,  2, 10, },
            triangles = new int[]{  6,  4,  3,  6,  3,  1,  6,  0,  5,  6,  2,  0,  6,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  7,  8,  4, 10,  1,  2, },
            triangles = new int[]{  0,  1,  2,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  4,  9,  2,  1, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3,  3,  1,  4,  3,  4,  5, } },
        new Cube{
            splited_edges = new int[]{ 11,  2,  3,  1,  9,  0,  7,  8,  4, },
            triangles = new int[]{  3,  4,  1,  4,  8,  1,  1,  8,  0,  8,  6,  0,  2,  7,  5, } },
        new Cube{
            splited_edges = new int[]{  7, 11,  4,  2,  0, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  2,  3, 11,  4,  7,  8, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  9,  4,  1,  7,  3, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  7,  8,  4,  1,  9,  0, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  3,  4,  7,  0, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  7,  8,  4, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ 11, 10,  8,  9, },
            triangles = new int[]{  0,  1,  2,  2,  1,  3, } },
        new Cube{
            splited_edges = new int[]{  0,  3,  9, 11, 10, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  1,  0, 10,  8, 11, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{ 10,  3, 11,  1, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  3,  2,  8, 10,  9, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{  9,  2, 10,  0, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{ 10,  1,  2,  8,  3,  0, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{  2, 10,  1, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  2,  1, 11,  9,  8, },
            triangles = new int[]{  0,  1,  2,  1,  3,  2,  3,  4,  2, } },
        new Cube{
            splited_edges = new int[]{ 11,  2,  3,  9,  0,  1, },
            triangles = new int[]{  2,  0,  4,  0,  3,  4,  0,  1,  5,  5,  3,  0, } },
        new Cube{
            splited_edges = new int[]{ 11,  0,  8,  2, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  3, 11,  2, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  1,  8,  3,  9, },
            triangles = new int[]{  0,  1,  2,  3,  1,  0, } },
        new Cube{
            splited_edges = new int[]{  1,  9,  0, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{  8,  3,  0, },
            triangles = new int[]{  0,  1,  2, } },
        new Cube{
            splited_edges = new int[]{ },
            triangles = new int[]{ } },
};

    public RawMesh GetMesh(float[] weights, float scale = 1)
    {
        return table[GetCubeId(weights)].GetMesh(weights, scale);
    }

    static public bool IsPositive(float weight)
    {
        return weight > 0;
    }

    public Cube GetCubeCase(float[] weights)
    {
        return table[GetCubeId(weights)];
    }

    public Cube GetCubeCase(int id)
    {
        return table[id];
    }

    static public int GetCubeId(float[] weights)
    {
        int cube_id = 0;
        for (int i = 0; i < 8; i++)
        {
            if (IsPositive(weights[i]))
            {
                cube_id |= 1 << i;
            }
        }
        return cube_id;
    }

    public static CubeTable main_table = new CubeTable();

    static public Vector3 SplitEdgeHalf(int edge_id)
    {
        return edges[edge_id].SplitEdge(0.5f);
    }

    public string PrintTableHardcode()
    {
        string result = "";
        result += $"    Cube[] table = new Cube[{table.Length}]\n    {{\n";
        foreach (Cube cube in table)
        {
            result += "        new Cube{\r\n            splited_edges = new int[]{ ";
            foreach (int num in cube.splited_edges)
            {
                result += $"{num,2:0}, ";
            }
            result += "},\n            triangles = new int[]{ ";
            foreach (int id in cube.triangles)
            {
                result += $"{id,2:0}, ";
            }
            result += "} },\n";
        }
        result += "};";

        return result;
    }

    public string PrintTableJava()
    {

        string result = "";
        result += $"const TriangleTable = [\n";
        foreach (Cube cube in table)
        {
            result += "\t[";
            foreach (int id in cube.triangles)
            {
                result += $"{cube.splited_edges[id]}, ";
            }
            result += "-1 ],\n";
        }
        result += "];";

        return result;
    }
}
