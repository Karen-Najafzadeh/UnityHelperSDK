using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace UnityHelperSDK.HelperUtilities{


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
        private static readonly Dictionary<string, Queue<AudioSource>> _audioSourcePools 
            = new Dictionary<string, Queue<AudioSource>>();
        private static readonly Dictionary<string, AudioSource> _activeSources 
            = new Dictionary<string, AudioSource>();
        private static readonly Dictionary<string, AudioClip> _clipCache 
            = new Dictionary<string, AudioClip>();
        private static AudioMixer _audioMixer;
        private static readonly Dictionary<string, float> _volumeSettings 
            = new Dictionary<string, float>();
        
        #region Initialization
        
        /// <summary>
        /// Initializes the audio system with the specified mixer
        /// </summary>
        public static void Initialize(AudioMixer mixer)
        {
            _audioMixer = mixer;
            LoadSavedVolumes();
        }
        
        #endregion
        
        #region Sound Effects
        
        /// <summary>
        /// Plays a sound effect with optional 3D positioning
        /// </summary>
        public static AudioSource PlaySound(AudioClip clip, string sourceId = null, bool loop = false, 
            Vector3? position = null, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return null;

            AudioSource source = GetAudioSource(sourceId);
            if (source == null) return null;

            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.pitch = pitch;

            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f; // Full 3D
            }
            else
            {
                source.spatialBlend = 0f; // 2D
            }

            source.Play();
            return source;
        }

        /// <summary>
        /// Plays a random sound from a collection
        /// </summary>
        public static AudioSource PlayRandomSound(AudioClip[] clips, string sourceId = null, 
            Vector3? position = null, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return null;

            int index = UnityEngine.Random.Range(0, clips.Length);
            return PlaySound(clips[index], sourceId, false, position, volume);
        }
        
        #endregion
        
        #region Music
        
        /// <summary>
        /// Starts playing background music with optional crossfade
        /// </summary>
        public static async Task PlayMusic(AudioClip music, float fadeInDuration = 1f, float fadeOutDuration = 1f)
        {
            if (music == null) return;

            AudioSource newSource = GetAudioSource("Music_New");
            AudioSource oldSource = null;

            if (_activeSources.TryGetValue("Music_Current", out oldSource))
            {
                // Start fade out of current music
                float startVolume = oldSource.volume;
                float elapsed = 0f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    oldSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
                    await Task.Yield();
                }

                ReturnAudioSource("Music_Current");
            }

            // Setup and start new music
            newSource.clip = music;
            newSource.loop = true;
            newSource.volume = 0f;
            newSource.spatialBlend = 0f;
            newSource.Play();

            // Fade in new music
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeInDuration)
            {
                fadeElapsed += Time.deltaTime;
                newSource.volume = Mathf.Lerp(0f, 1f, fadeElapsed / fadeInDuration);
                await Task.Yield();
            }

            _activeSources["Music_Current"] = newSource;
        }
        
        #endregion
        
        #region Mixer Control
        
        /// <summary>
        /// Sets the volume of a mixer group
        /// </summary>
        public static void SetVolume(string parameterName, float normalizedVolume)
        {
            if (_audioMixer == null) return;

            float dbValue = normalizedVolume > 0f ? 
                Mathf.Log10(normalizedVolume) * 20f : 
                -80f;

            _audioMixer.SetFloat(parameterName, dbValue);
            _volumeSettings[parameterName] = normalizedVolume;
            SaveVolumes();
        }

        /// <summary>
        /// Gets the current volume of a mixer group
        /// </summary>
        public static float GetVolume(string parameterName)
        {
            if (_volumeSettings.TryGetValue(parameterName, out float volume))
                return volume;
            return 1f;
        }
        
        #endregion
        
        #region Pool Management
        
        private static AudioSource GetAudioSource(string sourceId = null)
        {
            // If sourceId is provided and already exists, return that source
            if (!string.IsNullOrEmpty(sourceId) && _activeSources.TryGetValue(sourceId, out AudioSource existingSource))
            {
                return existingSource;
            }

            // Get or create a pooled source
            AudioSource source = GetPooledAudioSource();
            
            if (!string.IsNullOrEmpty(sourceId))
            {
                _activeSources[sourceId] = source;
            }

            return source;
        }

        private static AudioSource GetPooledAudioSource()
        {
            const string POOL_KEY = "DefaultPool";
            Queue<AudioSource> pool;

            if (!_audioSourcePools.TryGetValue(POOL_KEY, out pool))
            {
                pool = new Queue<AudioSource>();
                _audioSourcePools[POOL_KEY] = pool;
            }

            AudioSource source;
            if (pool.Count > 0)
            {
                source = pool.Dequeue();
            }
            else
            {
                // Create new GameObject with AudioSource
                GameObject obj = new GameObject("AudioSource");
                source = obj.AddComponent<AudioSource>();
                GameObject.DontDestroyOnLoad(obj);
            }

            return source;
        }

        private static void ReturnAudioSource(string sourceId)
        {
            if (_activeSources.TryGetValue(sourceId, out AudioSource source))
            {
                source.Stop();
                source.clip = null;
                _activeSources.Remove(sourceId);

                // Return to pool
                const string POOL_KEY = "DefaultPool";
                if (!_audioSourcePools.ContainsKey(POOL_KEY))
                {
                    _audioSourcePools[POOL_KEY] = new Queue<AudioSource>();
                }
                _audioSourcePools[POOL_KEY].Enqueue(source);
            }
        }
        
        #endregion
        
        #region Utilities
        
        private static void LoadSavedVolumes()
        {
            // Load saved volume settings from PlayerPrefs
            string[] mixerParams = { "MasterVolume", "MusicVolume", "SFXVolume", "UIVolume" };
            foreach (string param in mixerParams)
            {
                float savedVolume = PlayerPrefs.GetFloat($"Audio_{param}", 1f);
                SetVolume(param, savedVolume);
            }
        }

        private static void SaveVolumes()
        {
            foreach (var kvp in _volumeSettings)
            {
                PlayerPrefs.SetFloat($"Audio_{kvp.Key}", kvp.Value);
            }
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Fades the volume of an audio source over time
        /// </summary>
        public static async Task FadeVolume(AudioSource source, float targetVolume, float duration)
        {
            if (source == null) return;

            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                await Task.Yield();
            }

            source.volume = targetVolume;
        }

        /// <summary>
        /// Stops all active audio sources
        /// </summary>
        public static void StopAllSounds()
        {
            foreach (var source in _activeSources.Values)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
        }

        /// <summary>
        /// Pauses all active audio sources
        /// </summary>
        public static void PauseAllSounds()
        {
            foreach (var source in _activeSources.Values)
            {
                if (source != null && source.isPlaying)
                {
                    source.Pause();
                }
            }
        }

        /// <summary>
        /// Resumes all paused audio sources
        /// </summary>
        public static void ResumeAllSounds()
        {
            foreach (var source in _activeSources.Values)
            {
                if (source != null && !source.isPlaying)
                {
                    source.UnPause();
                }
            }
        }
        
        #endregion
    }
}