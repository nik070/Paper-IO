using System.Collections.Generic;
using Core;
using UnityEngine;

public static class AudioClips
{
    public const string Tap = "Tap";
    public const string Locked = "Tap_Locked";
    public const string Kill = "Kill";
    public const string PlayerDie = "PlayerDie";
    public const string PlayerEat = "PlayerEating";
    public const string ZoneCaptured = "ZoneCaptured";
    public const string KillToast = "kill_toast";
    public const string GameOver = "GameOver";
    public const string WinCard = "WinCard";
    public const string EndCard = "EndCard";
}

public class AudioHandle
{
    private readonly AudioSource _source;

    public AudioHandle(AudioSource source)
    {
        _source = source;
    }

    public float Volume
    {
        get
        {
            if (_source != null)
            {
                return _source.volume;
            }

            return 0f;
        }
        set
        {
            if (_source != null)
            {
                _source.volume = Mathf.Clamp01(value);
            }
        }
    }

    public bool IsPlaying
    {
        get { return _source != null && _source.isPlaying; }
    }

    public void Stop()
    {
        if (_source != null)
        {
            _source.Stop();
        }
    }
}

[System.Serializable]
public class NamedClip
{
    public string Name;
    public AudioClip Clip;
    [Range(0f, 1f)] public float Volume = 1f;
    public bool Loop;
}

public class AudioManager : SingletonBehaviour<AudioManager>
{
    private const int PoolSize = 8;
    private const float MinTimeBetweenPlayCalls = 0.1f;

    private readonly Dictionary<string, NamedClip> _clipsByName = new Dictionary<string, NamedClip>();
    private readonly Dictionary<string, float> _lastPlayTime = new Dictionary<string, float>();
    private readonly Dictionary<string, AudioSource> _loopingSources = new Dictionary<string, AudioSource>();
    private readonly List<AudioSource> _pool = new List<AudioSource>(PoolSize);
    private readonly List<string> _loopingKeysScratch = new List<string>();
    private float _globalVolume = 1f;
    private int _nextPoolIndex;

    [SerializeField] private bool _soundEnabled = true;
    [SerializeField] [Range(0f, 1f)] private float _soundVolume = 1f;
    [SerializeField] private List<NamedClip> _clips = new List<NamedClip>();

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        BuildClipLookup();
        CreatePool();
        SetSoundVolume();
    }

    public AudioHandle Play(string soundName)
    {
        if (!_soundEnabled)
        {
            return null;
        }

        return PlayInternal(soundName, -1f);
    }

    public void PlayWithCustomVolume(string soundName, float volume)
    {
        if (!_soundEnabled)
        {
            return;
        }

        PlayInternal(soundName, volume);
    }

    public void Stop(string soundName)
    {
        if (_loopingSources.TryGetValue(soundName, out AudioSource looping))
        {
            if (looping != null)
            {
                looping.Stop();
                looping.clip = null;
            }

            _loopingSources.Remove(soundName);
            return;
        }

        if (!_clipsByName.TryGetValue(soundName, out NamedClip clip) || clip.Clip == null)
        {
            return;
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            AudioSource src = _pool[i];
            if (src != null && src.isPlaying && src.clip == clip.Clip)
            {
                src.Stop();
            }
        }
    }

    public void StopAllSounds()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            AudioSource src = _pool[i];
            if (src != null)
            {
                src.Stop();
            }
        }

        _loopingKeysScratch.Clear();
        foreach (KeyValuePair<string, AudioSource> kvp in _loopingSources)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Stop();
                kvp.Value.clip = null;
            }

            _loopingKeysScratch.Add(kvp.Key);
        }

        for (int i = 0; i < _loopingKeysScratch.Count; i++)
        {
            _loopingSources.Remove(_loopingKeysScratch[i]);
        }
        _loopingKeysScratch.Clear();
    }

    public void Pause()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null)
            {
                _pool[i].Pause();
            }
        }

        foreach (KeyValuePair<string, AudioSource> kvp in _loopingSources)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Pause();
            }
        }
    }

    public void Resume()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null)
            {
                _pool[i].UnPause();
            }
        }

        foreach (KeyValuePair<string, AudioSource> kvp in _loopingSources)
        {
            if (kvp.Value != null)
            {
                kvp.Value.UnPause();
            }
        }
    }

    public void SetVolume(float volume)
    {
        _globalVolume = Mathf.Clamp01(volume);
        ApplyGlobalVolumeToLoopingSources();
    }

    public void SetSound()
    {
        if (!_soundEnabled)
        {
            StopAllSounds();
        }
    }

    public void SetSoundVolume()
    {
        _globalVolume = Mathf.Clamp01(_soundVolume);
        ApplyGlobalVolumeToLoopingSources();
    }

    private AudioHandle PlayInternal(string soundName, float volumeOverride)
    {
        if (!_clipsByName.TryGetValue(soundName, out NamedClip clip) || clip.Clip == null)
        {
            return null;
        }

        if (_lastPlayTime.TryGetValue(soundName, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < MinTimeBetweenPlayCalls)
            {
                return null;
            }
        }
        _lastPlayTime[soundName] = Time.unscaledTime;

        float perClipVolume = volumeOverride >= 0f ? Mathf.Clamp01(volumeOverride) : clip.Volume;
        float finalVolume = perClipVolume * _globalVolume;

        if (clip.Loop)
        {
            AudioSource loopSource;
            if (_loopingSources.TryGetValue(soundName, out AudioSource existing) && existing != null)
            {
                loopSource = existing;
            }
            else
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                _loopingSources[soundName] = loopSource;
            }

            loopSource.clip = clip.Clip;
            loopSource.volume = finalVolume;
            loopSource.loop = true;
            if (!loopSource.isPlaying)
            {
                loopSource.Play();
            }

            return new AudioHandle(loopSource);
        }

        AudioSource poolSource = GetNextPoolSource();
        if (poolSource == null)
        {
            return null;
        }

        poolSource.clip = clip.Clip;
        poolSource.volume = finalVolume;
        poolSource.loop = false;
        poolSource.Play();
        return new AudioHandle(poolSource);
    }

    private AudioSource GetNextPoolSource()
    {
        if (_pool.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            int index = (_nextPoolIndex + i) % _pool.Count;
            AudioSource src = _pool[index];
            if (src != null && !src.isPlaying)
            {
                _nextPoolIndex = (index + 1) % _pool.Count;
                return src;
            }
        }

        AudioSource chosen = _pool[_nextPoolIndex];
        _nextPoolIndex = (_nextPoolIndex + 1) % _pool.Count;
        return chosen;
    }

    private void CreatePool()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            _pool.Add(src);
        }
    }

    private void BuildClipLookup()
    {
        _clipsByName.Clear();
        for (int i = 0; i < _clips.Count; i++)
        {
            NamedClip nc = _clips[i];
            if (nc == null || string.IsNullOrEmpty(nc.Name))
            {
                continue;
            }

            _clipsByName[nc.Name] = nc;
        }
    }

    private void ApplyGlobalVolumeToLoopingSources()
    {
        foreach (KeyValuePair<string, AudioSource> kvp in _loopingSources)
        {
            if (kvp.Value == null)
            {
                continue;
            }

            if (_clipsByName.TryGetValue(kvp.Key, out NamedClip clip))
            {
                kvp.Value.volume = clip.Volume * _globalVolume;
            }
        }
    }
}
