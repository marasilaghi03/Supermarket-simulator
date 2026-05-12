using TMPro;
using UnityEngine;

public class PausePlay : MonoBehaviour
{
    [SerializeField] private TMP_Text buttonText;

    private bool paused = false;

    public void TogglePause()
    {
        paused = !paused;

        Time.timeScale = paused ? 0f : 1f;

        if (buttonText != null)
            buttonText.text = paused ? "Play" : "Pause";
    }

    public void Pause()
    {
        paused = true;
        Time.timeScale = 0f;

        if (buttonText != null)
            buttonText.text = "Play";
    }

    public void Play()
    {
        paused = false;
        Time.timeScale = 1f;

        if (buttonText != null)
            buttonText.text = "Pause";
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}