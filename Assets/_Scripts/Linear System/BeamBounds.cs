using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeamBounds : MonoBehaviour
{
    private BeamController beamController;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Trova in automatico il BeamController nella scena
        beamController = Object.FindFirstObjectByType<BeamController>();
    }

    void FixedUpdate()
{
    if (beamController == null || beamController.beamObject == null) return;

    // 1. Otteniamo il Transform della trave a cui fare riferimento
    Transform beamTransform = beamController.beamObject.transform;

    // 2. Convertiamo la posizione globale del Rigidbody in coordinate LOCALI rispetto alla trave
    Vector3 localPos = beamTransform.InverseTransformPoint(rb.position);
    
    // 3. Calcoliamo i limiti locali della trave
    float minX = beamController.BeamStartX;
    float maxX = beamController.BeamStartX + beamController.BeamLength;

    // 4. Se l'oggetto cerca di uscire dai bordi locali, lo blocchiamo
    if (localPos.x < minX || localPos.x > maxX)
    {
        // Applichiamo il clamp solo sulla X locale
        localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
        
        // 5. Riconvertiamo la posizione locale corretta in una posizione GLOBALE
        Vector3 worldPosCorrected = beamTransform.TransformPoint(localPos);
        
        // Assegniamo la posizione globale corretta al Rigidbody per evitare jittering
        rb.position = worldPosCorrected; 
        
        // 6. Azzeriamo le velocità relative alla trave per fermare l'inerzia
        // Per farlo in modo preciso in MR, azzeriamo la velocità globale del corpo rigido
        rb.linearVelocity = Vector3.zero; 
        rb.angularVelocity = Vector3.zero;
    }
}
}