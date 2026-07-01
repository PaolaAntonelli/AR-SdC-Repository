using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets; // Namespace ufficiale ObjectSpawner
using ZXing;
using TMPro;

public class QRMarkerTracker : MonoBehaviour
{
    [Header("Configurazione")]
    [SerializeField] private ObjectSpawner mySpawner; 
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARAnchorManager anchorManager; 
    
    [Header("UI Debug")]
    [SerializeField] private TextMeshProUGUI statusText;

    private bool qrLetto = false;
    private GameObject spawnedObject; 
    private ARAnchor currentAnchor;   
    private BarcodeReaderGeneric qrReader;

    void Start()
    {
        qrReader = new BarcodeReaderGeneric { AutoRotate = true };
        
        // Iscrizione all'evento dello spawner
        if (mySpawner != null) mySpawner.objectSpawned += OnObjectSpawned;
        
        if (statusText) statusText.text = "Fase 1: Inquadra il QR Code...";
        StartCoroutine(ScanRoutine());
    }

    private void OnObjectSpawned(GameObject go)
    {
        spawnedObject = go; 
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

            // Richiede "Allow 'unsafe' Code" nelle Project Settings
            unsafe { fixed (byte* ptr = buffer) { image.Convert(conversionParams, (IntPtr)ptr, size); } }
            
            image.Dispose();

            var luminanceSource = new RGBLuminanceSource(buffer, conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, RGBLuminanceSource.BitmapFormat.Gray8);
            var result = qrReader.Decode(luminanceSource);

            if (result != null)
            {
                qrLetto = true;
                if (statusText) statusText.text = "QR Letto! Guarda il tavolo e premi il GRILLETTO.";
            }
        }
    }

    void Update()
    {
        if (qrLetto && (Input.GetButtonDown("Fire1") || Input.GetMouseButtonDown(0)))
        {
            EseguiSpawnOspostamento();
        }
    }

    private void EseguiSpawnOspostamento()
    {
        Camera cam = Camera.main;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        // Tenta il raycast su piani e superfici fisiche
        if (raycastManager.Raycast(ray, hits, TrackableType.AllTypes))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null)
            {
                // Prima volta: lo creiamo
                mySpawner.SpawnObject(hitPose.position, hitPose.up);
                CreaAncora(hitPose);
            }
            else
            {
                // Successive volte: spostiamo l'oggetto esistente
                spawnedObject.transform.position = hitPose.position;
                spawnedObject.transform.rotation = Quaternion.LookRotation(hitPose.up);
                
                // Aggiorniamo l'ancora
                if (currentAnchor != null) anchorManager.TryRemoveAnchor(currentAnchor);
                CreaAncora(hitPose);
                
                if (statusText) statusText.text = "Oggetto spostato sul tavolo!";
            }
        }
    }

    private async void CreaAncora(Pose pose)
    {
        // Metodo asincrono ufficiale per ancore stabili
        var result = await anchorManager.TryAddAnchorAsync(pose);
        if (result.status.IsSuccess())
        {
            currentAnchor = result.value;
            if (spawnedObject != null) spawnedObject.transform.SetParent(currentAnchor.transform);
        }
    }
}