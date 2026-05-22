using UnityEngine;

public class LocalSoundTrigger : MonoBehaviour
{
    public void Play(string soundName)
    {
        AudioManager.Instance.Play(soundName);
    }
}