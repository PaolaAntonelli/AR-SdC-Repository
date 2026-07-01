
using UnityEngine;

using UnityEngine.InputSystem;

 

public class DraggableLoad : MonoBehaviour

{

    public BeamController beamController;

    private bool isDragging = false;

    private Camera mainCamera;

    private float initialY;

 

    void Awake() => mainCamera = Camera.main;

    void Start() => initialY = transform.position.y;

 

    void Update()

    {

        if (Mouse.current.leftButton.wasPressedThisFrame)

        {

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform) isDragging = true;

        }

 

        if (!Mouse.current.leftButton.isPressed) isDragging = false;

 

        if (isDragging) Drag();

    }

 

    void Drag()
    {
        if (beamController == null || beamController.beamObject == null) return;
        Transform beamTransform = beamController.beamObject.transform;

        Vector3 mPos = Mouse.current.position.ReadValue();
        // Calcoliamo il piano di trascinamento proiettando il raggio sul piano locale della trave
        Ray ray = mainCamera.ScreenPointToRay(mPos);
        Plane beamPlane = new Plane(beamTransform.forward, beamTransform.position);

        if (beamPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 worldPoint = ray.GetPoint(enterDistance);
            Vector3 localPoint = beamTransform.InverseTransformPoint(worldPoint);

            // Clamping nello spazio locale della trave
            float minX = beamController.BeamStartX;
            float maxX = beamController.BeamStartX + beamController.BeamLength;
            float cX = Mathf.Clamp(localPoint.x, minX, maxX);

            // Manteniamo intatte le coordinate Y e Z locali originarie dell'oggetto
            Vector3 localReset = beamTransform.InverseTransformPoint(transform.position);
            Vector3 targetLocalPos = new Vector3(cX, localReset.y, localReset.z);

            // Applichiamo la nuova posizione globale
            transform.position = beamTransform.TransformPoint(targetLocalPos);
        }
    }

}

