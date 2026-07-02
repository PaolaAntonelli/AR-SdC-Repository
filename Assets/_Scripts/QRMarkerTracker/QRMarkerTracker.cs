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
    // Definiamo gli stati possibili della nostra applicazione
    public enum TrackerState
    {
        Scanning,       // Fase 1: Ricerca QR
        ReadyToSpawn,   // Fase 2: QR Trovato, attesa tocco
        ObjectSpawned   // Fase 3: Oggetto istanziato, modalità spostamento
    }

    [Header("Configurazione")]
    [SerializeField] private ObjectSpawner mySpawner; 
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARRaycastManager raycastManager;
    
    [Header("UI Menu (Pannelli)")]
    [SerializeField] private GameObject panelScanning;      // Pannello: "Inquadra il QR..."
    [SerializeField] private GameObject panelReadyToSpawn;  // Pannello: "QR Letto! Tocca..."
    [SerializeField] private GameObject panelObjectSpawned; // Pannello: "Oggetto Istanziato..."
    
    [Header("UI Info (Opzionale)")]
    [SerializeField] private TextMeshProUGUI infoText; // Se vuoi mostrare piccoli feedback temporanei (es. "Spostato!")

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
        
        // Partiamo dallo stato di Scansione
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
                // Cambio stato: QR Letto
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
            // Cambio stato: Oggetto Spawnato
            AggiornaStatoUI(TrackerState.ObjectSpawned);
        }
        else
        {
            Destroy(go);
        }
    }

    void Update()
    {
        if (spawnedObject != null && TryGetTouchPosition(out Vector2 touchPosition))
        {
            SpostaOggettoEsistente(touchPosition);
        }
    }

    private bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                touchPosition = touch.position;
                return true;
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            touchPosition = Input.mousePosition;
            return true;
        }

        touchPosition = default;
        return false;
    }

    private void SpostaOggettoEsistente(Vector2 touchPosition)
    {
        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        if (raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;
            spawnedObject.transform.position = hitPose.position;
            
            // Per i feedback rapidi (es. "Spostato!"), un testo dinamico dentro al pannello attivo è comodo
            if (infoText) infoText.text = "Oggetto spostato!";
        }
    }

    /// <summary>
    /// Gestore centrale della UI. Attiva il pannello corretto e spegne gli altri.
    /// </summary>
    private void AggiornaStatoUI(TrackerState nuovoStato)
    {
        currentState = nuovoStato;

        // Reset iniziale di tutti i pannelli (evita che si sovrappongano)
        if (panelScanning) panelScanning.SetActive(false);
        if (panelReadyToSpawn) panelReadyToSpawn.SetActive(false);
        if (panelObjectSpawned) panelObjectSpawned.SetActive(false);

        // Attiva solo il pannello legato allo stato attuale
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