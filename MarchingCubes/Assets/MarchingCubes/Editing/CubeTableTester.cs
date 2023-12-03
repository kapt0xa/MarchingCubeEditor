using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeTableTester : MonoBehaviour
{
    [SerializeField] GameObject indicator_example;

    WeightIndicator[] weight_indicators;
    MeshFilter mesh_filter;
    [SerializeField] MeshFilter back_face;
    [HideInInspector] public int cube_id { get; private set; } = 0;
    [HideInInspector] public CubeTable.Cube cube = new CubeTable.Cube { splited_edges = new int[] { }, triangles = new int[] { } };
    [HideInInspector] public bool is_triangles_updated = true;

    private void Start()
    {
        mesh_filter = GetComponent<MeshFilter>();

        weight_indicators = new WeightIndicator[8];
        for(int i = 0; i < 8; ++i)
        {
            weight_indicators[i] = Instantiate(indicator_example, IdManagement.id_to_cube_node[i], new Quaternion(), transform).GetComponent<WeightIndicator>();
        }
    }

    public void SetID(int id)
    {
        for (int i = 0; i < 8; ++i)
        {
            float weight = ((id & (1 << i)) != 0) ? 1 : -1;
            weight_indicators[i].SetWeight(weight);
        }
        cube_id = id;
        LoadCubeCase();
    }

    void ManageUpdade()
    {
        bool updated = false;
        foreach(WeightIndicator weight in weight_indicators) 
        {
            if(weight.is_updated)
            {
                updated = true;
                weight.is_updated = false;
            }
        }
        if(is_triangles_updated)
        {
            updated = true;
            is_triangles_updated = false;
        }
        if(updated) 
        {
            ManageCubeCase();
            var raw_mesh = cube.GetMesh(GetWeights());

            Mesh mesh = new Mesh();
            Mesh back_face_mesh = new Mesh();

            mesh.vertices = raw_mesh.vertices;
            back_face_mesh.vertices = raw_mesh.vertices;

            int[] reversed_triangles = new int[raw_mesh.triangles.Length];
            for (int i = 0, j = raw_mesh.triangles.Length - 1; i < raw_mesh.triangles.Length; ++i, --j)
            {
                reversed_triangles[i] = raw_mesh.triangles[j];
            }

            mesh.triangles = raw_mesh.triangles;
            back_face_mesh.triangles = reversed_triangles;

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            back_face_mesh.RecalculateBounds();
            back_face_mesh.RecalculateNormals();
            back_face_mesh.RecalculateTangents();

            mesh_filter.mesh = mesh;
            back_face.mesh = back_face_mesh;
        }
    }

    void ManageCubeCase()
    {
        bool another_cube = false;
        foreach(WeightIndicator weight in weight_indicators) 
        {
            if(weight.is_sign_changed)
            {
                another_cube = true;
                weight.is_sign_changed = false;
            }
        }
        if(another_cube) 
        {
            LoadCubeCase();
        }
    }

    void LoadCubeCase()
    {
        float[] weights = GetWeights();
        cube_id = CubeTable.GetCubeId(weights);
        var cube_orig = CubeTable.main_table.GetCubeCase(cube_id);

        cube.triangles = new int[cube_orig.triangles.Length];
        cube_orig.triangles.CopyTo(cube.triangles, 0);

        cube.splited_edges = new int[cube_orig.splited_edges.Length];
        cube_orig.splited_edges.CopyTo(cube.splited_edges, 0);
    }

    public void LockCase()
    {
        foreach (WeightIndicator weight in weight_indicators)
        {
            weight.is_sign_change_alowed = false;
        }
    }

    public void UnlockCase()
    {
        foreach (WeightIndicator weight in weight_indicators)
        {
            weight.is_sign_change_alowed = true;
        }
    }

    float[] GetWeights()
    {
        float[] weights = new float[8];
        for (int i = 0; i < 8; ++i)
        {
            weights[i] = weight_indicators[i].weight;
        }
        return weights;
    }

    private void Update()
    {
        ManageUpdade();
    }
}
