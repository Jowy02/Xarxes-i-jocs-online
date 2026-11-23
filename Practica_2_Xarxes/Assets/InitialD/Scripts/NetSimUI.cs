using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetSimUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    [Header("Sliders")]
    [SerializeField] private Slider latencySlider;
    [SerializeField] private Slider jitterSlider;
    [SerializeField] private Slider dropSlider;

    [Header("Value Texts")]
    [SerializeField] private TMP_Text latencyText;
    [SerializeField] private TMP_Text jitterText;
    [SerializeField] private TMP_Text dropText;

    [Header("Buttons")]
    [SerializeField] private Button resetButton;

    private NetSim[] sims = new NetSim[0];

    void Start()
    {
        panel.SetActive(false);

        latencySlider.onValueChanged.AddListener(_ => ApplyValues());
        jitterSlider.onValueChanged.AddListener(_ => ApplyValues());
        dropSlider.onValueChanged.AddListener(_ => ApplyValues());

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetValues);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        bool open = !panel.activeSelf;

        if (open)
        {
            // Busca los NetSim al abrir (el del cliente y el del servidor)
            sims = FindObjectsByType<NetSim>(FindObjectsSortMode.None);
            LoadValues();
        }

        panel.SetActive(open);
    }

    // Copia los valores actuales del NetSim a los sliders
    private void LoadValues()
    {
        if (sims.Length == 0)
        {
            Debug.LogWarning("[NETSIM UI] No NetSim found.");
            return;
        }

        latencySlider.SetValueWithoutNotify(sims[0].latency);
        jitterSlider.SetValueWithoutNotify(sims[0].jitter);
        dropSlider.SetValueWithoutNotify(sims[0].dropChance);

        UpdateTexts();
    }

    // Copia los valores de los sliders a todos los NetSim
    private void ApplyValues()
    {
        foreach (NetSim sim in sims)
        {
            if (sim == null) continue;

            sim.latency = latencySlider.value;
            sim.jitter = jitterSlider.value;
            sim.dropChance = dropSlider.value;
        }

        UpdateTexts();
    }

    private void ResetValues()
    {
        latencySlider.value = 0f;
        jitterSlider.value = 0f;
        dropSlider.value = 0f;
    }

    private void UpdateTexts()
    {
        latencyText.text = Mathf.RoundToInt(latencySlider.value * 1000f) + " ms";
        jitterText.text = Mathf.RoundToInt(jitterSlider.value * 1000f) + " ms";
        dropText.text = Mathf.RoundToInt(dropSlider.value * 100f) + " %";
    }

    void OnDestroy()
    {
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetValues);
    }
}