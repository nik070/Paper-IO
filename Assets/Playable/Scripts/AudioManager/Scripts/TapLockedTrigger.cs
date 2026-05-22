using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TapLockedTrigger : MonoBehaviour, IPointerDownHandler
{
    private Button _button;

    public bool onlyIfInteractable;

    // Start is called before the first frame update
    private void Start()
    {
        _button = GetComponent<Button>();
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if ((!_button.interactable && !onlyIfInteractable) || (_button.interactable && onlyIfInteractable))
        {
            AudioManager.Instance.Play(AudioClips.Locked);
        }
    }
}