using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CaseEditor : MonoBehaviour
{
    int[][] classification = IdManagement.GetGroupClassification();
    [HideInInspector] public int category { get; private set; } = 0;
    [HideInInspector] public int type { get; private set; } = 0;
    CubeTableTester tester;
    [SerializeField] GameObject indicator_example;
    List<int> triangles = new List<int>();
    SplitedIndicator[] splited_edges = new SplitedIndicator[0];
    [SerializeField] UnityEngine.UI.Text category_txt;
    [SerializeField] UnityEngine.UI.Text cube_case_txt;
    bool initiated = false;

    public void ClearTriangles()
    {
        triangles.Clear();
        tester.cube.triangles = new int[0];
        tester.is_triangles_updated = true;
    }

    public void UpdateTriangles(int val)
    {
        triangles.Add(val);
        tester.cube.triangles = new int[(triangles.Count / 3) * 3];
        for (int i = 0; i < tester.cube.triangles.Length; ++i) 
        {
            tester.cube.triangles[i] = triangles[i];
        }
        tester.is_triangles_updated = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        tester = FindObjectOfType<CubeTableTester>();
    }

    void UpdateCase()
    {
        foreach(SplitedIndicator splited in splited_edges) 
        {
            Destroy(splited.gameObject);
        }
        CubeTable.Cube cube_case = CubeTable.main_table.GetCubeCase(tester.cube_id);
        splited_edges = new SplitedIndicator[cube_case.splited_edges.Length];
        for(int i = 0; i < splited_edges.Length; ++i)
        {
            splited_edges[i] = Instantiate(
                    indicator_example, 
                    CubeTable.SplitEdgeHalf(cube_case.splited_edges[i]), 
                    new Quaternion(),
                    tester.transform)
                .GetComponent<SplitedIndicator>();
            splited_edges[i].id = i;
            splited_edges[i].parent_editor = this;
        }
        triangles.Clear();
        foreach(int val in cube_case.triangles) 
        {
            triangles.Add(val);
        }
    }

    public void NextClassification()
    {
        ++category;
        if (category >= classification.Length)
        {
            --category;
        }
        else
        {
            type = 0;
        }
        tester.SetID(classification[category][type]);
        category_txt.text = category.ToString();
        cube_case_txt.text = classification[category][type].ToString();
        UpdateCase();
    }

    public void PrevClassification()
    {
        --category;
        if (category < 0)
        {
            ++category;
        }
        else
        {
            type = 0;
        }
        tester.SetID(classification[category][type]);
        category_txt.text = category.ToString();
        cube_case_txt.text = classification[category][type].ToString();
        UpdateCase();
    }

    public void NextType()
    {
        ++type;
        if (type >= classification[category].Length)
        {
            --type;
        }
        tester.SetID(classification[category][type]);
        cube_case_txt.text = classification[category][type].ToString();
        UpdateCase();
    }

    public void PrevType()
    {
        --type;
        if (type < 0)
        {
            ++type;
        }
        tester.SetID(classification[category][type]);
        cube_case_txt.text = classification[category][type].ToString();
        UpdateCase();
    }

    public void SaveToFile()
    {
        string data_to_save = CubeTable.main_table.PrintTableHardcode();
        File.WriteAllText("table_hardcode.txt", data_to_save);
        data_to_save = CubeTable.main_table.PrintTableJava();
        File.WriteAllText("table_java.txt", data_to_save);
    }

    public void SaveCubeVariations()
    {
        CubeTable.main_table.FillVariations(tester.cube, tester.cube_id);
    }

    // Update is called once per frame
    void Update()
    {
        // keep_empty
    }
    private void LateUpdate()
    {
        if(!initiated)
        {
            initiated = true;

            tester.LockCase();
            tester.SetID(classification[category][type]);
        }
    }
}
