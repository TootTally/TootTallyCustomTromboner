using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

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
            Plugin.LogInfo("Loading CustomTromboners...");

            if (_bonerDict != null)
                foreach (string key in _bonerDict.Keys)
                    _bonerDict[key].Unload(true);

            _bonerDict = new Dictionary<string, AssetBundle>();

            var path = Path.Combine(Paths.BepInExRootPath, CUSTOM_TROMBONER_FOLDER);
            var files = TrombonerFileHelper.GetAllBonerFilesFromDirectory(path);
            files.ForEach(file =>
            {
                Plugin.Instance.StartCoroutine(LoadAssetBundleAsync(file.FullName, bundle =>
                {
                    var name = file.Name.Replace(TrombonerFileHelper.BONER_FILE_EXT, "");
                    _bonerDict.Add(name, bundle);
                    Plugin.dropdown.AddOptions(name);
                    Plugin.LogInfo($"{name} boner added.");
                }));
            });
            Plugin.LogInfo("Custom Tromboners Loaded.");
        }

        public static IEnumerator<AssetBundleCreateRequest> LoadAssetBundleAsync(string path, Action<AssetBundle> callback)
        {
            var bundleRequest = AssetBundle.LoadFromFileAsync(path);
            yield return bundleRequest;
            if (bundleRequest != null)
                callback(bundleRequest.assetBundle);
            else
                Plugin.LogInfo($"Failed to load {path} boner.");
        }

        public static void ResolveCurrentBundle()
        {
            var bonerName = Plugin.Instance.option.BonerName.Value;
            if (bonerName != Plugin.DEFAULT_BONER && _bonerDict.ContainsKey(bonerName))
            {
                _currentBundle = _bonerDict[bonerName];
                Plugin.LogInfo($"Boner bundle {_currentBundle.name} loaded.");
            }
            else if (_currentBundle != null && bonerName == Plugin.DEFAULT_BONER)
            {
                Plugin.LogInfo($"No bundle loaded.");
                _currentBundle = null;
            }
        }

        public static Material GetMaterial(string name) => _currentBundle?.LoadAsset<Material>(name);
        public static Texture GetTexture(string name) => _currentBundle?.LoadAsset<Texture>(name);

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

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPrefix]
            public static void SetPuppetIDOnStartPrefix(GameController __instance)
            {
                if (Plugin.Instance.option.BonerName.Value == Plugin.DEFAULT_BONER) return;
                else if (_currentBundle == null)
                    ResolveCurrentBundle(); //Try to load bundle

                if (_currentBundle == null) return; //If fails to load

                _customPuppetPrefab = _currentBundle.LoadAsset<GameObject>("puppet.prefab");
                if (_customPuppetPrefab != null)
                {
                    _customPuppet = GameObject.Instantiate(_customPuppetPrefab, __instance.modelparent.transform);
                    _customPuppet.transform.localPosition = new Vector3(0.7f, -0.4f, 1.3f);

                    if (!_customPuppet.TryGetComponent(out _customPuppetAnimator))
                        Plugin.LogInfo("No animator found in custom puppet.");
                    else
                        _customPuppetAnimator.enabled = false;
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

                var normPointerY = (__instance.pointer.transform.localPosition.y + 180) / 360f;
                _customPuppetAnimator.SetFloat("PointerY", normPointerY);
                _customPuppetAnimator.SetFloat("Breathing", __instance.breathcounter);

                if (_lastOutOfBreath != __instance.outofbreath)
                    _customPuppetAnimator.SetBool("OutOfBreath", __instance.outofbreath);

                _lastOutOfBreath = __instance.outofbreath;
            }

            [HarmonyPatch(typeof(HumanPuppetController), nameof(HumanPuppetController.startPuppetBob))]
            [HarmonyPostfix]
            public static void ActivateCustomPuppetAnimation()
            {
                if (_customPuppetAnimator != null)
                    _customPuppetAnimator.enabled = true;
            }

            [HarmonyPatch(typeof(HumanPuppetController), nameof(HumanPuppetController.playCameraRotationTween))]
            [HarmonyPostfix]
            public static void SetCustomAnimationActive(bool play)
            {
                if (_customPuppetAnimator != null)
                    _customPuppetAnimator.enabled = play;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OverwriteHumanPuppetBody(GameController __instance)
            {
                if (_customPuppetPrefab != null)
                {
                    __instance.puppet_human.SetActive(false);
                    if (_customPuppetAnimator != null)
                    {
                        _customPuppetAnimator.SetBool("Tooting", false);
                        _customPuppetAnimator.SetBool("OutOfBreath", false);
                        _customPuppetAnimator.SetFloat("PointerY", .5f);
                        var tempo = __instance.tempo * (GlobalVariables.turbomode ? 2 : GlobalVariables.practicemode);
                        _customPuppetAnimator.SetFloat("Tempo", tempo);
                        _customPuppetAnimator.SetFloat("AnimationSpeed", tempo / 120f);
                    }

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
