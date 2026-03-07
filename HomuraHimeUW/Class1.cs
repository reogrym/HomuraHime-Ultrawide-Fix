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
        // 1. Replaces the UnityEngine.UI.dll edit
        [HarmonyPatch(typeof(CanvasScaler), "HandleScaleWithScreenSize")]
        [HarmonyPrefix]
        public static void PreFixCanvasScaler(CanvasScaler __instance)
        {
            __instance.matchWidthOrHeight = 1f;
        }

        // 2. Replaces the AdvUguiManager edit
        // CHANGED: We now hook into "Update" because LateUpdate doesn't exist in the vanilla file!
        [HarmonyPatch(typeof(Utage.AdvUguiManager), "Update")]
        [HarmonyPostfix]
        public static void FixUIElements(Utage.AdvUguiManager __instance)
        {
            float targetAspect = 1.7777778f;
            float currentAspect = (float)Screen.width / (float)Screen.height;
            float scaleFactor = targetAspect / currentAspect;

            // SLIM THE TEXT BOX 
            if (__instance.MessageWindow != null)
            {
                RectTransform msgRect = __instance.MessageWindow.GetComponent<RectTransform>();
                Vector3 targetScale = new Vector3(scaleFactor, 1f, 1f);

                if (msgRect != null && Vector3.Distance(msgRect.localScale, targetScale) > 0.005f)
                {
                    msgRect.localScale = targetScale;
                }
            }

            // SLIM THE PORTRAITS (Targeting the Character folder)
            if (__instance.Engine != null && __instance.Engine.GraphicManager != null)
            {
                Transform charFolder = __instance.Engine.GraphicManager.transform.Find("Characters");
                if (charFolder == null) charFolder = __instance.Engine.GraphicManager.transform;

                RectTransform rect = charFolder.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (rect.localScale.x != scaleFactor)
                    {
                        rect.localScale = new Vector3(scaleFactor, 1f, 1f);
                    }
                }
            }
        }

        // 3. Replaces the UtageUguiMainGame portrait edit
        // This one stays as LateUpdate because it actually exists natively here.
        [HarmonyPatch(typeof(UtageUguiMainGame), "LateUpdate")]
        [HarmonyPostfix]
        public static void FixMainGame(UtageUguiMainGame __instance)
        {
            if (__instance.Engine != null && __instance.Engine.GraphicManager != null)
            {
                AspectRatioFitter aspect = __instance.Engine.GraphicManager.gameObject.GetComponent<AspectRatioFitter>();
                if (aspect == null)
                {
                    aspect = __instance.Engine.GraphicManager.gameObject.AddComponent<AspectRatioFitter>();
                    aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    aspect.aspectRatio = 1.7777778f;
                }
            }
        }
        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(bool) })]
        [HarmonyPrefix]
        public static void InterceptLegacyResolution(ref int width, ref int height)
        {
            width = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            height = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;
        }

        // Intercepts modern resolution commands
        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(UnityEngine.FullScreenMode) })]
        [HarmonyPrefix]
        public static void InterceptModernResolution(ref int width, ref int height)
        {
            width = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            height = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;
        }
    }
}