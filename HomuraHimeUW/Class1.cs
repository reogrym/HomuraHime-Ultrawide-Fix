using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UltrawideFixMod
{
    [BepInPlugin("himeuw", "Homura Hime Ultrawide Fix", "0.1.2")]
    public class UltrawidePlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> AutoResolution;
        public static ConfigEntry<int> ResWidth;
        public static ConfigEntry<int> ResHeight;

        private static Utage.AdvUguiManager[] cachedAdvManagers = new Utage.AdvUguiManager[0];
        private static UtageUguiMainGame[] cachedMainGames = new UtageUguiMainGame[0];

        // This list will now safely hold active AND hidden text boxes
        private static List<Utage.AdvUguiMessageWindow> allMsgWindows = new List<Utage.AdvUguiMessageWindow>();
        private static Dictionary<int, List<Transform>> textCaches = new Dictionary<int, List<Transform>>();

        private void Awake()
        {
            AutoResolution = Config.Bind("Resolution", "AutoDetect", true, "Set to true to use native resolution.");
            ResWidth = Config.Bind("Resolution", "ManualWidth", 3440, "Manual width");
            ResHeight = Config.Bind("Resolution", "ManualHeight", 1440, "Manual height");

            Harmony.CreateAndPatchAll(typeof(ResolutionPatches));
            Logger.LogInfo("Ultrawide Fix loaded successfully!");
        }

        private void Start()
        {
            StartCoroutine(ApplyResolutionDelayed());
            StartCoroutine(SlowSearchLoop());
            StartCoroutine(FastUpdateLoop());
        }

        private System.Collections.IEnumerator ApplyResolutionDelayed()
        {
            yield return new WaitForSeconds(1f);
            if (AutoResolution.Value) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(ResWidth.Value, ResHeight.Value, FullScreenMode.FullScreenWindow);
        }

        // --- LOOP 1: THE SLOW SEARCHER (Now grabs hidden objects to prevent popping) ---
        private System.Collections.IEnumerator SlowSearchLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                float targetAspect = 1.7777778f;

                // 1. HUD Pillarbox
                CanvasScaler[] scalers = FindObjectsOfType<CanvasScaler>();
                foreach (CanvasScaler scaler in scalers)
                {
                    if (scaler.matchWidthOrHeight != 1f) scaler.matchWidthOrHeight = 1f;

                    if (scaler.GetComponent<Canvas>().isRootCanvas)
                    {
                        foreach (Transform childTransform in scaler.transform)
                        {
                            RectTransform child = childTransform as RectTransform;
                            if (child == null) continue;

                            string name = child.name.ToLower();
                            if (name.Contains("utage") || name.Contains("fade") || name.Contains("bg") || name.Contains("background") || name.Contains("mask")) continue;

                            if (child.anchorMin == Vector2.zero && child.anchorMax == Vector2.one)
                            {
                                AspectRatioFitter fitter = child.GetComponent<AspectRatioFitter>();
                                if (fitter == null)
                                {
                                    fitter = child.gameObject.AddComponent<AspectRatioFitter>();
                                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                                    fitter.aspectRatio = targetAspect;
                                }
                            }
                        }
                    }
                }

                // 2. Refresh the root UI managers
                cachedAdvManagers = FindObjectsOfType<Utage.AdvUguiManager>();
                cachedMainGames = FindObjectsOfType<UtageUguiMainGame>();

                // 3. Find ALL text boxes safely (the 'true' includes hidden ones waiting to pop in!)
                allMsgWindows.Clear();
                foreach (var manager in cachedAdvManagers)
                {
                    if (manager != null) allMsgWindows.AddRange(manager.GetComponentsInChildren<Utage.AdvUguiMessageWindow>(true));
                }
                foreach (var mainGame in cachedMainGames)
                {
                    if (mainGame != null) allMsgWindows.AddRange(mainGame.GetComponentsInChildren<Utage.AdvUguiMessageWindow>(true));
                }

                // 4. Cache the fonts for the Inverse Scale
                foreach (var window in allMsgWindows)
                {
                    if (window == null) continue;
                    int id = window.GetInstanceID();
                    if (!textCaches.ContainsKey(id))
                    {
                        List<Transform> foundTexts = new List<Transform>();
                        foreach (var graphic in window.GetComponentsInChildren<Graphic>(true))
                        {
                            string typeName = graphic.GetType().Name;
                            if (typeName.Contains("Text") || typeName.Contains("TextMeshPro"))
                            {
                                foundTexts.Add(graphic.transform);
                            }
                        }
                        textCaches[id] = foundTexts;
                    }
                }

                yield return wait;
            }
        }

        // --- LOOP 2: THE FAST UPDATER ---
        private System.Collections.IEnumerator FastUpdateLoop()
        {
            while (true)
            {
                float targetAspect = 1.7777778f;
                float currentAspect = (float)Screen.width / (float)Screen.height;
                float scaleFactor = targetAspect / currentAspect;
                float inverseScale = 1f / scaleFactor;

                // 1. Smoothly fix Text Boxes and Cut-Ins
                foreach (var window in allMsgWindows)
                {
                    if (window != null)
                    {
                        Transform msgT = window.transform;

                        // Links the width scale to whatever the game is doing with the height scale
                        float currentY = msgT.localScale.y;
                        float targetX = currentY * scaleFactor;

                        if (Mathf.Abs(msgT.localScale.x - targetX) > 0.005f)
                        {
                            msgT.localScale = new Vector3(targetX, currentY, msgT.localScale.z);
                        }

                        int id = window.GetInstanceID();
                        if (textCaches.ContainsKey(id))
                        {
                            foreach (var txtTransform in textCaches[id])
                            {
                                if (txtTransform != null)
                                {
                                    float txtY = txtTransform.localScale.y;
                                    float targetTxtX = txtY * inverseScale;

                                    if (Mathf.Abs(txtTransform.localScale.x - targetTxtX) > 0.005f)
                                    {
                                        txtTransform.localScale = new Vector3(targetTxtX, txtY, txtTransform.localScale.z);
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. Smoothly fix Visual Novel Portraits
                foreach (var manager in cachedAdvManagers)
                {
                    if (manager != null && manager.Engine != null && manager.Engine.GraphicManager != null)
                    {
                        Transform charFolder = manager.Engine.GraphicManager.transform.Find("Characters");
                        if (charFolder == null) charFolder = manager.Engine.GraphicManager.transform;

                        float currentY = charFolder.localScale.y;
                        float targetX = currentY * scaleFactor;

                        if (Mathf.Abs(charFolder.localScale.x - targetX) > 0.005f)
                            charFolder.localScale = new Vector3(targetX, currentY, charFolder.localScale.z);
                    }
                }

                // 3. Smoothly fix Dojo Portraits
                foreach (var mainGame in cachedMainGames)
                {
                    if (mainGame != null && mainGame.Engine != null && mainGame.Engine.GraphicManager != null)
                    {
                        Transform gmTransform = mainGame.Engine.GraphicManager.transform;

                        float currentY = gmTransform.localScale.y;
                        float targetX = currentY * scaleFactor;

                        if (Mathf.Abs(gmTransform.localScale.x - targetX) > 0.005f)
                            gmTransform.localScale = new Vector3(targetX, currentY, gmTransform.localScale.z);
                    }
                }

                yield return null;
            }
        }
    }

    // --- RESOLUTION ENFORCER ONLY ---
    public class ResolutionPatches
    {
        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(bool) })]
        [HarmonyPrefix]
        public static bool InterceptLegacyResolution(int width, int height)
        {
            int targetW = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            int targetH = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;
            return (width == targetW && height == targetH);
        }

        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(UnityEngine.FullScreenMode) })]
        [HarmonyPrefix]
        public static bool InterceptModernResolution(int width, int height)
        {
            int targetW = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            int targetH = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;
            return (width == targetW && height == targetH);
        }
    }
}