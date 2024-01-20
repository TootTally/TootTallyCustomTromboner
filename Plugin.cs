using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyModules;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallySettings;
using TootTallySettings.TootTallySettingsObjects;
using UnityEngine;

namespace TootTallyCustomTromboner
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallyCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallySettings", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "CustomBoner.cfg";
        private const string BONER_CONFIG_FIELD = "CustomBoner";
        public const string DEFAULT_BONER = "None";
        public Options option;
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "Custom Tromboner"; set => Name = value; }

        public static TootTallySettingPage settingPage;
        public static TootTallySettingDropdown dropdown;

        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "Custom Tromboner", true, "Change the appearance of your tromboner");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            option = new Options()
            {
                BonerName = config.Bind(BONER_CONFIG_FIELD, nameof(option.BonerName), DEFAULT_BONER),
            };

            string sourceFolderPath = Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location), CustomTromboner.CUSTOM_TROMBONER_FOLDER);
            string targetFolderPath = Path.Combine(Paths.BepInExRootPath, CustomTromboner.CUSTOM_TROMBONER_FOLDER);
            FileHelper.TryMigrateFolder(sourceFolderPath, targetFolderPath, true);

            settingPage = TootTallySettingsManager.AddNewPage("Custom Tromboner", "Custom Tromboner", 40f, new Color(0, 0, 0, 0));
            settingPage.AddLabel("BonerLabel", "Custom Tromboners", 24, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.BottomLeft);
            CustomTromboner.LoadAssetBundles();
            List<string> folderNames = new() { DEFAULT_BONER };
            folderNames.AddRange(CustomTromboner.GetBonerNames);
            dropdown = settingPage.AddDropdown($"BonerDropdown", Instance.option.BonerName, folderNames.ToArray());
            settingPage.AddButton("Reload Tromboners", delegate
            {
                dropdown.dropdown.ClearOptions();
                dropdown.AddOptions(DEFAULT_BONER);
                CustomTromboner.LoadAssetBundles();
                TootTallyNotifManager.DisplayNotif("Reloaded tromboners.");
            });

            TootTallySettings.Plugin.TryAddThunderstoreIconToPageButton(Instance.Info.Location, Name, settingPage);

            _harmony.PatchAll(typeof(CustomTromboner.CustomTrombonerPatches));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }


        public class Options
        {
            public ConfigEntry<string> BonerName { get; set; }
        }
    }
}