using BepInEx;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TootTallyCore.Utils.Helpers;
using TootTallySettings.TootTallySettingsObjects;
using UnityEngine;
using static UnityEngine.UIElements.StyleVariableResolver;

namespace TootTallyCustomTromboner
{
    public static class CustomTromboner
    {
        public const string CUSTOM_TROMBONER_FOLDER = "CustomTromboners";

        private static Dictionary<string, AssetBundle> _bonerDict;
        private static AssetBundle _currentBundle;

        public static string[] GetBonerNames => _bonerDict.Keys.ToArray();

        public static void LoadAssetBundles()
        {
            Plugin.LogInfo("New Custom Tromboner detected, reloading CustomTromboners...");

            if (_bonerDict != null)
                foreach (string key in _bonerDict.Keys)
                    _bonerDict[key].Unload(true);

            _bonerDict = new Dictionary<string, AssetBundle>();

            var path = Path.Combine(Paths.BepInExRootPath, CUSTOM_TROMBONER_FOLDER);
            var files = TrombonerFileHelper.GetAllBonerFilesFromDirectory(path);
            files.ForEach(AddToAssetBundle);
            Plugin.LogInfo("Custom Tromboners Loaded.");
        }

        public static void AddToAssetBundle(FileInfo file)
        {
            Plugin.LogInfo($"Would add {file.Name} using {file.FullName}");
            try
            {
                _bonerDict.Add(file.Name.Replace(TrombonerFileHelper.BONER_FILE_EXT, ""), AssetBundle.LoadFromFile(file.FullName));
            }
            catch (Exception ex)
            {
                Plugin.LogError(ex.Message);
                Plugin.LogError(ex.StackTrace);
            }
        }

        public static void ResolveCurrentBundle()
        {
            var bonerName = Plugin.Instance.option.BonerName.Value;
            if (bonerName != Plugin.DEFAULT_BONER && _bonerDict.ContainsKey(bonerName))
            {
                _currentBundle = _bonerDict[bonerName];
                _currentBundle.GetAllAssetNames().ToList().ForEach(Plugin.LogInfo);
                Plugin.LogInfo($"Boner bundle {_currentBundle.name} loaded.");
            }
            else if (_currentBundle != null && bonerName == Plugin.DEFAULT_BONER)
            {
                Plugin.LogInfo($"No bundle loaded.");
                _currentBundle = null;
            }
        }

        public static Material GetMaterial(string name) => _currentBundle.LoadAsset<Material>(name);
        public static Texture GetTexture(string name) => _currentBundle.LoadAsset<Texture>(name);

        public static int GetPuppetIDFromName(string PuppetName) => PuppetName.ToLower().Replace(" ", "") switch
        {
            "beezerly" or "kazyleii" => 0,
            "appaloosa" or "trixiebell" => 2,
            "hornlord" or "soda" => 4,
            "jermajesty" or "meldor" => 6,
            _ => -1
        };

        public static class CustomTrombonerPatches
        {
            public static GameObject _customPuppet, _customPuppetPrefab;
            public static Animator _customPuppetAnimator;
            [HarmonyPatch(typeof(HomeController), nameof(HomeController.tryToSaveSettings))]
            [HarmonyPostfix]
            public static void OnSettingsChange()
            {
                ResolveCurrentBundle();
            }

            [HarmonyPatch(typeof(HomeController), nameof(HomeController.Start))]
            [HarmonyPostfix]
            public static void OnHomeControllerStart()
            {
                var path = Path.Combine(Paths.BepInExRootPath, CUSTOM_TROMBONER_FOLDER);
                if (_bonerDict == null || Directory.GetFiles(path).Any(x => !_bonerDict.ContainsKey(x.Replace(TrombonerFileHelper.BONER_FILE_EXT, ""))))
                    LoadAssetBundles();
                ResolveCurrentBundle();
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPrefix]
            public static void SetPuppetIDOnStartPrefix(GameController __instance)
            {
                if (_currentBundle == null) return;
                _customPuppetPrefab = _currentBundle.LoadAsset<GameObject>("puppet.prefab");
                if (_customPuppetPrefab != null)
                {
                    _customPuppet = GameObject.Instantiate(_customPuppetPrefab, __instance.modelparent.transform);
                    _customPuppet.transform.localPosition = new Vector3(0.7f, -0.4f, 1.3f);

                    if (!_customPuppet.TryGetComponent(out _customPuppetAnimator))
                    {
                        Plugin.LogInfo("No animator found in custom puppet.");
                    }
                    else
                    {
                        _customPuppetAnimator.SetBool("Tooting", false);
                        _customPuppetAnimator.SetBool("OutOfBreath", false);
                        _customPuppetAnimator.SetFloat("PointerY", 0);
                        _customPuppetAnimator.SetFloat("Tempo", __instance.tempo);
                        _customPuppetAnimator.SetFloat("AnimationSpeed", __instance.tempo / 120f);
                    }
                }
            }

            private static bool _lastTooting;

            [HarmonyPatch(typeof(GameController), nameof(GameController.isNoteButtonPressed))]
            [HarmonyPostfix]
            public static void OnTootingEvent(ref bool __result)
            {
                if (_customPuppetAnimator == null) return;

                if (_lastTooting != __result)
                    _customPuppetAnimator.SetBool("Tooting", __result);

                _lastTooting = __result;
            }

            private static bool _lastOutOfBreath;

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPostfix]
            public static void UpdatePointerYEvent(GameController __instance)
            {
                if (_customPuppetAnimator == null) return;

                _customPuppetAnimator.SetFloat("PointerY", __instance.pointer.transform.localPosition.y);

                if (_lastOutOfBreath != __instance.outofbreath)
                    _customPuppetAnimator.SetBool("OutOfBreath", __instance.outofbreath);

                _lastOutOfBreath = __instance.outofbreath;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OverwriteHumanPuppetBody(GameController __instance)
            {
                if (_customPuppetPrefab != null)
                {
                    __instance.puppet_human.transform.localScale = Vector3.zero;
                    return;
                }
                if (_currentBundle == null)
                    return;
                Plugin.LogInfo("Applying Custom Boner...");
                var puppetController = __instance.puppet_humanc;
                puppetController.head_oob = GetMaterial("head_oob.mat");
                puppetController.head_def = GetMaterial("head_def.mat");
                puppetController.head_def_es = GetMaterial("head_def_es.mat");
                puppetController.head_act = GetMaterial("head_act.mat");
                puppetController.costume_alt = GetMaterial("custom_alt.mat");
                var mats = puppetController.bodymesh.materials;
                mats[0] = GetMaterial("body.mat");
                puppetController.bodymesh.materials = mats;
            }
        }

        //Not used yet
        [Serializable]
        private class BonerInfo : MonoBehaviour
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Author { get; set; }
            public string PuppetName { get; set; }
        }
    }
}
