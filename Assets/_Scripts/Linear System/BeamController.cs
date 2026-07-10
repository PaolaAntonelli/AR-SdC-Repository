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
    
    [Tooltip("Scala per il diagramma del Taglio (Shear)")]
    public float shearDiagramScale = 0.05f;   // <--- NUOVO: Scala distinta per il Taglio
    
    [Tooltip("Scala per il diagramma del Momento (Moment)")]
    public float momentDiagramScale = 0.05f;  // <--- NUOVO: Scala distinta per il Momento
    
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
 
            // <--- MODIFICATO: Ora passano i due fattori di scala indipendenti
            RenderDiagram(shearLine, results.shearPoints, shearDiagramScale, Color.cyan);
            RenderDiagram(momentLine, results.momentPoints, momentDiagramScale, Color.magenta);
 
            if (showDeflection) ApplyDeflectionToMesh(results.deflectionPoints);
            else ResetMesh();
        }
    }
 
    void ApplyDeflectionToMesh(float[] deflections)
    {
        Vector3[] displacedVertices = new Vector3[originalVertices.Length];
 
        // localDown identifica la direzione "giù" nel sistema di coordinate della mesh
        Vector3 localDown = beamObject.transform.InverseTransformDirection(Vector3.down);
 
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            float relL = (v[lengthAxis] - meshMinL) / meshSizeL;
            int idx = Mathf.Clamp(Mathf.RoundToInt(relL * (deflections.Length - 1)), 0, deflections.Length - 1);
 
            // CORREZIONE VERSO: Moltiplichiamo per deflectionVisualScale.
            // Se deflections[idx] è negativo (abbassamento), dAmount deve spingere verso localDown.
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
    
    // <--- MODIFICATO: Metodi UI sdoppiati in caso tu debba collegarli a degli Slider dinamici
    public void SetShearDiagramScale(float s) => shearDiagramScale = s;
    public void SetMomentDiagramScale(float s) => momentDiagramScale = s;
 
    public void ResetStructure()
    {
        foreach (GameObject obj in GetAllElements()) Destroy(obj);
        Invoke("SetupInitialScenario", 0.05f);
    }
 
    void SetupInitialScenario()
    {
        UpdateBeamDimensions(); // Aggiorna BeamStartX e BeamLength
        
        // Se il pivot è al centro, BeamStartX sarà negativo (es. -2) 
        // e BeamStartX + BeamLength sarà positivo (es. +2)
        SpawnAtPosition(supportPrefab, BeamStartX, supportYOffset);
        SpawnAtPosition(supportPrefab, BeamStartX + BeamLength, supportYOffset);
        
        // Il centro matematico sarà perfettamente coerente
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
 
    private GameObject SpawnAtPosition(GameObject prefab, float localX, float yOff)
    {
        // Creiamo l'oggetto direttamente come figlio della trave
        GameObject inst = Instantiate(prefab, beamObject.transform);
        
        // Assegniamo la sua posizione LOCALE relativa al pivot centrale della trave
        inst.transform.localPosition = new Vector3(localX, yOff, 0f);
        inst.transform.localRotation = Quaternion.identity;

        if (inst.TryGetComponent(out DraggableLoad drag)) drag.beamController = this;
        return inst;
    }
 
    void UpdateBeamDimensions()
    {
        Renderer r = beamObject.GetComponent<Renderer>();
        if (r == null) return;
        
        // Usiamo lo scale locale o la dimensione della mesh locale anziché i bounds globali
        MeshFilter mf = beamObject.GetComponent<MeshFilter>();
        if (mf != null)
        {
            // La lunghezza locale della mesh moltiplicata per la scala X dell'oggetto
            BeamLength = mf.sharedMesh.bounds.size.x * beamObject.transform.localScale.x;
            // L'inizio locale (di solito -metà lunghezza se la mesh è centrata)
            BeamStartX = mf.sharedMesh.bounds.min.x * beamObject.transform.localScale.x;
        }
    }
 
    GameObject[] GetAllElements()
    {
        var l = new List<GameObject>(GameObject.FindGameObjectsWithTag("Support"));
        l.AddRange(GameObject.FindGameObjectsWithTag("Load"));
        return l.ToArray();
    }
 
    // Modifica anche GetRelativePositions per calcolare la X locale rispetto alla trave
    List<float> GetRelativePositions(string tag)
    {
        var p = new List<float>();
        foreach (var o in GameObject.FindGameObjectsWithTag(tag))
        {
            // Trasformiamo la posizione globale dell'oggetto in coordinate locali rispetto alla trave
            Vector3 localPos = beamObject.transform.InverseTransformPoint(o.transform.position);
            p.Add(Mathf.Clamp(localPos.x - BeamStartX, 0, BeamLength));
        }
        return p;
    }
 
    // genera mesh dei diagrammi e colora i bordi
    void RenderDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null || values == null || values.Length == 0) return;

        line.positionCount = values.Length;
        Vector3[] localBorderPoints = new Vector3[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            // Calcolo della X locale sulla trave (va da BeamStartX a BeamStartX + BeamLength)
            float localX = BeamStartX + ((float)i / (values.Length - 1)) * BeamLength;
            
            // Posizione base locale sulla trave + l'offset del diagramma
            Vector3 localBasePos = new Vector3(localX, 0, 0) + diagramOffset;
            Vector3 localDiagramPos = localBasePos + new Vector3(0, values[i] * scale, 0);

            // Convertiamo in posizione globale SOLO per il LineRenderer (che ragiona in world space)
            line.SetPosition(i, beamObject.transform.TransformPoint(localDiagramPos));
            
            // Salviamo la coordinata LOCALE per la generation della mesh interna
            localBorderPoints[i] = localDiagramPos;
        }

        GenerateDiagramMesh(line.gameObject, localBorderPoints, values.Length);
    }

    void GenerateDiagramMesh(GameObject container, Vector3[] localBorderPoints, int resolution)
    {
        MeshFilter meshFilter = container.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = container.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = container.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = container.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        
        Vector3[] vertices = new Vector3[resolution * 2];
        Vector2[] uvs = new Vector2[resolution * 2];
        int[] triangles = new int[(resolution - 1) * 6];

        int vertIndex = 0;
        int triIndex = 0;

        for (int i = 0; i < resolution; i++)
        {
            float localX = BeamStartX + ((float)i / (resolution - 1)) * BeamLength;
            Vector3 localBasePoint = new Vector3(localX, 0, 0) + diagramOffset;
            Vector3 localDiagramPoint = localBorderPoints[i];

            // Trasformiamo i punti nello spazio locale del CONTAINER del diagramma
            vertices[vertIndex] = container.transform.InverseTransformPoint(beamObject.transform.TransformPoint(localBasePoint));          
            vertices[vertIndex + 1] = container.transform.InverseTransformPoint(beamObject.transform.TransformPoint(localDiagramPoint)); 

            float normalizedX = (float)i / (resolution - 1);
            uvs[vertIndex] = new Vector2(normalizedX, 0f);
            uvs[vertIndex + 1] = new Vector2(normalizedX, 1f);

            if (i < resolution - 1)
            {
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
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }
}
