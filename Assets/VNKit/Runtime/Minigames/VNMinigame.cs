using System;
using System.Collections.Generic;
using UnityEngine;

namespace VNKit
{
    /// <summary>Context handed to a mini-game when it starts.</summary>
    public class VNMinigameContext
    {
        public Transform parent;             // full-screen overlay area to build UI into
        public VisualNovelEngine engine;
        public VNCommand command;            // the @minigame command (params: difficulty, picks, var, ...)
        public Action<bool, string> onComplete; // call exactly once: (success, valueForVariable)
    }

    /// <summary>
    /// Base class for mini-games that can be embedded in a .vns script via
    ///   @minigame Lockpick difficulty:2 var:lockResult
    /// Build your UI in Start(); update in Tick(); finish via ctx.onComplete.
    /// The script pauses (PlayerState.WaitingMinigame) until completion.
    /// </summary>
    public abstract class VNMinigame
    {
        protected VNMinigameContext ctx;
        protected GameObject root;
        protected bool done;

        public virtual void Start(VNMinigameContext context)
        {
            ctx = context;
            root = UIFactory.Rect("VNKit.Minigame." + GetType().Name, context.parent).gameObject;
            UIFactory.Stretch((RectTransform)root.transform);
        }

        /// <summary>Per-frame update (called by the engine while the game is active).</summary>
        public virtual void Tick(float dt) { }

        public virtual void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
        }

        protected void Complete(bool success, string value)
        {
            if (done) return;
            done = true;
            var cb = ctx.onComplete;
            if (cb != null) cb(success, value);
        }
    }

    /// <summary>
    /// Mini-game registry. "Lockpick" is built in. Register your own before the engine boots:
    ///   VNMinigames.Register("Fishing", () => new MyFishingGame());
    /// </summary>
    public static class VNMinigames
    {
        static readonly Dictionary<string, Func<VNMinigame>> factories =
            new Dictionary<string, Func<VNMinigame>>();

        static VNMinigames()
        {
            Register("Lockpick", () => new LockpickMinigame());
        }

        public static void Register(string id, Func<VNMinigame> factory)
        {
            if (!string.IsNullOrEmpty(id) && factory != null) factories[id] = factory;
        }

        public static VNMinigame Create(string id)
        {
            Func<VNMinigame> f;
            if (!string.IsNullOrEmpty(id) && factories.TryGetValue(id, out f)) return f();
            return null;
        }

        public static bool Exists(string id)
        {
            return !string.IsNullOrEmpty(id) && factories.ContainsKey(id);
        }

        /// <summary>2.12: ids of every registered mini-game (the phone Games tab).</summary>
        public static List<string> GetIds()
        {
            return new List<string>(factories.Keys);
        }
    }
}
