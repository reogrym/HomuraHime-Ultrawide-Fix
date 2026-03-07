using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UltrawideFixMod
{
    // BepInEx metadata to identify your mod
    [BepInPlugin("himeuw", "Homura Hime Ultrawide Fix", "0.0.1")]
    public class UltrawidePlugin : BaseUnityPlugin
    {
        // Variables to hold the config settings
        public static ConfigEntry<bool> AutoResolution;
        public static ConfigEntry<int> ResWidth;
        public static ConfigEntry<int> ResHeight;

        private void Awake()
        {
            // 1. GENERATE THE CONFIG FILE INSTANTLY
            AutoResolution = Config.Bind("Resolution", "AutoDetect", true, "Set to true to use your monitor's native resolution. False to use manual width/height.");
            ResWidth = Config.Bind("Resolution", "ManualWidth", 3440, "Manual width if AutoDetect is false.");
            ResHeight = Config.Bind("Resolution", "ManualHeight", 1440, "Manual height if AutoDetect is false.");

            // 2. INJECT THE FIXES INSTANTLY
            Harmony.CreateAndPatchAll(typeof(UltrawidePatches));

            Logger.LogInfo("Ultrawide Fix loaded successfully!");
        }

        private void Start()
        {
            // Tell Unity to run our delayed resolution routine
            StartCoroutine(ApplyResolutionDelayed());
        }

        // This is the coroutine that waits for the game to finish booting
        private System.Collections.IEnumerator ApplyResolutionDelayed()
        {
            // Wait for exactly 1 second to let the game's internal settings load first
            yield return new UnityEngine.WaitForSeconds(1f);

            // 3. APPLY RESOLUTION 
            if (AutoResolution.Value)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                // Changed to standard string concatenation to fix the C# 7.3 error
                Logger.LogInfo("Resolution forced to Auto: " + Display.main.systemWidth + "x" + Display.main.systemHeight);
            }
            else
            {
                Screen.SetResolution(ResWidth.Value, ResHeight.Value, FullScreenMode.FullScreenWindow);
                // Changed to standard string concatenation here as well
                Logger.LogInfo("Resolution forced to Manual: " + ResWidth.Value + "x" + ResHeight.Value);
            }
        }
    }

    // This class contains all the logic you previously injected via dnSpy
    public class UltrawidePatches
    {
        // --- SMART CACHE VARIABLES ---
        private static object lastGraphicManager = null;
        private static RectTransform cachedCharRect = null;

        private static UnityEngine.GameObject lastMainGameObj = null;
        private static UnityEngine.UI.AspectRatioFitter cachedFitter = null;

        // 1. Replaces the UnityEngine.UI.dll edit
        [HarmonyPatch(typeof(UnityEngine.UI.CanvasScaler), "HandleScaleWithScreenSize")]
        [HarmonyPrefix]
        public static void PreFixCanvasScaler(UnityEngine.UI.CanvasScaler __instance)
        {
            __instance.matchWidthOrHeight = 1f;
        }

        // 2. Replaces the AdvUguiManager edit
        [HarmonyPatch(typeof(Utage.AdvUguiManager), "Update")]
        [HarmonyPostfix]
        public static void FixUIElements(Utage.AdvUguiManager __instance)
        {
            float targetAspect = 1.7777778f;
            float currentAspect = (float)UnityEngine.Screen.width / (float)UnityEngine.Screen.height;
            float scaleFactor = targetAspect / currentAspect;

            // SLIM THE TEXT BOX 
            if (__instance.MessageWindow != null)
            {
                // Using 'as RectTransform' is instant and avoids the expensive GetComponent lag.
                // It ensures we ALWAYS target the active text box in the current scene.
                UnityEngine.RectTransform msgRect = __instance.MessageWindow.transform as UnityEngine.RectTransform;

                if (msgRect != null && msgRect.localScale.x != scaleFactor)
                {
                    msgRect.localScale = new UnityEngine.Vector3(scaleFactor, 1f, 1f);
                }
            }

            // SLIM THE PORTRAITS
            if (__instance.Engine != null && __instance.Engine.GraphicManager != null)
            {
                var currentGM = __instance.Engine.GraphicManager;

                // If the game loaded a new scene/manager, or our cache broke, we refresh it once
                if (lastGraphicManager != currentGM || cachedCharRect == null)
                {
                    lastGraphicManager = currentGM;
                    UnityEngine.Transform charFolder = currentGM.transform.Find("Characters");
                    if (charFolder == null) charFolder = currentGM.transform;

                    cachedCharRect = charFolder as UnityEngine.RectTransform;
                }

                if (cachedCharRect != null && cachedCharRect.localScale.x != scaleFactor)
                {
                    cachedCharRect.localScale = new UnityEngine.Vector3(scaleFactor, 1f, 1f);
                }
            }
        }

        // 3. Replaces the UtageUguiMainGame portrait edit
        [HarmonyPatch(typeof(UtageUguiMainGame), "LateUpdate")]
        [HarmonyPostfix]
        public static void FixMainGame(UtageUguiMainGame __instance)
        {
            if (__instance.Engine != null && __instance.Engine.GraphicManager != null)
            {
                UnityEngine.GameObject currentObj = __instance.Engine.GraphicManager.gameObject;

                // Smart cache to prevent running GetComponent/AddComponent every single frame
                if (lastMainGameObj != currentObj || cachedFitter == null)
                {
                    lastMainGameObj = currentObj;
                    cachedFitter = currentObj.GetComponent<UnityEngine.UI.AspectRatioFitter>();

                    if (cachedFitter == null)
                    {
                        cachedFitter = currentObj.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                    }
                }

                if (cachedFitter != null)
                {
                    cachedFitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
                    cachedFitter.aspectRatio = 1.7777778f;
                }
            }
        }

        // 4. THE RESOLUTION INTERCEPTORS
        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(bool) })]
        [HarmonyPrefix]
        public static void InterceptLegacyResolution(ref int width, ref int height)
        {
            width = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            height = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;
        }

        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(UnityEngine.FullScreenMode) })]
        [HarmonyPrefix]
        public static void InterceptModernResolution(ref int width, ref int height)
        {
            width = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            height = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;
        }
    }
}