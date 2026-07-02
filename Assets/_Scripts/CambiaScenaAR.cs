using UnityEngine;
using UnityEngine.SceneManagement;

public class CambiaScenaAR : MonoBehaviour
{
    // Questa funzione verrà richiamata dal tuo bottone
    public void CaricaScena(string nomeScena)
    {
        // Ricarica o passa alla scena specificata
        SceneManager.LoadScene(nomeScena);
    }
}