using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>
    /// Full-screen event CG layer (Naninovel-style @cg). Sits on the stage above
    /// backgrounds and characters, under the dialogue UI. A CG can be a static sprite
    /// or an animated Spine skeleton (see VisualNovelEngine.spineCgs); switching CGs
    /// crossfades smoothly, so animated illustrations change with the text.
    /// </summary>
    public class CgManager
    {
        public string CurrentName { get; private set; }

        readonly VisualNovelEngine engine;
        readonly VNRunner runner;
        readonly RectTransform layer;
        readonly Image image;
        readonly CanvasGroup group;
        VNSpineActor spine;
        Coroutine fade;

        const float RefW = 1920f;
        const float RefH = 1080f;

        public CgManager(Transform stageRoot, VisualNovelEngine engine)
        {
            this.engine = engine;
            runner = VNRunner.Create("VNKit.CG", stageRoot);

            layer = UIFactory.Rect("Layer", runner.transform);
            layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
            layer.pivot = new Vector2(0.5f, 0.5f);
            layer.sizeDelta = new Vector2(RefW, RefH);

            image = UIFactory.Image(layer, "Image", Color.white);
            UIFactory.Stretch((RectTransform)image.transform);
            image.enabled = false;

            group = layer.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        /// <summary>Show a CG (sprite and/or spine skeleton ready-loaded). Fades in over 'time'.</summary>
        public void Show(string name, Sprite sprite, Object spineData, VNSpineCgEntry spineCfg, float time)
        {
            CurrentName = name;
            ClearSpine();

            if (spineCfg != null && spineData != null)
            {
                image.enabled = false;
                image.sprite = null;
                spine = VNSpineActor.Create(layer, "Spine", spineData, spineCfg.animation, spineCfg.loop, 1f);
            }
            else
            {
                Fit(sprite);
                image.sprite = sprite;
                image.enabled = sprite != null;
            }

            StartFade(1f, time);
        }

        public void Hide(float time)
        {
            CurrentName = null;
            StartFade(0f, time);
            // Spine cleanup after the fade completes (or immediately when instant).
            if (time <= 0.02f) ClearSpine();
            else runner.StartCoroutine(ClearSpineAfter(time));
        }

        IEnumerator ClearSpineAfter(float delay)
        {
            float t = 0f;
            while (t < delay) { t += Time.deltaTime; yield return null; }
            ClearSpine();
            image.enabled = false;
            image.sprite = null;
        }

        void StartFade(float target, float time)
        {
            if (fade != null) runner.StopCoroutine(fade);
            fade = runner.StartCoroutine(FadeRoutine(target, time));
        }

        IEnumerator FadeRoutine(float target, float time)
        {
            float from = group.alpha;
            if (time <= 0.02f) { group.alpha = target; fade = null; yield break; }
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / time));
                yield return null;
            }
            group.alpha = target;
            fade = null;
        }

        void Fit(Sprite s)
        {
            if (s == null) return;
            var r = s.rect;
            float scale = Mathf.Max(RefW / r.width, RefH / r.height);
            layer.sizeDelta = new Vector2(r.width * scale, r.height * scale);
        }

        void ClearSpine()
        {
            if (spine != null) Object.Destroy(spine.gameObject);
            spine = null;
        }
    }
}
