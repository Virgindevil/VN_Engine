using System.Collections;
using UnityEngine;

namespace VNKit
{
    /// <summary>BGM with crossfade, a small SFX pool, and a single voice channel.</summary>
    public class VNAudioManager
    {
        public string CurrentBgm { get; private set; }

        readonly VisualNovelEngine engine;
        readonly VNRunner runner;
        readonly AudioSource bgmA;
        readonly AudioSource bgmB;
        readonly AudioSource voice;
        readonly AudioSource[] sfxPool;
        bool aActive = true;
        Coroutine bgmFade;

        public VNAudioManager(Transform parent, VisualNovelEngine engine)
        {
            this.engine = engine;
            runner = VNRunner.Create("VNKit.Audio", parent);
            bgmA = NewSource("BGM.A", true);
            bgmB = NewSource("BGM.B", true);
            voice = NewSource("Voice", false);
            sfxPool = new AudioSource[4];
            for (int i = 0; i < sfxPool.Length; i++) sfxPool[i] = NewSource("SFX." + i, false);
            ApplyVolumes();
        }

        AudioSource NewSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(runner.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        float BgmVol { get { return engine.Settings.bgmVolume * engine.Settings.masterVolume; } }
        float SfxVol { get { return engine.Settings.sfxVolume * engine.Settings.masterVolume; } }
        float VoiceVol { get { return engine.Settings.voiceVolume * engine.Settings.masterVolume; } }

        // ---------------- BGM ----------------

        public void PlayBgm(string name, AudioClip clip, float fade)
        {
            if (clip == null)
            {
                VNLog.Warn("BGM '" + name + "' could not be played (missing clip).");
                return;
            }
            if (name == CurrentBgm && ActiveBgm().isPlaying) return;

            CurrentBgm = name;
            AudioSource next = aActive ? bgmB : bgmA;
            AudioSource prev = aActive ? bgmA : bgmB;
            aActive = !aActive;

            next.clip = clip;
            next.volume = 0f;
            next.Play();

            StopBgmFade();
            bgmFade = runner.StartCoroutine(BgmFadeRoutine(prev, next, fade));
        }

        public void StopBgm(float fade)
        {
            CurrentBgm = null;
            AudioSource cur = ActiveBgm();
            if (!cur.isPlaying) return;
            StopBgmFade();
            bgmFade = runner.StartCoroutine(FadeOutStop(cur, fade));
        }

        AudioSource ActiveBgm() { return aActive ? bgmA : bgmB; }

        IEnumerator BgmFadeRoutine(AudioSource prev, AudioSource next, float fade)
        {
            float target = BgmVol;
            if (fade <= 0.05f)
            {
                prev.Stop();
                next.volume = target;
                yield break;
            }
            float t = 0f;
            float prevStart = prev.volume;
            while (t < fade)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fade);
                prev.volume = Mathf.Lerp(prevStart, 0f, k);
                next.volume = Mathf.Lerp(0f, target, k);
                yield return null;
            }
            prev.Stop();
            prev.volume = 0f;
            next.volume = target;
            bgmFade = null;
        }

        IEnumerator FadeOutStop(AudioSource src, float fade)
        {
            float start = src.volume;
            float t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / fade));
                yield return null;
            }
            src.Stop();
            src.volume = 0f;
            bgmFade = null;
        }

        void StopBgmFade()
        {
            if (bgmFade != null) runner.StopCoroutine(bgmFade);
            bgmFade = null;
        }

        // ---------------- SFX / Voice ----------------

        public void PlaySfx(AudioClip clip, float vol)
        {
            if (clip == null) return;
            AudioSource s = sfxPool[0];
            for (int i = 0; i < sfxPool.Length; i++)
            {
                if (!sfxPool[i].isPlaying) { s = sfxPool[i]; break; }
            }
            s.PlayOneShot(clip, vol * SfxVol);
        }

        public void PlayVoice(AudioClip clip)
        {
            voice.Stop();
            if (clip == null) return;
            voice.clip = clip;
            voice.volume = VoiceVol;
            voice.Play();
        }

        public void StopVoice()
        {
            voice.Stop();
        }

        public void ApplyVolumes()
        {
            if (voice != null) voice.volume = VoiceVol;
            var cur = ActiveBgm();
            if (cur != null && cur.isPlaying) cur.volume = BgmVol;
        }
    }
}
