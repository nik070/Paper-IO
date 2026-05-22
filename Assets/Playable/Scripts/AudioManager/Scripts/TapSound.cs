using UnityEngine;
using UnityEngine.UI;

public class TapSound : MonoBehaviour
{
    private Button _button;

    private void OnEnable()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlaySound);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(PlaySound);
    }

    private void PlaySound()
    {
        AudioManager.Instance.Play(AudioClips.Tap);
    }
}