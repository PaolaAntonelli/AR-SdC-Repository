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
    [Header("Configurazione")]
    [SerializeField] private ObjectSpawner mySpawner; 
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARRaycastManager raycastManager;
    
    [Header("UI Debug")]
    [SerializeField] private TextMeshProUGUI statusText;

    private bool qrLetto = false;
    private GameObject spawnedObject = null; 
    private BarcodeReaderGeneric qrReader;

    void Start()
    {
        qrReader = new BarcodeReaderGeneric { AutoRotate = true };
        
        if (mySpawner != null)
        {
            // Ci iscriviamo all'evento per intercettare OGNI volta che lo spawner crea qualcosa
            mySpawner.objectSpawned += OnObjectSpawned; 
        }
        
        if (statusText) statusText.text = "Fase 1: Inquadra il QR Code...";
        StartCoroutine(ScanRoutine());
    }

    private void OnDestroy()
    {
        // Buona pratica: disiscriversi dagli eventi quando l'oggetto viene distrutto
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
                if (statusText) statusText.text = "QR Letto! Tocca il tavolo per spawnare l'oggetto.";
            }
        }
    }

    // 1. IL GESTORE DEGLI SPAWN (Il trucco per "spegnere" lo spawner)
    private void OnObjectSpawned(GameObject go)
    {
        // Se il QR non è ancora stato letto, distruggiamo preventivamente qualsiasi spawn involontario
        if (!qrLetto)
        {
            Destroy(go);
            return;
        }

        // Se l'oggetto non esiste ancora, lo salviamo (Primo Spawn autorizzato)
        if (spawnedObject == null)
        {
            spawnedObject = go;
            if (statusText) statusText.text = "Oggetto Istanziato! Tocca un altro punto per spostarlo.";
        }
        else
        {
            // Se l'oggetto esiste già, significa che l'utente ha toccato di nuovo lo schermo.
            // Poiché l'ObjectSpawner è testardo e ignora l'enabled, distruggiamo il clone istantaneamente.
            Destroy(go);
        }
    }

    void Update()
    {
        // 2. SPOSTAMENTO: Solo se l'oggetto è stato spawnato catturiamo l'input per muoverlo
        if (spawnedObject != null && TryGetTouchPosition(out Vector2 touchPosition))
        {
            SpostaOggettoEsistente(touchPosition);
        }
    }

    // Supporto per tocco su schermo (Mobile) o Click (Editor)
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

        // Usiamo la posizione del tocco a schermo per lanciare il raycast sui piani AR
        if (raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;
            
            // Spostiamo l'oggetto nel nuovo punto rilevato
            spawnedObject.transform.position = hitPose.position;
            
            if (statusText) statusText.text = "Oggetto spostato con successo!";
        }
    }
}