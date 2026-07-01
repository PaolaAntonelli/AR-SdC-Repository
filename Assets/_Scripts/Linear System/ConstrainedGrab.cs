using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit;

 

// Ereditiamo da XRGrabInteractable per mantenerne tutte le funzionalit�
public class ConstrainedGrab : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    private BeamController beamController;
    private Transform beamTransform;
    private float localY;
    private float localZ;

    protected override void Awake()
    {
        base.Awake();
        beamController = Object.FindFirstObjectByType<BeamController>();
        if (beamController != null && beamController.beamObject != null)
        {
            beamTransform = beamController.beamObject.transform;
            // Salviamo le posizioni locali relative alla trave
            Vector3 localStartPos = beamTransform.InverseTransformPoint(transform.position);
            localY = localStartPos.y;
            localZ = localStartPos.z;
        }
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);

        if (phase == XRInteractionUpdateOrder.UpdatePhase.Fixed || phase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        {
            if (beamController == null || beamTransform == null) return;

            // 1. Convertiamo la posizione corrente (mossa dalla mano) in coordinate locali della trave
            Vector3 localPos = beamTransform.InverseTransformPoint(transform.position);

            // 2. Applichiamo il Clamp sui limiti locali
            float minX = beamController.BeamStartX;
            float maxX = beamController.BeamStartX + beamController.BeamLength;
            localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
            
            // Forziamo Y e Z locali per non far uscire l'oggetto dalla trave
            localPos.y = localY;
            localPos.z = localZ;

            // 3. Riconvertiamo in posizione globale e applichiamo
            transform.position = beamTransform.TransformPoint(localPos);

            // Azzeriamo le velocità fisiche
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}