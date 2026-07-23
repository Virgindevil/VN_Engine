using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VNKit
{
    /// <summary>One on-stage character: a sprite anchored to a horizontal position, with fades and slides.</summary>
    public class CharacterActor
    {
        public string Name { get; private set; }
        public string Appearance { get; private set; }
        public float PosX { get; private set; }
        public bool Visible { get; private set; }
        public bool HasSprite { get { return image.sprite != null; } }

        readonly RectTransform root;
        readonly Image image;
        readonly CanvasGroup group;
        readonly VNRunner host;
        Coroutine anim;

        const float CharHeight = 940f; // in 1920x1080 reference space

        public CharacterActor(Transform parent, string name, VNRunner host)
        {
            Name = name;
            this.host = host;

            root = UIFactory.Rect("Char." + name, parent);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);

            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            image = UIFactory.Image(root, "Sprite", Color.white);
            UIFactory.Stretch((RectTransform)image.transform);
            image.preserveAspect = true;
            image.enabled = false;

            PosX = 0.5f;
        }

        public void SetSprite(Sprite s, string appearance)
        {
            Appearance = appearance;
            image.sprite = s;
            image.enabled = s != null;
            if (s != null)
            {
                var r = s.rect;
                root.sizeDelta = new Vector2(CharHeight * r.width / r.height, CharHeight);
            }
        }

        public void SetPosition(float x, float time)
        {
            StopAnim();
            if (time <= 0f || !Visible)
            {
                PosX = x;
                ApplyAnchor();
            }
            else anim = host.StartCoroutine(MoveRoutine(x, time));
        }

        void ApplyAnchor()
        {
            root.anchorMin = new Vector2(PosX, 0f);
            root.anchorMax = new Vector2(PosX, 0f);
            root.anchoredPosition = Vector2.zero;
        }

        IEnumerator MoveRoutine(float target, float time)
        {
            float from = PosX;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                PosX = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time)));
                ApplyAnchor();
                yield return null;
            }
            PosX = target;
            ApplyAnchor();
            anim = null;
        }

        public void Show(float time)
        {
            StopAnim();
            Visible = true;
            ApplyAnchor();
            if (time <= 0f) group.alpha = 1f;
            else anim = host.StartCoroutine(FadeRoutine(1f, time, null));
        }

        public void Hide(float time, System.Action onDone)
        {
            StopAnim();
            Visible = false;
            if (time <= 0f)
            {
                group.alpha = 0f;
                if (onDone != null) onDone();
            }
            else anim = host.StartCoroutine(FadeRoutine(0f, time, onDone));
        }

        IEnumerator FadeRoutine(float target, float time, System.Action onDone)
        {
            float from = group.alpha;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / time));
                yield return null;
            }
            group.alpha = target;
            anim = null;
            if (onDone != null) onDone();
        }

        public void DestroyActor()
        {
            StopAnim();
            if (root != null) Object.Destroy(root.gameObject);
        }

        void StopAnim()
        {
            if (anim != null && host != null) host.StopCoroutine(anim);
            anim = null;
        }
    }
}
