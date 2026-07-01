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
    [SerializeField] private ARAnchorManager anchorManager; // <--- AGGIUNTO: Trascina l'ARAnchorManager qui

    [Header("Oggetto da far Spuntare")]
    [SerializeField] private GameObject objectToSpawnPrefab;

    [Header("Interfaccia Debug Visore")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Filtri Stabilità")]
    [Tooltip("Se attivo, l'oggetto appare sul QR e poi rimane immobile lì, ignorando i movimenti successivi.")]
    [SerializeField] private bool bloccaDopoIlPrimoSpun = true; // Consigliato 'true' per QR fissi
    [Tooltip("Distanza minima in metri (es. 0.05 = 5cm) per aggiornare la posizione. Evita il tremolio.")]
    [SerializeField] private float sogliaMovimento = 0.05f;

    private GameObject spawnedObject;
    private ARAnchor currentAnchor; // Gestisce l'ancoraggio al mondo reale
    private BarcodeReaderGeneric qrReader;
    private bool isScanning = false;
    private float scanInterval = 0.5f;

    void Start()
    {
        if (cameraManager == null) cameraManager = FindObjectOfType<ARCameraManager>();
        if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();
        if (anchorManager == null) anchorManager = FindObjectOfType<ARAnchorManager>();

        if (statusText != null) statusText.text = "Scanner Attivo. Inquadra un QR...";

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
        if (bloccaDopoIlPrimoSpun && spawnedObject != null) return; // Stop scansione se abbiamo già l'oggetto bloccato

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
                    // ZXing ha lo 0,0 in alto a sinistra, Unity in basso a sinistra. Invertiamo la Y.
                    sumY += (conversionParams.outputDimensions.y - point.Y);
                }

                // Coordinate normalizzate (0-1) rispetto all'immagine CPU
                Vector2 qrCenterNormalized = new Vector2(
                    sumX / points.Length / conversionParams.outputDimensions.x,
                    sumY / points.Length / conversionParams.outputDimensions.y
                );

                Position3DObject(qrCenterNormalized);
            }
        }
    }

    private void Position3DObject(Vector2 normalizedCpuPos)
    {
        Camera cam = cameraManager.GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // CORREZIONE CRUCIALE: Convertiamo la posizione della CPU in coordinate dello schermo effettive del visore
        Vector3 screenPos = new Vector3(normalizedCpuPos.x * Screen.width, normalizedCpuPos.y * Screen.height, 0);
        Ray ray = cam.ScreenPointToRay(screenPos);
        
        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        // Cerchiamo solo piani stimati o tracciati (il tavolo)
        // Cerchiamo solo piani reali tracciati (all'interno dei poligoni rilevati) o piani generici
            if (raycastManager.Raycast(ray, hits, TrackableType.PlaneWithinPolygon | TrackableType.Planes))        {
            Pose hitPose = hits[0].pose;
            Quaternion rotazioneAllineataAlTavolo = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);

            if (spawnedObject == null)
            {
                // Instanziamo l'oggetto
                spawnedObject = Instantiate(objectToSpawnPrefab, hitPose.position, rotazioneAllineataAlTavolo);
                
                // Creiamo un Anchor sul piano per bloccarlo nel mondo reale
                currentAnchor = anchorManager.AttachAnchor((ARPlane)hits[0].trackable, hitPose);
                if (currentAnchor != null)
                {
                    spawnedObject.transform.SetParent(currentAnchor.transform, true);
                }

                if (statusText != null) statusText.text = "QR Rilevato! Oggetto ancorato al tavolo.";
            }
            else if (!bloccaDopoIlPrimoSpun)
            {
                // Se non è bloccato, aggiorna solo se supera la soglia per evitare il tremolio
                float distanzaDalVecchioPunto = Vector3.Distance(spawnedObject.transform.position, hitPose.position);
                if (distanzaDalVecchioPunto > sogliaMovimento)
                {
                    // Rimuovi vecchio anchor se esiste
                    if (currentAnchor != null) Destroy(currentAnchor);

                    // Sposta l'oggetto e ricrea l'anchor
                    spawnedObject.transform.position = hitPose.position;
                    spawnedObject.transform.rotation = rotazioneAllineataAlTavolo;

                    currentAnchor = anchorManager.AttachAnchor((ARPlane)hits[0].trackable, hitPose);
                    if (currentAnchor != null)
                    {
                        spawnedObject.transform.SetParent(currentAnchor.transform, true);
                    }
                }
            }
        }
        else
        {
            if (spawnedObject == null && statusText != null)
            {
                statusText.text = "QR Letto. Muovi il visore per rilevare il piano del tavolo...";
            }
        }
    }
}