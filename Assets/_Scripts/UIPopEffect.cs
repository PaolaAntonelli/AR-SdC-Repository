using UnityEngine;
using System.Collections;
using TMPro; // Rimuovi se usi il testo standard di Unity

public class UIPopEffect : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private RectTransform m_PanelTransform;
    [SerializeField] private TextMeshProUGUI m_TextMessage; // Cambia in Text se non usi TMP

    [Header("Animation Settings")]
    [SerializeField] private AnimationCurve m_PopCurve;
    [SerializeField] private float m_Duration = 0.3f;

    private Coroutine m_AnimationCoroutine;

    public void ChangeMessageWithPop(string newText)
    {
        // Se c'è già un'animazione in corso, la ferma per evitare conflitti
        if (m_AnimationCoroutine != null)
            StopCoroutine(m_AnimationCoroutine);

        m_AnimationCoroutine = StartCoroutine(PopRoutine(newText));
    }

    private IEnumerator PopRoutine(string newText)
    {
        Vector3 originalScale = Vector3.one;
        float timer = 0f;

        // --- FASE 1: Rimpicciolisce il pannello (Pop Out) ---
        while (timer < m_Duration / 2f)
        {
            timer += Time.deltaTime;
            float progress = timer / (m_Duration / 2f);
            
            // Inverte la curva per andare da 1 a 0
            m_PanelTransform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
            yield return null;
        }
        m_PanelTransform.localScale = Vector3.zero;

        // Cambia il testo mentre il pannello non si vede
        if (m_TextMessage != null)
            m_TextMessage.text = newText;

        timer = 0f;

        // --- FASE 2: Fa ricomparire il pannello con rimbalzo (Pop In) ---
        while (timer < m_Duration / 2f)
        {
            timer += Time.deltaTime;
            float progress = timer / (m_Duration / 2f);
            
            // Valuta la curva di animazione per creare l'effetto molla
            float curveValue = m_PopCurve.Evaluate(progress);
            m_PanelTransform.localScale = originalScale * curveValue;
            yield return null;
        }
        
        // Assicura che la scala finale sia perfettamente 1,1,1
        m_PanelTransform.localScale = originalScale;
    }
}