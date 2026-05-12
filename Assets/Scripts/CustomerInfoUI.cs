using TMPro;
using UnityEngine;

public class CustomerInfoUI : MonoBehaviour
{
    public static CustomerInfoUI Instance;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text infoText;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(CustomerAgent customer)
    {
        panel.SetActive(true);
        infoText.text = customer.GetInfoText();
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}