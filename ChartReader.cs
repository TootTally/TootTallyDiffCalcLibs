using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using UnityEngine;
using static TootTallyCore.Utils.Helpers.SongDataHelper;

namespace TootTallyDiffCalcLibs
{
    public static class ChartReader
    {
        private static List<Chart> _allChartList = new List<Chart>();
        private static readonly string TrackassetDir = $"{Application.streamingAssetsPath}/trackassets";

        public static void AddChartToList(string path) =>
            _allChartList.Add(LoadChart(path));

        public static Chart LoadBaseGame(string trackRef)
        {
            var binaryFormatter = new BinaryFormatter();
            var chart = new Chart();
            var metadataFilePath = $"{TrackassetDir}/{trackRef}/metadata_en.tmb";
            using (FileStream fileStream = File.Open(metadataFilePath, FileMode.Open))
            {
                var metadata = (SavedLevelMetadata)binaryFormatter.Deserialize(fileStream);
                chart.name = metadata.trackname_long;
                chart.shortName = metadata.trackname_short;
                chart.trackRef = trackRef;
                chart.author = metadata.artist;
                chart.genre = metadata.genre;
                chart.description = metadata.description;
                chart.difficulty = metadata.difficulty.ToString();
                chart.year = metadata.year;
            }

            var songFilePath = $"{TrackassetDir}/{trackRef}/trackdata.tmb";
            using (FileStream fileStream = File.Open(songFilePath, FileMode.Open))
            {
                var savedLevel = (SavedLevel)binaryFormatter.Deserialize(fileStream);
                chart.savednotespacing = savedLevel.savednotespacing;
                chart.endpoint = savedLevel.endpoint;
                chart.timesig = savedLevel.timesig.ToString();
                chart.tempo = savedLevel.tempo;
                chart.notes = savedLevel.savedleveldata.ToArray();
            }
            chart.OnDeserialize();
            return chart;
        }

        public static Chart LoadChart(string path)
        {
            StreamReader reader = new StreamReader(path);
            string json = reader.ReadToEnd();
            reader.Close();
            Chart chart = JsonConvert.DeserializeObject<Chart>(json);
            chart.OnDeserialize();
            return chart;
        }

        public static Chart LoadChartFromJson(string json)
        {
            Chart chart = JsonConvert.DeserializeObject<Chart>(json);
            chart.OnDeserialize();
            return chart;
        }

        public static string CalcSHA256Hash(byte[] data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                string ret = "";
                byte[] hashArray = sha256.ComputeHash(data);
                foreach (byte b in hashArray)
                {
                    ret += $"{b:x2}";
                }
                return ret;
            }
        }

        public static void SaveChartData(string path, string json)
        {
            StreamWriter writer = new StreamWriter(path);
            writer.WriteLine(json);
            writer.Close();
        }
    }
}
