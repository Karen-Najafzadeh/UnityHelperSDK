using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A comprehensive audio management helper that handles sound effects,
/// music, audio pools, and mixer control.
/// 
/// Features:
/// - Sound effect and music playback
/// - Audio source pooling
/// - 3D spatial audio
/// - Mixer parameter control
/// - Volume fade transitions
/// - Audio group management
/// </summary>
public static class AudioHelper
{
    // Audio source pools
    private static readonly Dictionary<string, Queue<AudioSource>> _audioSourcePools 
        = new Dictionary<string, Queue<AudioSource>>();
    
    // Currently playing audio
    private static readonly Dictionary<string, AudioSource> _activeSources 
        = new Dictionary<string, AudioSource>();
    
    // Cached audio clips
    private static readonly Dictionary<string, AudioClip> _clipCache 
        = new Dictionary<string, AudioClip>();
    
    // Mixer settings
    private static AudioMixer _audioMixer;
    private static readonly Dictionary<string, float> _volumeSettings 
        = new Dictionary<string, float>();
    
    #region Initialization
    
    /// <summary>
    /// Initialize the audio system
    /// </summary>
    public static void Initialize(AudioMixer mixer, int poolSize = 10)
    {
        _audioMixer = mixer;
        
        // Create initial pool of audio sources
        var root = new GameObject("AudioSourcePool").transform;
        UnityEngine.Object.DontDestroyOnLoad(root.gameObject);
        
        for (int i = 0; i < poolSize; i++)
        {
            CreatePooledAudioSource(root);
        }
    }
    
    private static AudioSource CreatePooledAudioSource(Transform parent)
    {
        var go = new GameObject("PooledAudioSource");
        go.transform.SetParent(parent);
        var source = go.AddComponent<AudioSource>();
        AddToPool("default", source);
        return source;
    }
    
    #endregion
    
    #region Sound Effects
    
    /// <summary>
    /// Play a sound effect
    /// </summary>
    public static AudioSource PlaySound(
        AudioClip clip,
        float volume = 1f,
        float pitch = 1f,
        bool loop = false,
        string mixerGroup = "SFX")
    {
        var source = GetAudioSource();
        if (source == null) return null;
        
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = loop;
        
        if (_audioMixer != null)
        {
            var group = _audioMixer.FindMatchingGroups(mixerGroup)[0];
            source.outputAudioMixerGroup = group;
        }
        
        source.Play();
        
        if (!loop)
        {
            ReturnToPoolAfterDelay(source, clip.length);
        }
        
        return source;
    }
    
    /// <summary>
    /// Play a sound effect at a world position
    /// </summary>
    public static AudioSource PlaySoundAtPosition(
        AudioClip clip,
        Vector3 position,
        float volume = 1f,
        float pitch = 1f,
        bool loop = false,
        string mixerGroup = "SFX")
    {
        var source = PlaySound(clip, volume, pitch, loop, mixerGroup);
        if (source != null)
        {
            source.transform.position = position;
            source.spatialBlend = 1f; // Full 3D
        }
        return source;
    }
    
    #endregion
    
    #region Music
    
    /// <summary>
    /// Play background music with optional crossfade
    /// </summary>
    public static async Task PlayMusic(
        AudioClip music,
        float fadeTime = 1f,
        float volume = 1f,
        string mixerGroup = "Music")
    {
        if (_activeSources.TryGetValue("music", out var currentMusic))
        {
            await FadeOut(currentMusic, fadeTime);
        }
        
        var source = PlaySound(music, 0f, 1f, true, mixerGroup);
        if (source != null)
        {
            _activeSources["music"] = source;
            await FadeIn(source, fadeTime, volume);
        }
    }
    
    /// <summary>
    /// Stop background music
    /// </summary>
    public static async Task StopMusic(float fadeTime = 1f)
    {
        if (_activeSources.TryGetValue("music", out var music))
        {
            await FadeOut(music, fadeTime);
            ReturnToPool("default", music);
            _activeSources.Remove("music");
        }
    }
    
    #endregion
    
    #region Mixer Control
    
    /// <summary>
    /// Set volume for a mixer group
    /// </summary>
    public static void SetVolume(string parameterName, float normalizedVolume)
    {
        if (_audioMixer == null) return;
        
        _volumeSettings[parameterName] = normalizedVolume;
        float db = normalizedVolume > 0 ? 
            Mathf.Log10(normalizedVolume) * 20 : 
            -80f;
        
        _audioMixer.SetFloat(parameterName, db);
    }
    
    /// <summary>
    /// Get volume for a mixer group
    /// </summary>
    public static float GetVolume(string parameterName)
    {
        return _volumeSettings.TryGetValue(parameterName, out float volume) ? 
            volume : 1f;
    }
    
    #endregion
    
    #region Pool Management
    
    private static AudioSource GetAudioSource()
    {
        if (!_audioSourcePools.TryGetValue("default", out var pool))
            return null;
            
        if (pool.Count == 0)
        {
            var parent = pool.Peek().transform.parent;
            return CreatePooledAudioSource(parent);
        }
        
        return pool.Dequeue();
    }
    
    private static void AddToPool(string poolName, AudioSource source)
    {
        if (!_audioSourcePools.ContainsKey(poolName))
        {
            _audioSourcePools[poolName] = new Queue<AudioSource>();
        }
        
        source.gameObject.SetActive(false);
        _audioSourcePools[poolName].Enqueue(source);
    }
    
    private static void ReturnToPool(string poolName, AudioSource source)
    {
        source.Stop();
        source.clip = null;
        AddToPool(poolName, source);
    }
    
    private static async void ReturnToPoolAfterDelay(AudioSource source, float delay)
    {
        await Task.Delay((int)(delay * 1000));
        if (source != null)
        {
            ReturnToPool("default", source);
        }
    }
    
    #endregion
    
    #region Utilities
    
    private static async Task FadeOut(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float time = 0;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            await Task.Yield();
        }
        
        source.volume = 0f;
    }
    
    private static async Task FadeIn(AudioSource source, float duration, float targetVolume)
    {
        float time = 0;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, time / duration);
            await Task.Yield();
        }
        
        source.volume = targetVolume;
    }
    
    #endregion
}
