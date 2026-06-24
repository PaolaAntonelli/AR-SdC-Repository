using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
 
public class BeamController : MonoBehaviour
{
    [Header("Impostazioni Solutore")]
    public SolverType currentSolver = SolverType.Analytic;
 
    public GameObject beamObject;
    public GameObject supportPrefab;
    public GameObject loadPrefab;
    public LineRenderer shearLine;
    public LineRenderer momentLine;
 
    [Header("Parametri Grafici")]
    public float forceMagnitude = 15f;
    public float currentDiagramScale = 0.05f;
    // Se la deformazione va verso l'alto, cambia questo valore in negativo (es. -100)
    public float deflectionVisualScale = 100f;
    public Vector3 diagramOffset = new Vector3(0, -2f, 0);
    public float minDistanceBetweenObjects = 0.5f;
 
    private bool showDeflection = false;
    private Vector3[] originalVertices;
    private Mesh deformingMesh;
    private MeshFilter meshFilter;
 
    private int lengthAxis = 0;
    private float meshMinL, meshSizeL;
 
    public float BeamStartX { get; private set; }
    public float BeamLength { get; private set; }

    [Header("Posizionamento Verticale (Offset)")]
    public float loadYOffset = 0.2f;    // Regola questo valore nell'Inspector per i carichi
    public float supportYOffset = -0.2f; // Regola questo valore nell'Inspector per i supporti
 
    // Questa funzione può essere collegata a un UI Toggle o Button
    public void SetSolverType(bool isAnalytic)
    {
        currentSolver = isAnalytic ? SolverType.Analytic : SolverType.FEM;
    }
    void Start()
    {
        if (beamObject != null)
        {
            meshFilter = beamObject.GetComponent<MeshFilter>();
            Mesh sourceMesh = meshFilter.sharedMesh;
 
            deformingMesh = new Mesh();
            deformingMesh.name = "DynamicBeamMesh";
            deformingMesh.vertices = sourceMesh.vertices;
            deformingMesh.triangles = sourceMesh.triangles;
            deformingMesh.uv = sourceMesh.uv;
            deformingMesh.normals = sourceMesh.normals;
            deformingMesh.MarkDynamic();
 
            originalVertices = sourceMesh.vertices;
            meshFilter.mesh = deformingMesh;
 
            DetectMeshAxes(sourceMesh);
            UpdateBeamDimensions();
            SetupInitialScenario();
        }
    }
 
    void DetectMeshAxes(Mesh m)
    {
        Bounds b = m.bounds;
        Vector3 sizes = b.size;
 
        if (sizes.x >= sizes.y && sizes.x >= sizes.z) lengthAxis = 0;
        else if (sizes.y >= sizes.x && sizes.y >= sizes.z) lengthAxis = 1;
        else lengthAxis = 2;
 
        meshMinL = b.min[lengthAxis];
        meshSizeL = b.size[lengthAxis];
    }
 
    void Update()
    {
        if (beamObject == null) return;
        UpdateBeamDimensions();
 
        List<float> sPos = GetRelativePositions("Support");
        List<float> lPos = GetRelativePositions("Load");
        List<float> lMag = new List<float>();
        foreach (var l in lPos) lMag.Add(forceMagnitude);
 
        if (sPos.Count >= 1)
        {
            BeamData results;
 
            // SWITCH TRA I DUE METODI
            if (currentSolver == SolverType.Analytic)
            {
                results = BeamMath.CalculateAnalytic(BeamLength, lPos, lMag, sPos, 100);
            }
            else
            {
                results = BeamMath.CalculateFEM(BeamLength, lPos, lMag, sPos, 100);
            }
 
            RenderDiagram(shearLine, results.shearPoints, currentDiagramScale, Color.cyan);
            RenderDiagram(momentLine, results.momentPoints, currentDiagramScale, Color.magenta);
 
            if (showDeflection) ApplyDeflectionToMesh(results.deflectionPoints);
            else ResetMesh();
        }
    }
 
    void ApplyDeflectionToMesh(float[] deflections)
    {
        Vector3[] displacedVertices = new Vector3[originalVertices.Length];
 
        // localDown identifica la direzione "gių" nel sistema di coordinate della mesh
        Vector3 localDown = beamObject.transform.InverseTransformDirection(Vector3.down);
 
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            float relL = (v[lengthAxis] - meshMinL) / meshSizeL;
            int idx = Mathf.Clamp(Mathf.RoundToInt(relL * (deflections.Length - 1)), 0, deflections.Length - 1);
 
            // CORREZIONE VERSO: Moltiplichiamo per deflectionVisualScale.
            // Se deflections[idx] č negativo (abbassamento), dAmount deve spingere verso localDown.
            float dAmount = deflections[idx] * deflectionVisualScale;
 
            // Sommiamo lo spostamento alla posizione originale del vertice
            displacedVertices[i] = v + (localDown * dAmount);
        }
 
