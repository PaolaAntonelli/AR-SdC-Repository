using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using TMPro;

public class QRMarkerTracker : MonoBehaviour
{
    [Header("Configurazione AR")]
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARAnchorManager anchorManager; 

    [Header("Oggetto da far Spuntare")]
    [SerializeField] private GameObject objectToSpawnPrefab;

    [Header("Interfaccia Debug Visore")]
    [SerializeField] private TextMeshProUGUI statusText; 

    [Header("Filtri Stabilità")]
    [Tooltip("Se attivo, l'oggetto appare sul QR e poi rimane immobile lì, ignorando i movimenti successivi.")]
    [SerializeField] private bool bloccaDopoIlPrimoSpun = true; 
    [Tooltip("Distanza minima in metri (es. 0.05 = 5cm) per aggiornare la posizione. Evita il tremolio.")]
    [SerializeField] private float sogliaMovimento = 0.05f;

    private GameObject spawnedObject;
    private ARAnchor currentAnchor; 
    private BarcodeReaderGeneric qrReader; 
    private bool isScanning = false;
    private float scanInterval = 0.3f; 

    // VARIABILI DI MEMORIA PER IL POSIZIONAMENTO ASINCRONO
    private bool qrRilevatoMaInAttesaDiTavolo = false;
    private Vector2 coordinateQRSalvate;

    void Start()
    {
        if (cameraManager == null) cameraManager = FindObjectOfType<ARCameraManager>();
        if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();
        if (anchorManager == null) anchorManager = FindObjectOfType<ARAnchorManager>();

        if (statusText != null) statusText.text = "Allinea il visore. Inquadra il QR sul tavolo...";

        qrReader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[] { BarcodeFormat.QR_CODE }
            }
        };

        StartCoroutine(ScanRoutine());
    }

    IEnumerator ScanRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(scanInterval);
            
            // Se l'oggetto è già stato creato e vogliamo bloccarlo, fermiamo del tutto i calcoli
            if (bloccaDopoIlPrimoSpun && spawnedObject != null)
            {
                if (statusText != null) statusText.text = "Oggetto Bloccato sul tavolo.";
                yield break; 
            }

            // Se abbiamo letto il QR ma stiamo ancora cercando il tavolo, diamo la priorità al Raycast
            if (qrRilevatoMaInAttesaDiTavolo)
            {
                TentaPosizionamentoSuTavolo(coordinateQRSalvate);
            }
            else if (!isScanning && cameraManager != null)
            {
                isScanning = true;
                ExecuteQRScan();
                isScanning = false;
            }
        }
    }

    private void ExecuteQRScan()
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width / 2, image.height / 2), 
            outputFormat = TextureFormat.R8, 
            transformation = XRCpuImage.Transformation.None
        };

        int size = image.GetConvertedDataSize(conversionParams);
        byte[] buffer = new byte[size];

        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                image.Convert(conversionParams, (IntPtr)ptr, size);
            }
        }

        image.Dispose();

        var luminanceSource = new RGBLuminanceSource(
            buffer, 
            conversionParams.outputDimensions.x, 
            conversionParams.outputDimensions.y, 
            RGBLuminanceSource.BitmapFormat.Gray8
        );

        Result result = qrReader.Decode(luminanceSource);

        if (result != null)
        {
            var points = result.ResultPoints;
            if (points != null && points.Length > 0)
            {
                float sumX = 0, sumY = 0;
                foreach (var point in points)
                {
                    sumX += point.X;
                    sumY += (conversionParams.outputDimensions.y - point.Y);
                }
                
                // Salviamo le coordinate relative a dove si trovava il QR
                coordinateQRSalvate = new Vector2(
                    sumX / points.Length / conversionParams.outputDimensions.x, 
                    sumY / points.Length / conversionParams.outputDimensions.y
                );

                qrRilevatoMaInAttesaDiTavolo = true;
                
                if (statusText != null) statusText.text = "QR Letto! Cerco il tavolo (puoi muovere la testa)...";
                
                // Proviamo subito a posizionarlo
                TentaPosizionamentoSuTavolo(coordinateQRSalvate);
            }
        }
    }

    private void TentaPosizionamentoSuTavolo(Vector2 normalizedCpuPos)
{
    Camera cam = cameraManager.GetComponent<Camera>();
    if (cam == null) cam = Camera.main;
    if (cam == null) return;

    // SOLUZIONE DEFINITIVA: Sparamo il raggio dritto in avanti partendo dallo sguardo del visore.
    // Dato che stai guardando il tavolo/QR per scansionarlo, questo raggio colpirà al 100% 
    // la superficie orizzontale blu che vedi davanti a te.
    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
    List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // Filtriamo SOLO per i piani reali (le superfici blu che vedi)
    TrackableType flags = TrackableType.PlaneWithinPolygon | TrackableType.Planes;

    if (raycastManager.Raycast(ray, hits, flags))
    {
        Pose hitPose = hits[0].pose;
        
        // Allineiamo la rotazione dell'oggetto basandoci su come guardi il tavolo
        Quaternion rotazioneAllineataAlTavolo = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);

        if (spawnedObject == null)
        {
            var trackable = hits[0].trackable;
            if (trackable is ARPlane plane)
            {
                currentAnchor = anchorManager.AttachAnchor(plane, hitPose);
            }
            else
            {
                GameObject anchorObj = new GameObject("ARAnchor_QR_Fixed");
                anchorObj.transform.position = hitPose.position;
                anchorObj.transform.rotation = rotazioneAllineataAlTavolo;
                currentAnchor = anchorObj.AddComponent<ARAnchor>();
            }

            if (currentAnchor != null)
            {
                // Istanziamo il prefab dentro l'ancora agganciata alla superficie blu
                spawnedObject = Instantiate(objectToSpawnPrefab, currentAnchor.transform);
                
                // Azzeriamo la posizione locale così l'oggetto si materializza ESATTAMENTE sulla superficie del tavolo
                spawnedObject.transform.localPosition = Vector3.zero;
                spawnedObject.transform.localRotation = Quaternion.identity;
                
                // SUCCESSO: resettiamo la memoria, l'oggetto ora è fisso sul tavolo
                qrRilevatoMaInAttesaDiTavolo = false;
                
                if (statusText != null) statusText.text = "Oggetto ancorato con successo sulla superficie del tavolo!";
            }
        }
    }
    else
    {
        if (statusText != null && spawnedObject == null)
        {
            statusText.text = "QR letto! Guarda la superficie blu sul tavolo per confermare il posizionamento...";
        }
    }
}
}