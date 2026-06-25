using UnityEngine;

public class ARBridgeHelper : MonoBehaviour
{
    private Transform visualGroup;

    void Start()
    {
        // Trova l'oggetto VisualGroup che abbiamo creato dentro il prefab
        visualGroup = transform.Find("VisualGroup");
        
        if (visualGroup == null)
        {
            Debug.LogError("ARBridgeHelper: Impossibile trovare il 'VisualGroup' figlio! Controlla il nome.");
        }
    }

    void LateUpdate()
    {
        if (visualGroup == null) return;

        // Giriamo al contrario l'elenco dei figli del Root per evitare errori di indice durante lo spostamento
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            // Se troviamo un oggetto che NON è il VisualGroup, significa che è un carico
            // o un vincolo appena generato dal manager esterno!
            if (child != visualGroup)
            {
                // Lo adottiamo dentro VisualGroup.
                // Usando 'false', l'oggetto manterrà le sue coordinate locali relative alla trave,
                // ma si curverà e si sposterà istantaneamente insieme alla rotazione AR!
                child.SetParent(visualGroup, false);
            }
        }
    }
}