        deformingMesh.vertices = displacedVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();
    }
 
    void ResetMesh()
    {
        if (deformingMesh != null && deformingMesh.vertices.Length > 0 && deformingMesh.vertices != originalVertices)
        {
            deformingMesh.vertices = originalVertices;
            deformingMesh.RecalculateNormals();
            deformingMesh.RecalculateBounds();
        }
    }
 
    // --- LOGICA SPAWN E UI ---
 
    public void ToggleDeflection() => showDeflection = !showDeflection;
    public void SetDiagramScale(float s) => currentDiagramScale = s;
 
    public void ResetStructure()
    {
        foreach (GameObject obj in GetAllElements()) Destroy(obj);
        Invoke("SetupInitialScenario", 0.05f);
    }
 
    void SetupInitialScenario()
    {
        UpdateBeamDimensions();
        // Sostituito -0.6f con supportYOffset
        SpawnAtPosition(supportPrefab, BeamStartX, supportYOffset);
        SpawnAtPosition(supportPrefab, BeamStartX + BeamLength, supportYOffset);
        // Sostituito 0.6f con loadYOffset
        SpawnAtPosition(loadPrefab, BeamStartX + (BeamLength / 2f), loadYOffset);
    }
 
    // Sostituito -0.6f e 0.6f con le nuove variabili
    public void AddSupport() => SpawnAtPosition(supportPrefab, GetValidSpawnX(), supportYOffset);
    public void AddLoad() => SpawnAtPosition(loadPrefab, GetValidSpawnX(), loadYOffset);
 
    float GetValidSpawnX()
    {
        float center = BeamStartX + (BeamLength / 2f);
        for (int i = 0; i < 20; i++)
        {
            float offset = (i / 2 + 1) * minDistanceBetweenObjects * (i % 2 == 0 ? 1 : -1);
            float testX = (i == 0) ? center : center + offset;
            testX = Mathf.Clamp(testX, BeamStartX, BeamStartX + BeamLength);
            bool occupied = false;
            foreach (var obj in GetAllElements())
                if (obj != null && Mathf.Abs(obj.transform.position.x - testX) < minDistanceBetweenObjects * 0.8f) occupied = true;
            if (!occupied) return testX;
        }
        return center;
    }
 
    private GameObject SpawnAtPosition(GameObject prefab, float worldX, float yOff)
    {
        Vector3 pos = new Vector3(worldX, beamObject.transform.position.y + yOff, beamObject.transform.position.z);
        GameObject inst = Instantiate(prefab, pos, Quaternion.identity);
        if (inst.TryGetComponent(out DraggableLoad drag)) drag.beamController = this;
        return inst;
    }
 
    void UpdateBeamDimensions()
    {
        Renderer r = beamObject.GetComponent<Renderer>();
        if (r == null) return;
        BeamLength = r.bounds.size.x;
        BeamStartX = r.bounds.min.x;
    }
 
    GameObject[] GetAllElements()
    {
        var l = new List<GameObject>(GameObject.FindGameObjectsWithTag("Support"));
        l.AddRange(GameObject.FindGameObjectsWithTag("Load"));
        return l.ToArray();
    }
 
    List<float> GetRelativePositions(string tag)
    {
        var p = new List<float>();
        foreach (var o in GameObject.FindGameObjectsWithTag(tag))
            p.Add(Mathf.Clamp(o.transform.position.x - BeamStartX, 0, BeamLength));
        return p;
    }
 
 // genera mesh dei diagrammi e colora i bordi
 void RenderDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null || values == null || values.Length == 0) return;

        line.positionCount = values.Length;
        Vector3[] borderPoints = new Vector3[values.Length];

        // 1. Calcolo e disegno della linea di confine globale (LineRenderer)
        for (int i = 0; i < values.Length; i++)
        {
            float x = BeamStartX + ((float)i / (values.Length - 1)) * BeamLength;
            Vector3 basePos = new Vector3(x, beamObject.transform.position.y, beamObject.transform.position.z) + diagramOffset;
            Vector3 diagramPos = basePos + new Vector3(0, values[i] * scale, 0);

            line.SetPosition(i, diagramPos);
            borderPoints[i] = diagramPos; // Salva la coordinata globale
        }

        // 2. Generazione del poligono di riempimento in coordinate locali
        GenerateDiagramMesh(line.gameObject, borderPoints, values.Length);
    }

    void GenerateDiagramMesh(GameObject container, Vector3[] borderPoints, int resolution)
    {
        MeshFilter meshFilter = container.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = container.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = container.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = container.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        
        // 2 vertici (base e diagramma) per ogni punto di campionamento
        Vector3[] vertices = new Vector3[resolution * 2];
        
        // Avendo impostato "Render Face: Both" nel materiale, bastano 6 indici (2 triangoli) per segmento
        int[] triangles = new int[(resolution - 1) * 6];

        int vertIndex = 0;
        int triIndex = 0;

        for (int i = 0; i < resolution; i++)
        {
            float x = BeamStartX + ((float)i / (resolution - 1)) * BeamLength;
            Vector3 globalBasePoint = new Vector3(x, beamObject.transform.position.y, beamObject.transform.position.z) + diagramOffset;
            Vector3 globalDiagramPoint = borderPoints[i];

            // Mantiene la correzione della scala convertendo da World Space a Local Space del GameObject
            vertices[vertIndex] = container.transform.InverseTransformPoint(globalBasePoint);          // Vertice inferiore (Trave)
            vertices[vertIndex + 1] = container.transform.InverseTransformPoint(globalDiagramPoint); // Vertice superiore (Diagramma)

            if (i < resolution - 1)
            {
                // Generazione di una sola faccia (l'Inspector penserà a renderizzarla double-sided)
                triangles[triIndex++] = vertIndex;
                triangles[triIndex++] = vertIndex + 1;
                triangles[triIndex++] = vertIndex + 2;

                triangles[triIndex++] = vertIndex + 1;
                triangles[triIndex++] = vertIndex + 3;
                triangles[triIndex++] = vertIndex + 2;
            }

            vertIndex += 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }
}
 