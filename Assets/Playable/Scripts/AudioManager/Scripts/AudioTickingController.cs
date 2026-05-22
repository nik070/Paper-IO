using UnityEngine;

public class AudioTickingController
{
    private readonly string _soundName;

    private int _soundCounter;
    private int _soundChecker;

    public AudioTickingController(string soundName)
    {
        _soundName = soundName;
    }

    public void UpdateValue(float value)
    {
        _soundCounter = Mathf.FloorToInt(value * 100);

        if (_soundChecker != _soundCounter)
        {
            AudioManager.Instance.Play(_soundName);
            _soundChecker = _soundCounter;
        }
    }
}