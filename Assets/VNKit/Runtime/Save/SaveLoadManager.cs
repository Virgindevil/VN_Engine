using System.Collections;
using System.IO;
using UnityEngine;

namespace VNKit
{
    /// <summary>
    /// JSON save files + PNG thumbnails under Application.persistentDataPath/VNSaves.
    /// 12 slots by default.
    /// </summary>
    public class SaveLoadManager
    {
        public const int SlotCount = 12;

        string Dir { get { return Path.Combine(Application.persistentDataPath, "VNSaves"); } }
        static string SlotName(int slot) { return "slot" + slot.ToString("D2"); }
        string SlotPath(int slot) { return Path.Combine(Dir, SlotName(slot) + ".json"); }
        string ThumbPath(int slot) { return Path.Combine(Dir, SlotName(slot) + ".png"); }

        public void Save(int slot, VNSaveData data)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        }

        public VNSaveData Load(int slot)
        {
            string p = SlotPath(slot);
            if (!File.Exists(p)) return null;
            try
            {
                return JsonUtility.FromJson<VNSaveData>(File.ReadAllText(p));
            }
            catch (System.Exception e)
            {
                VNLog.Warn("Failed to read save slot " + slot + ": " + e.Message);
                return null;
            }
        }

        public bool HasSave(int slot)
        {
            return File.Exists(SlotPath(slot));
        }

        public void Delete(int slot)
        {
            if (File.Exists(SlotPath(slot))) File.Delete(SlotPath(slot));
            if (File.Exists(ThumbPath(slot))) File.Delete(ThumbPath(slot));
        }

        public Texture2D LoadThumbnail(int slot)
        {
            string p = ThumbPath(slot);
            if (!File.Exists(p)) return null;
            try
            {
                var bytes = File.ReadAllBytes(p);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(bytes)) return t;
                Object.Destroy(t);
            }
            catch (System.Exception) { /* fall through */ }
            return null;
        }

        /// <summary>Hides the given UI for one frame, captures the screen, restores the UI.</summary>
        public void CaptureThumbnail(MonoBehaviour host, GameObject uiToHide, int slot)
        {
            host.StartCoroutine(CaptureRoutine(uiToHide, slot));
        }

        IEnumerator CaptureRoutine(GameObject uiToHide, int slot)
        {
            bool wasActive = uiToHide != null && uiToHide.activeSelf;
            if (wasActive) uiToHide.SetActive(false);

            yield return new WaitForEndOfFrame();

            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex != null)
                {
                    var png = tex.EncodeToPNG();
                    Directory.CreateDirectory(Dir);
                    File.WriteAllBytes(ThumbPath(slot), png);
                    Object.Destroy(tex);
                }
            }
            catch (System.Exception e)
            {
                VNLog.Warn("Thumbnail capture failed: " + e.Message);
            }

            if (wasActive && uiToHide != null) uiToHide.SetActive(true);
        }
    }
}
