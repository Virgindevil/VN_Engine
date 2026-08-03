using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VNKit
{
    /// <summary>
    /// Skyrim-style lockpicking mini-game. Usage in a script:
    ///   @minigame Lockpick difficulty:2 picks:3 var:lockResult
    /// Move the pick with A/D (or the mouse), hold SPACE to turn the lock.
    /// Close to the sweet spot the cylinder turns freely; elsewhere the pick strains
    /// and eventually breaks. Result lands in the variable named by "var" (1/0).
    /// Esc cancels (counts as failure).
    /// </summary>
    public class LockpickMinigame : VNMinigame
    {
        RectTransform cylinderRT;
        RectTransform pickRT;
        Image pickImg;
        TextMeshProUGUI hintText;
        TextMeshProUGUI picksText;
        TextMeshProUGUI resultText;

        float pickAngle;          // -80..80 degrees
        float sweetSpot;          // hidden target angle
        float tolerance;          // degrees of leeway
        float turnProgress;       // 0..1
        float hp = 1f;            // current pick durability
        int picksLeft;
        float resultTimer = -1f;
        bool resultSuccess;
        float time;
        float lastMouseX = -1f;

        static Sprite ringSprite;
        static Sprite cylinderSprite;

        public override void Start(VNMinigameContext context)
        {
            base.Start(context);

            float difficulty = context.command.GetFloat("difficulty", 1f);
            tolerance = Mathf.Max(5f, 20f - difficulty * 5f);
            picksLeft = Mathf.Max(1, (int)context.command.GetFloat("picks", 3f));
            sweetSpot = Random.Range(-60f, 60f);

            // Dim overlay (scene stays visible behind the lock)
            var dim = UIFactory.AddImage(root, new Color(0f, 0f, 0f, 0.55f));
            dim.raycastTarget = true;

            // Lock visuals
            EnsureSprites();
            var center = new Vector2(0.5f, 0.55f);

            var ring = UIFactory.Image(root.transform, "Ring", Color.white);
            ring.sprite = ringSprite;
            var rrt = (RectTransform)ring.transform;
            rrt.anchorMin = rrt.anchorMax = center;
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(512f, 512f);
            rrt.anchoredPosition = Vector2.zero;
            ring.raycastTarget = false;

            var cyl = UIFactory.Image(root.transform, "Cylinder", Color.white);
            cyl.sprite = cylinderSprite;
            cylinderRT = (RectTransform)cyl.transform;
            cylinderRT.anchorMin = cylinderRT.anchorMax = center;
            cylinderRT.pivot = new Vector2(0.5f, 0.5f);
            cylinderRT.sizeDelta = new Vector2(400f, 400f);
            cylinderRT.anchoredPosition = Vector2.zero;
            cyl.raycastTarget = false;

            var pick = UIFactory.Image(root.transform, "Pick", new Color(0.85f, 0.85f, 0.9f, 1f));
            pickImg = pick;
            pick.sprite = UIFactory.UISprite;
            pick.type = UnityEngine.UI.Image.Type.Sliced;
            pickRT = (RectTransform)pick.transform;
            pickRT.anchorMin = pickRT.anchorMax = center;
            pickRT.pivot = new Vector2(0.5f, 0f);   // rotates around the lock center
            pickRT.sizeDelta = new Vector2(10f, 300f);
            pickRT.anchoredPosition = new Vector2(0f, 20f);
            pick.raycastTarget = false;

            hintText = UIFactory.Text(root.transform, "Hint", VNLoc.T("minigame.lockpick.hint"), 24,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            var hrt = (RectTransform)hintText.transform;
            hrt.anchorMin = new Vector2(0.1f, 0.10f);
            hrt.anchorMax = new Vector2(0.9f, 0.16f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            picksText = UIFactory.Text(root.transform, "Picks", "", 24,
                TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.9f));
            var prt = (RectTransform)picksText.transform;
            prt.anchorMin = new Vector2(0.04f, 0.90f);
            prt.anchorMax = new Vector2(0.4f, 0.96f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            RefreshPicksText();

            resultText = UIFactory.Text(root.transform, "Result", "", 52,
                TextAnchor.MiddleCenter, UIFactory.AccentColor);
            var resrt = (RectTransform)resultText.transform;
            resrt.anchorMin = new Vector2(0.2f, 0.22f);
            resrt.anchorMax = new Vector2(0.8f, 0.34f);
            resrt.offsetMin = Vector2.zero;
            resrt.offsetMax = Vector2.zero;
            resultText.fontStyle = FontStyles.Bold;
        }

        void RefreshPicksText()
        {
            if (picksText != null) picksText.text = VNLoc.T("minigame.picks") + ": " + picksLeft;
        }

        public override void Tick(float dt)
        {
            if (done) return;
            time += dt;

            // Result pause before completing
            if (resultTimer >= 0f)
            {
                resultTimer -= dt;
                if (resultTimer < 0f) Complete(resultSuccess, resultSuccess ? "1" : "0");
                return;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            // --- Pick movement: keys or mouse ---
            float key = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) key -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) key += 1f;
            pickAngle = Mathf.Clamp(pickAngle + key * 90f * dt, -80f, 80f);

            float mx = Input.mousePosition.x / Mathf.Max(1, Screen.width);
            if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.01f) lastMouseX = mx;
            if (lastMouseX >= 0f && Mathf.Abs(mx - lastMouseX) > 0.002f) lastMouseX = mx;
            if (lastMouseX >= 0f && key == 0f)
                pickAngle = Mathf.Lerp(pickAngle, Mathf.Clamp((lastMouseX - 0.5f) * 2f * 90f, -80f, 80f), 10f * dt);

            bool turning = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.E);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                resultSuccess = false;
                resultTimer = 0.01f; // instant cancel
                return;
            }
#else
            bool turning = false;
#endif

            // --- Turning / strain ---
            float dist = Mathf.Abs(pickAngle - sweetSpot);
            float closeness = Mathf.Clamp01(1f - dist / (tolerance * 2.5f));
            float displayAngle = pickAngle;

            if (turning)
            {
                float factor = Mathf.Clamp01(closeness * 1.5f);
                turnProgress = Mathf.Clamp01(turnProgress + dt * factor / 1.1f);

                if (factor < 0.5f)
                {
                    hp -= dt * (0.55f - factor) * 1.6f;
                    displayAngle += Mathf.Sin(time * 55f) * (0.55f - factor) * 3f; // strain wobble
                    pickImg.color = Color.Lerp(new Color(0.85f, 0.85f, 0.9f, 1f), Color.red, 1f - hp);
                }

                if (hp <= 0f)
                {
                    picksLeft--;
                    RefreshPicksText();
                    if (picksLeft <= 0)
                    {
                        resultSuccess = false;
                        resultTimer = 1.2f;
                        resultText.text = VNLoc.T("minigame.lockpick.fail");
                        return;
                    }
                    hp = 1f;
                    pickImg.color = new Color(0.85f, 0.85f, 0.9f, 1f);
                }

                if (turnProgress >= 1f)
                {
                    resultSuccess = true;
                    resultTimer = 1.0f;
                    resultText.text = VNLoc.T("minigame.lockpick.success");
                    return;
                }
            }
            else
            {
                turnProgress = Mathf.Max(0f, turnProgress - dt * 0.8f); // lock settles back
                pickImg.color = Color.Lerp(pickImg.color, new Color(0.85f, 0.85f, 0.9f, 1f), 5f * dt);
            }

            pickRT.localEulerAngles = new Vector3(0f, 0f, -displayAngle);
            cylinderRT.localEulerAngles = new Vector3(0f, 0f, -turnProgress * 90f);
        }

        // ---------------- procedural sprites ----------------

        static void EnsureSprites()
        {
            if (ringSprite == null) ringSprite = MakeRingSprite(512, 230f, 26f);
            if (cylinderSprite == null) cylinderSprite = MakeCylinderSprite(400, 180f);
        }

        static Sprite MakeRingSprite(int size, float radius, float thickness)
        {
            var tex = NewTex(size, "VNKit.Lock.Ring");
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(d - radius));
                    px[y * size + x] = new Color32(200, 200, 210, (byte)(a / (thickness * 0.5f) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeCylinderSprite(int size, float radius)
        {
            var tex = NewTex(size, "VNKit.Lock.Cylinder");
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radius) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    byte shade = (byte)(60 + 40 * (1f - d / radius));
                    var col = new Color32(shade, shade, (byte)(shade + 10), 235);
                    // bright notch at the top so rotation is visible
                    if (dy > radius * 0.55f && Mathf.Abs(dx) < 10f) col = new Color32(230, 200, 120, 255);
                    px[y * size + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Texture2D NewTex(int size, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }
}
