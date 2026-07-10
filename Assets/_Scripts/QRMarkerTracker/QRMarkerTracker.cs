using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using ZXing;
using TMPro;

public class QRMarkerTracker : MonoBehaviour
{
    public enum TrackerState
    {
        Scanning,
        ReadyToSpawn,
        ObjectSpawned
    }

    [Header("Configurazione")]
    [SerializeField] private ObjectSpawner mySpawner; 
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARRaycastManager raycastManager;
    
    [Header("Configurazione Input Visore")]
    [Tooltip("Trascina qui il Transform del tuo controller (es. RightHand Controller o Ray Interactor) per sapere da dove sparare il raggio di spostamento.")]
    [SerializeField] private Transform controllerPointer;

    [Header("UI Menu (Pannelli)")]
    [SerializeField] private GameObject panelScanning;      
    [SerializeField] private GameObject panelReadyToSpawn;  
    [SerializeField] private GameObject panelObjectSpawned; 
    
    [Header("UI Info (Opzionale)")]
    [SerializeField] private TextMeshProUGUI infoText; 

    private bool qrLetto = false;
    private GameObject spawnedObject = null; 
    private BarcodeReaderGeneric qrReader;
    private TrackerState currentState;

    void Start()
    {
        qrReader = new BarcodeReaderGeneric { AutoRotate = true };
        
        if (mySpawner != null)
        {
            mySpawner.objectSpawned += OnObjectSpawned; 
        }
        
        AggiornaStatoUI(TrackerState.Scanning);
        StartCoroutine(ScanRoutine());
    }

    private void OnDestroy()
    {
        if (mySpawner != null)
        {
            mySpawner.objectSpawned -= OnObjectSpawned;
        }
    }

    IEnumerator ScanRoutine()
    {
        while (!qrLetto)
        {
            EseguiScansioneQR();
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void EseguiScansioneQR()
    {
        if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            var conversionParams = new XRCpuImage.ConversionParams 
            { 
                inputRect = new RectInt(0, 0, image.width, image.height), 
                outputDimensions = new Vector2Int(image.width / 2, image.height / 2), 
                outputFormat = TextureFormat.R8, 
                transformation = XRCpuImage.Transformation.None 
            };

            int size = image.GetConvertedDataSize(conversionParams);
            byte[] buffer = new byte[size];

            unsafe { fixed (byte* ptr = buffer) { image.Convert(conversionParams, (IntPtr)ptr, size); } }
            
            image.Dispose();

            var luminanceSource = new RGBLuminanceSource(buffer, conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, RGBLuminanceSource.BitmapFormat.Gray8);
            var result = qrReader.Decode(luminanceSource);

            if (result != null)
            {
                qrLetto = true;
                AggiornaStatoUI(TrackerState.ReadyToSpawn);
            }
        }
    }

    private void OnObjectSpawned(GameObject go)
    {
        if (!qrLetto)
        {
            Destroy(go);
            return;
        }

        if (spawnedObject == null)
        {
            spawnedObject = go;
            AggiornaStatoUI(TrackerState.ObjectSpawned);
        }
        else
        {
            Destroy(go);
        }
    }

    void Update()
    {
        // Adesso passiamo ad Update un vero e proprio Ray fisico nello spazio 3D
        if (spawnedObject != null && TryGetInteractionRay(out Ray interactionRay))
        {
            SpostaOggettoEsistente(interactionRay);
        }
    }

    // NUOVO METODO DI INPUT PER VISORE / EDITOR
    private bool TryGetInteractionRay(out Ray ray)
    {
        // Se siamo nell'Editor di Unity, permettiamo il test classico col click del mouse
        if (Application.isEditor)
        {
            if (Input.GetMouseButtonDown(0))
            {
                ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                return true;
            }
        }
        else // Se siamo dentro al Meta Quest
        {
            // Il trigger del controller viene comunque interpretato come GetMouseButtonDown o Touch
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                // Se hai assegnato il controller nell'Inspector, spara il raggio dalla punta del controller
                if (controllerPointer != null)
                {
                    ray = new Ray(controllerPointer.position, controllerPointer.forward);
                }
                else
                {
                    // Fallback: spara il raggio partendo dal centro del tuo visore (sguardo/gaze)
                    ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                }
                return true;
            }
        }

        ray = default;
        return false;
    }

    // MODIFICATO: Accetta il Ray invece del Vector2
    private void SpostaOggettoEsistente(Ray interactionRay)
    {
        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        // Sfruttiamo l'overload nativo di ARRaycastManager che accetta un Ray fisicamente orientato nello spazio
        if (raycastManager.Raycast(interactionRay, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;
            spawnedObject.transform.position = hitPose.position;
            
            if (infoText) infoText.text = "Oggetto spostato!";
        }
    }

    private void AggiornaStatoUI(TrackerState nuovoStato)
    {
        currentState = nuovoStato;

        if (panelScanning) panelScanning.SetActive(false);
        if (panelReadyToSpawn) panelReadyToSpawn.SetActive(false);
        if (panelObjectSpawned) panelObjectSpawned.SetActive(false);

        switch (nuovoStato)
        {
            case TrackerState.Scanning:
                if (panelScanning) panelScanning.SetActive(true);
                break;

            case TrackerState.ReadyToSpawn:
                if (panelReadyToSpawn) panelReadyToSpawn.SetActive(true);
                break;

            case TrackerState.ObjectSpawned:
                if (panelObjectSpawned) panelObjectSpawned.SetActive(true);
                if (infoText) infoText.text = "Tocca un altro punto per spostarlo.";
                break;
        }
    }
}