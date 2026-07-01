using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets; // Namespace ufficiale di ObjectSpawner
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
    private GameObject spawnedObject; 
    private BarcodeReaderGeneric qrReader;

    void Start()
    {
        qrReader = new BarcodeReaderGeneric { AutoRotate = true };
        
        // 1. STRATEGIA INIZIALE: Disabilitiamo lo spawner all'avvio
        if (mySpawner != null)
        {
            mySpawner.enabled = false;
            // Ascoltiamo l'evento ufficiale dello spawner per sapere quando finisce il suo lavoro[cite: 2]
            mySpawner.objectSpawned += OnObjectSpawned; 
        }
        
        if (statusText) statusText.text = "Fase 1: Inquadra il QR Code...";
        StartCoroutine(ScanRoutine());
    }

    private void OnObjectSpawned(GameObject go)
    {
        // Salviamo l'oggetto unico appena creato
        spawnedObject = go; 
        
        // 3. CHIUSURA: L'oggetto è stato creato, spegniamo forzatamente lo spawner!
        if (mySpawner != null)
        {
            mySpawner.enabled = false;
        }

        if (statusText) statusText.text = "Oggetto Istanziato! I click futuri lo sposteranno soltanto.";
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
                
                // 2. ACCENSIONE: Il QR è stato letto, riattiviamo lo spawner per fargli accettare il tocco
                if (mySpawner != null)
                {
                    mySpawner.enabled = true;
                }

                if (statusText) statusText.text = "QR Letto! Guarda il tavolo e clicca/tocca per spawnare l'oggetto.";
            }
        }
    }

    void Update()
    {
        // 4. SPOSTAMENTO: Se l'oggetto esiste già, lo spawner è disattivato.
        // I click nativi vengono ignorati dallo spawner, quindi li catturiamo noi per SPOSTARE l'oggetto.
        if (spawnedObject != null && (Input.GetButtonDown("Fire1") || Input.GetMouseButtonDown(0)))
        {
            SpostaOggettoEsistente();
        }
    }

    private void SpostaOggettoEsistente()
    {
        Camera cam = Camera.main;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        // Cerchiamo le superfici mappate dal visore
        if (raycastManager.Raycast(ray, hits, TrackableType.AllTypes))
        {
            Pose hitPose = hits[0].pose;
            
            // Spostiamo e ruotiamo l'oggetto sul nuovo punto che stiamo guardando
            spawnedObject.transform.position = hitPose.position;
            
            if (statusText) statusText.text = "Oggetto spostato!";
        }
    }
}