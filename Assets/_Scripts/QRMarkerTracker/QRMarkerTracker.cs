using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using TMPro; // Estensione obbligatoria per usare TextMeshPro

public class QRMarkerTracker : MonoBehaviour
{
    [Header("Configurazione AR")]
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARRaycastManager raycastManager;
    
    [Header("Oggetto da far Spuntare")]
    [SerializeField] private GameObject objectToSpawnPrefab;

    [Header("Interfaccia Debug Visore")]
    [SerializeField] private TextMeshProUGUI statusText; // Trascina qui il tuo testo TMP
    // --- AGGIUNGI QUESTE DUE VARIABILI IN CIMA ALLO SCRIPT (sotto le altre variabili [SerializeField]) ---
[Header("Filtri Stabilità (Nuovi)")]
[Tooltip("Se attivo, l'oggetto appare sul QR e poi rimane immobile lì, ignorando i movimenti successivi.")]
[SerializeField] private bool bloccaDopoIlPrimoSpun = false; 
[Tooltip("Distanza minima in metri (es. 0.03 = 3cm) per aggiornare la posizione. Evita il tremolio.")]
[SerializeField] private float sogliaMovimento = 0.03f;
    
    private GameObject spawnedObject;
    private BarcodeReaderGeneric qrReader; // Usiamo la classe generica corretta per i pixel nativi
    private bool isScanning = false;
    private float scanInterval = 0.5f; 

    void Start()
    {
        if (cameraManager == null) cameraManager = FindObjectOfType<ARCameraManager>();
        if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();

        // Imposta il testo iniziale
        if (statusText != null) statusText.text = "Scanner Attivo. Inquadra un QR...";

        // Inizializza ZXing
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
            
            if (!isScanning && cameraManager != null)
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
    if (statusText != null) statusText.text = "ERRORE: La telecamera non passa i pixel!";
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

        // Impacchettiamo i pixel per ZXing
        var luminanceSource = new RGBLuminanceSource(
            buffer, 
            conversionParams.outputDimensions.x, 
            conversionParams.outputDimensions.y, 
            RGBLuminanceSource.BitmapFormat.Gray8
        );

        Result result = qrReader.Decode(luminanceSource);

        if (result != null)
        {
            // --- AGGIORNAMENTO SCRITTA NEL VISORE ---
            if (statusText != null)
            {
                statusText.text = "QR RILEVATO! Contenuto: " + result.Text;
            }
            
            var points = result.ResultPoints;
            if (points != null && points.Length > 0)
            {
                float sumX = 0, sumY = 0;
                foreach (var point in points)
                {
                    sumX += point.X;
                    sumY += point.Y;
                }
                Vector2 qrCenterNormalized = new Vector2(sumX / points.Length / conversionParams.outputDimensions.x, sumY / points.Length / conversionParams.outputDimensions.y);
                
                Position3DObject(qrCenterNormalized);
            }
        }
    }

    private void Position3DObject(Vector2 normalizedScreenPos)
{
    Camera cam = cameraManager.GetComponent<Camera>();
    if (cam == null) cam = Camera.main;
    if (cam == null) return;

    Ray ray = cam.ViewportPointToRay(new Vector3(normalizedScreenPos.x, normalizedScreenPos.y, 0));
    System.Collections.Generic.List<ARRaycastHit> hits = new System.Collections.Generic.List<ARRaycastHit>();

    if (raycastManager.Raycast(ray, hits, TrackableType.AllTypes))
    {
        Pose hitPose = hits[0].pose;

        // SPIEGAZIONE ROTAZIONE:
        // Prendiamo l'angolo Y di come tu stai guardando il QR Code (cam.transform.eulerAngles.y)
        // Impostiamo X e Z rigidamente a 0.
        // Questo garantisce che la Root sia perfettamente parallela al tavolo, senza inclinazioni strane su Z.
        Quaternion rotazioneAllineataAllaCamera = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);

        if (spawnedObject == null)
        {
            // Istanziamo l'INTERA ROOT con la rotazione del tuo sguardo
            spawnedObject = Instantiate(objectToSpawnPrefab, hitPose.position, rotazioneAllineataAllaCamera);
            
            if (statusText != null) statusText.text = "Oggetto inserito e allineato al tuo sguardo!";
        }
        else
        {
            if (!bloccaDopoIlPrimoSpun)
            {
                float distanzaDalVecchioPunto = Vector3.Distance(spawnedObject.transform.position, hitPose.position);
                
                if (distanzaDalVecchioPunto > sogliaMovimento)
                {
                    // Spostiamo e ruotiamo l'INTERA ROOT
                    spawnedObject.transform.position = hitPose.position;
                    spawnedObject.transform.rotation = rotazioneAllineataAllaCamera;
                }
            }
        }
    }
    else
    {
        if (spawnedObject == null && statusText != null) 
        {
            statusText.text = "QR Letto, in attesa di rilevare il tavolo... Muovi il visore.";
        }
    }
}
}