using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>Two crossfading background layers over a black base. Images are "cover"-fitted to 1920x1080.</summary>
    public class BackgroundManager
    {
        public string CurrentName { get; private set; }

        readonly VisualNovelEngine engine;
        readonly VNRunner runner;
        readonly Image imgA;
        readonly Image imgB;
        readonly CanvasGroup groupA;
        readonly CanvasGroup groupB;
        bool aOnTop = true;
        Coroutine fade;

        const float RefW = 1920f;
        const float RefH = 1080f;

        public BackgroundManager(Transform stageRoot, VisualNovelEngine engine)
        {
            this.engine = engine;
            runner = VNRunner.Create("VNKit.Backgrounds", stageRoot);

            var baseImg = UIFactory.Image(runner.transform, "Base", Color.black);
            UIFactory.Stretch((RectTransform)baseImg.transform);

            imgA = CreateLayer("BG.A", out groupA);
            imgB = CreateLayer("BG.B", out groupB);
        }

        Image CreateLayer(string name, out CanvasGroup g)
        {
            var rt = UIFactory.Rect(name, runner.transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RefW, RefH);

            var img = UIFactory.Image(rt, "Image", Color.white);
            UIFactory.Stretch((RectTransform)img.transform);
            img.enabled = false;

            g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
            return img;
        }

        public void Set(string name, Sprite sprite, float time)
        {
            CurrentName = name;
            Image top = aOnTop ? imgB : imgA;
            CanvasGroup topG = aOnTop ? groupB : groupA;
            CanvasGroup botG = aOnTop ? groupA : groupB;
            aOnTop = !aOnTop;

            Fit(top, sprite);
            top.sprite = sprite;
            top.enabled = sprite != null;

            StopFade();
            fade = runner.StartCoroutine(CrossFade(topG, botG, time));
        }

        public void Clear(float time)
        {
            CurrentName = null;
            StopFade();
            fade = runner.StartCoroutine(FadeBoth(time));
        }

        /// <summary>Instant restore from a save file.</summary>
        public void Restore(string name)
        {
            StopFade();
            if (string.IsNullOrEmpty(name))
            {
                groupA.alpha = 0f; groupB.alpha = 0f;
                imgA.enabled = false; imgB.enabled = false;
                CurrentName = null;
                return;
            }

            var sprite = engine.LoadBackground(name);
            Image top = aOnTop ? imgB : imgA;
            CanvasGroup topG = aOnTop ? groupB : groupA;
            CanvasGroup botG = aOnTop ? groupA : groupB;
            aOnTop = !aOnTop;

            Fit(top, sprite);
            top.sprite = sprite;
            top.enabled = sprite != null;
            topG.alpha = 1f;
            botG.alpha = 0f;
            CurrentName = name;
        }

        void Fit(Image img, Sprite s)
        {
            if (s == null) return;
            var rt = (RectTransform)img.transform.parent;
            var r = s.rect;
            float scale = Mathf.Max(RefW / r.width, RefH / r.height);
            rt.sizeDelta = new Vector2(r.width * scale, r.height * scale);
        }

        IEnumerator CrossFade(CanvasGroup top, CanvasGroup bottom, float time)
        {
            if (time <= 0.02f) { top.alpha = 1f; bottom.alpha = 0f; yield break; }
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / time);
                top.alpha = k;
                bottom.alpha = 1f - k;
                yield return null;
            }
            top.alpha = 1f;
            bottom.alpha = 0f;
            fade = null;
        }

        IEnumerator FadeBoth(float time)
        {
            float a0 = groupA.alpha, b0 = groupB.alpha;
            if (time <= 0.02f) { groupA.alpha = 0f; groupB.alpha = 0f; yield break; }
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / time);
                groupA.alpha = a0 * k;
                groupB.alpha = b0 * k;
                yield return null;
            }
            groupA.alpha = 0f;
            groupB.alpha = 0f;
            fade = null;
        }

        void StopFade()
        {
            if (fade != null) runner.StopCoroutine(fade);
            fade = null;
        }
    }
}
