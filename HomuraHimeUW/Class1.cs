using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
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

        private void Awake()
        {
            AutoResolution = Config.Bind("Resolution", "AutoDetect", true, "Set to true to use native resolution.");
            ResWidth = Config.Bind("Resolution", "ManualWidth", 3440, "Manual width");
            ResHeight = Config.Bind("Resolution", "ManualHeight", 1440, "Manual height");

            // We only use Harmony to enforce the resolution now. No more UI patching!
            Harmony.CreateAndPatchAll(typeof(ResolutionPatches));
            Logger.LogInfo("Ultrawide Fix loaded successfully!");
        }

        private void Start()
        {
            StartCoroutine(ApplyResolutionDelayed());

            // Start our lag-free native UI checker
            StartCoroutine(NativeUIFixLoop());
        }

        private System.Collections.IEnumerator ApplyResolutionDelayed()
        {
            yield return new UnityEngine.WaitForSeconds(1f);

            if (AutoResolution.Value)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(ResWidth.Value, ResHeight.Value, FullScreenMode.FullScreenWindow);
            }
        }

        // --- THE NATIVE ZERO-LAG UI FIXER ---
        private System.Collections.IEnumerator NativeUIFixLoop()
        {
            // This runs natively twice a second. Zero CPU bottleneck.
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.5f);

            while (true)
            {
                // 1. Restore the Canvas Scaler Fix safely
                CanvasScaler[] scalers = FindObjectsOfType<CanvasScaler>();
                foreach (CanvasScaler scaler in scalers)
                {
                    if (scaler.matchWidthOrHeight != 1f)
                    {
                        scaler.matchWidthOrHeight = 1f;
                    }
                }

                float targetAspect = 1.7777778f;
                float currentAspect = (float)Screen.width / (float)Screen.height;
                float scaleFactor = targetAspect / currentAspect;

                // 2. Slim the Text Box and Visual Novel Portraits
                Utage.AdvUguiManager[] advManagers = FindObjectsOfType<Utage.AdvUguiManager>();
                foreach (var manager in advManagers)
                {
                    if (manager.MessageWindow != null)
                    {
                        RectTransform msgRect = manager.MessageWindow.transform as RectTransform;
                        if (msgRect != null && msgRect.localScale.x != scaleFactor)
                        {
                            msgRect.localScale = new Vector3(scaleFactor, 1f, 1f);
                        }
                    }

                    if (manager.Engine != null && manager.Engine.GraphicManager != null)
                    {
                        Transform charFolder = manager.Engine.GraphicManager.transform.Find("Characters");
                        if (charFolder == null) charFolder = manager.Engine.GraphicManager.transform;

                        if (charFolder.localScale.x != scaleFactor)
                        {
                            charFolder.localScale = new Vector3(scaleFactor, 1f, 1f);
                        }
                    }
                }

                // 3. Slim the Dojo / Main Game Portraits
                UtageUguiMainGame[] mainGames = FindObjectsOfType<UtageUguiMainGame>();
                foreach (var mainGame in mainGames)
                {
                    if (mainGame.Engine != null && mainGame.Engine.GraphicManager != null)
                    {
                        Transform gmTransform = mainGame.Engine.GraphicManager.transform;
                        if (gmTransform.localScale.x != scaleFactor)
                        {
                            gmTransform.localScale = new Vector3(scaleFactor, 1f, 1f);
                        }
                    }
                }

                yield return wait;
            }
        }
    }

    // --- RESOLUTION ENFORCER ONLY ---
    // --- RESOLUTION ENFORCER ONLY ---
    public class ResolutionPatches
    {
        [HarmonyPatch(typeof(UnityEngine.Screen), "SetResolution", new System.Type[] { typeof(int), typeof(int), typeof(bool) })]
        [HarmonyPrefix]
        public static bool InterceptLegacyResolution(int width, int height)
        {
            int targetW = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemWidth : UltrawidePlugin.ResWidth.Value;
            int targetH = UltrawidePlugin.AutoResolution.Value ? UnityEngine.Display.main.systemHeight : UltrawidePlugin.ResHeight.Value;

            // If the game is trying to set our correct ultrawide resolution, let it pass (return true).
            // If it tries to revert to 16:9, BLOCK the engine from running the method entirely (return false).
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