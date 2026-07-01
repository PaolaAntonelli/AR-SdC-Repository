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
    [Tooltip("Trascina qui il PREFAB del tuo oggetto, non lo spawner!")]
    [SerializeField] private GameObject objectToSpawnPrefab;

    [Header("UI Debug")]
    [SerializeField] private TextMeshProUGUI statusText;

    private bool qrLetto = false;
    private GameObject spawnedObject; 
    private ARAnchor currentAnchor;   
    private BarcodeReaderGeneric qrReader;

    void Start()
    {
        qrReader = new BarcodeReaderGeneric { AutoRotate = true };
        if (statusText) statusText.text = "Fase 1: Inquadra il QR Code...";
        StartCoroutine(ScanRoutine());
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
        // Intercetta il click/trigger
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
                // PRIMA VOLTA: Creiamo l'oggetto dal Prefab
                spawnedObject = Instantiate(objectToSpawnPrefab, hitPose.position, Quaternion.identity);
                AllineaAllaCamera(spawnedObject, hitPose.up, cam);
                CreaAncora(hitPose);
                
                if (statusText) statusText.text = "Oggetto istanziato! Clicca altrove per spostarlo.";
            }
            else
            {
                // VOLTE SUCCESSIVE: Spostiamo lo stesso oggetto senza duplicarlo
                spawnedObject.transform.position = hitPose.position;
                AllineaAllaCamera(spawnedObject, hitPose.up, cam);
                
                // Rimuoviamo la vecchia ancora e creiamo la nuova
                if (currentAnchor != null) anchorManager.TryRemoveAnchor(currentAnchor);
                CreaAncora(hitPose);
                
                if (statusText) statusText.text = "Oggetto spostato!";
            }
        }
    }

    // QUESTA È LA MAGIA PRESA DA OBJECTSPAWNER:
    // Calcola l'orientamento per far apparire l'oggetto dritto verso i tuoi occhi,
    // allineandolo perfettamente all'inclinazione del tavolo.
    private void AllineaAllaCamera(GameObject obj, Vector3 spawnNormal, Camera cam)
    {
        Vector3 forward = cam.transform.position - obj.transform.position;
        Vector3 projectedForward = Vector3.ProjectOnPlane(forward, spawnNormal);
        obj.transform.rotation = Quaternion.LookRotation(projectedForward, spawnNormal);
    }

    private async void CreaAncora(Pose pose)
    {
        // Generiamo l'ancora solida asincrona per inchiodarlo al tavolo
        var result = await anchorManager.TryAddAnchorAsync(pose);
        if (result.status.IsSuccess())
        {
            currentAnchor = result.value;
            if (spawnedObject != null) spawnedObject.transform.SetParent(currentAnchor.transform);
        }
    }
}