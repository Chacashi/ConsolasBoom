using UnityEngine;
using UnityEngine.Audio;
using System;

namespace HouseEscape.Audio
{
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string musicBus = "Music";
        [SerializeField] private string sfxBus = "SFX";
        [SerializeField] private string uiBus = "UI";

        [Header("Music")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip ambientMusicLow;
        [SerializeField] private AudioClip ambientMusicHigh;
        [SerializeField] private float musicFadeTime = 2f;
        [SerializeField] private float musicVolume = 0.5f;

        [Header("3D Sounds")]
        [SerializeField] private float monsterFootstepVolume = 0.8f;
        [SerializeField] private float generatorVolume = 0.6f;
        [SerializeField] private float itemPickupVolume = 0.7f;

        [Header("One-Shot Sounds")]
        [SerializeField] private AudioClip damageSound;
        [SerializeField] private AudioClip healSound;
        [SerializeField] private AudioClip itemPickupSound;
        [SerializeField] private AudioClip monsterScream;
        [SerializeField] private AudioClip lightsChangeSound;
        [SerializeField] private AudioClip generatorActivateSound;

        private static AudioManager _instance;
        private float _currentMusicVolume;
        private bool _isFadingMusic;
        private float _fadeTarget;

        public static AudioManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null)
                musicSource = gameObject.AddComponent<AudioSource>();

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;
            _currentMusicVolume = musicVolume;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null) return;

            if (_isFadingMusic)
                StopCoroutine(FadeMusic(0f));

            _isFadingMusic = true;
            _fadeTarget = 0f;
            StartCoroutine(FadeMusic(0f));

            musicSource.clip = clip;
            musicSource.Play();

            _fadeTarget = musicVolume;
            StartCoroutine(FadeMusic(musicVolume));
        }

        public void StopMusic()
        {
            if (musicSource == null) return;

            _isFadingMusic = true;
            _fadeTarget = 0f;
            StartCoroutine(FadeMusic(0f));
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null && !_isFadingMusic)
                musicSource.volume = musicVolume;
        }

        public void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, volume);
        }

        public void PlayDamageSound(Vector3 position)
        {
            PlayOneShot(damageSound, position);
        }

        public void PlayHealSound(Vector3 position)
        {
            PlayOneShot(healSound, position);
        }

        public void PlayItemPickupSound(Vector3 position)
        {
            PlayOneShot(itemPickupSound, position, itemPickupVolume);
        }

        public void PlayMonsterScream(Vector3 position)
        {
            PlayOneShot(monsterScream, position, 1.5f);
        }

        public void PlayLightsChangeSound(Vector3 position)
        {
            PlayOneShot(lightsChangeSound, position, 0.8f);
        }

        public void PlayGeneratorActivateSound(Vector3 position)
        {
            PlayOneShot(generatorActivateSound, position, generatorVolume);
        }

        public void SetMixerVolume(string busName, float volume)
        {
            if (audioMixer == null) return;

            audioMixer.SetFloat(busName + "_Volume", Mathf.Log10(Mathf.Clamp01(volume)));
        }

        public void SetMixerMute(string busName, bool mute)
        {
            if (audioMixer == null) return;

            audioMixer.SetFloat(busName + "_Mute", mute ? 1f : 0f);
        }

        private System.Collections.IEnumerator FadeMusic(float targetVolume)
        {
            float startVolume = musicSource != null ? musicSource.volume : 0f;
            float elapsed = 0f;

            while (elapsed < musicFadeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / musicFadeTime);
                float volume = Mathf.Lerp(startVolume, targetVolume, t);

                if (musicSource != null)
                    musicSource.volume = volume;

                yield return null;
            }

            if (musicSource != null)
                musicSource.volume = targetVolume;

            _isFadingMusic = false;
            _currentMusicVolume = targetVolume;
        }
    }
}
