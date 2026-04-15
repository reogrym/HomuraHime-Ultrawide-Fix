using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UltrawideFixMod
{
    [BepInPlugin("himeuw", "Homura Hime Ultrawide Fix", "0.1.3")] // plugin GUID, name, version, hopefully the last version number I have to update for a while lol
    public class UltrawidePlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> AutoResolution;
        public static ConfigEntry<int> ResWidth;
        public static ConfigEntry<int> ResHeight;

        private static List<Utage.AdvUguiManager> allAdvManagers = new List<Utage.AdvUguiManager>();
        private static List<UtageUguiMainGame> allMainGames = new List<UtageUguiMainGame>();
        private static List<Utage.AdvUguiMessageWindow> allMsgWindows = new List<Utage.AdvUguiMessageWindow>();
        private static Dictionary<int, List<Transform>> textCaches = new Dictionary<int, List<Transform>>();

        private void Awake()
        {
            AutoResolution = Config.Bind("Resolution", "AutoDetect", true, "Set to true to use native resolution.");
            ResWidth = Config.Bind("Resolution", "ManualWidth", 3440, "Manual width");
            ResHeight = Config.Bind("Resolution", "ManualHeight", 1440, "Manual height");

            Harmony.CreateAndPatchAll(typeof(ResolutionPatches));

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            StartCoroutine(ApplyResolutionDelayed());
            StartCoroutine(FastUpdateLoop());
        }

        private System.Collections.IEnumerator ApplyResolutionDelayed()
        {
            yield return new WaitForSeconds(1f);
            if (AutoResolution.Value) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(ResWidth.Value, ResHeight.Value, FullScreenMode.FullScreenWindow);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            float targetAspect = 1.7777778f;

            // Apply HUD Pillarbox once per scene because the game doesn't do it for us and it causes scaling issues with the UI otherwise
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

            // Refresh UI managers and text box caches for the FastUpdateLoop to use, we have to do this every scene because the game creates/destroys UI managers instead of reusing them
            allAdvManagers.Clear();
            allAdvManagers.AddRange(FindObjectsOfType<Utage.AdvUguiManager>());

            allMainGames.Clear();
            allMainGames.AddRange(FindObjectsOfType<UtageUguiMainGame>());

            // Find all text boxes in the scene to apply scaling
            allMsgWindows.Clear();
            foreach (var manager in allAdvManagers)
            {
                if (manager != null) allMsgWindows.AddRange(manager.GetComponentsInChildren<Utage.AdvUguiMessageWindow>(true));
            }
            foreach (var mainGame in allMainGames)
            {
                if (mainGame != null) allMsgWindows.AddRange(mainGame.GetComponentsInChildren<Utage.AdvUguiMessageWindow>(true));
            }

            // Cache fonts to avoid doing GetComponentsInChildren every frame which causes stuttering when too many text boxes are on screen
            textCaches.Clear();
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
        }

        private System.Collections.IEnumerator FastUpdateLoop()
        {
            while (true)
            {
                try
                {
                    float targetAspect = 1.7777778f;
                    float currentAspect = (float)Screen.width / (float)Screen.height;
                    float scaleFactor = targetAspect / currentAspect;
                    float inverseScale = 1f / scaleFactor;

                    // Fix for Text Boxes and Cut-Ins
                    foreach (var window in allMsgWindows)
                    {
                        if (window != null && window.gameObject != null && window.gameObject.activeInHierarchy)
                        {
                            Transform msgT = window.transform;

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

                    // Fix VN (Utage) portraits and character sprites
                    foreach (var manager in allAdvManagers)
                    {
                        if (manager != null && manager.Engine != null && manager.Engine.GraphicManager != null && manager.gameObject != null && manager.gameObject.activeInHierarchy)
                        {
                            Transform charFolder = manager.Engine.GraphicManager.transform.Find("Characters");
                            if (charFolder == null) charFolder = manager.Engine.GraphicManager.transform;

                            float currentY = charFolder.localScale.y;
                            float targetX = currentY * scaleFactor;

                            if (Mathf.Abs(charFolder.localScale.x - targetX) > 0.005f)
                                charFolder.localScale = new Vector3(targetX, currentY, charFolder.localScale.z);
                        }
                    }

                    // Fix dojo portraits
                    foreach (var mainGame in allMainGames)
                    {
                        if (mainGame != null && mainGame.Engine != null && mainGame.Engine.GraphicManager != null && mainGame.gameObject != null && mainGame.gameObject.activeInHierarchy)
                        {
                            Transform gmTransform = mainGame.Engine.GraphicManager.transform;

                            float currentY = gmTransform.localScale.y;
                            float targetX = currentY * scaleFactor;

                            if (Mathf.Abs(gmTransform.localScale.x - targetX) > 0.005f)
                                gmTransform.localScale = new Vector3(targetX, currentY, gmTransform.localScale.z);
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Silent error handling
                }

                yield return null;
            }
        }
    }

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