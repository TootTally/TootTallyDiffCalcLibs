using BaboonAPI.Hooks.Initializer;
using BaboonAPI.Hooks.Tracks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TootTallyCore.Utils.TootTallyGlobals;
using TootTallyCore.Utils.TootTallyModules;
using TrombLoader.CustomTracks;

namespace TootTallyDiffCalcLibs
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallyCore", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "TootTallyDiffCalcLibs.cfg";
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        public string Name { get => PluginInfo.PLUGIN_NAME; set => Name = value; }

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
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "DiffCalcLibs", true, "Library to locally calculate the difficulty of charts.");
            TootTallyModuleManager.AddModule(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            _harmony.PatchAll(typeof(DiffCalcPatches));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            _harmony.UnpatchSelf();
            LogInfo($"Module unloaded!");
        }

        public static class DiffCalcPatches
        {
            private static CancellationTokenSource _cancellationToken;
            private static string _lastTrackref;

            [HarmonyPatch(typeof(LoadController), nameof(LoadController.Start))]
            [HarmonyPostfix]
            public static void ProcessChartBackup()
            {
                //Backup
                if (DiffCalcGlobals.selectedChart.trackRef == GlobalVariables.chosen_track_data.trackref) return;

                var path = GetSongTMBPath(GlobalVariables.chosen_track_data.trackref);
                _cancellationToken?.Cancel();
                _cancellationToken = new CancellationTokenSource();
                var isBaseGame = path == GlobalVariables.chosen_track_data.trackref;
                Task.Run(() => ProcessChart(path, isBaseGame, _cancellationToken), _cancellationToken.Token);
            }


            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.advanceSongs))]
            [HarmonyPostfix]
            public static void OnSongChangeProcessChartAsync(List<SingleTrackData> ___alltrackslist, int ___songindex)
            {
                var trackref = ___alltrackslist[___songindex].trackref;
                if (DiffCalcGlobals.selectedChart.trackRef == trackref || _lastTrackref == trackref)
                {
                    Plugin.LogInfo($"{DiffCalcGlobals.selectedChart.trackRef} - {trackref} - trackref was the same.");
                    return;
                }

                var path = GetSongTMBPath(trackref);
                _lastTrackref = trackref;
                _cancellationToken?.Cancel();
                _cancellationToken = new CancellationTokenSource();
                var isBaseGame = path == trackref;
                Task.Run(() => ProcessChart(path, isBaseGame, _cancellationToken), _cancellationToken.Token);
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Start))]
            [HarmonyPostfix]
            public static void ProcessFirstChart(List<SingleTrackData> ___alltrackslist, int ___songindex) => OnSongChangeProcessChartAsync(___alltrackslist, ___songindex);

            private async static void ProcessChart(string path, bool isBaseGame, CancellationTokenSource source)
            {
                if (isBaseGame) Plugin.LogInfo($"Trying to get base game chart: {path}");
                Chart c = isBaseGame ? ChartReader.LoadBaseGame(path) : ChartReader.LoadChart(path);
                if (source.IsCancellationRequested)
                {
                    Plugin.LogInfo($"Disposing of {c.shortName}");
                    c.Dispose();
                    return;
                }
                Plugin.LogInfo($"Song {c.shortName} processed in {c.calculationTime.TotalSeconds}s");
                DiffCalcGlobals.selectedChart.Dispose();
                DiffCalcGlobals.selectedChart = c;
                DiffCalcGlobals.OnSelectedChartSetEvent?.Invoke(c);
                _cancellationToken = null;
                await Task.Yield();
            }

            public static string GetSongTMBPath(string trackref)
            {
                var track = TrackLookup.lookup(trackref);
                if (track is CustomTrack ct)
                {
                    var path = $"{ct.folderPath}/song.tmb";
                    if (File.Exists(path))
                        return path;
                }
                return trackref;
            }
        }
    }
}