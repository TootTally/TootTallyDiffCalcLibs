using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace TootTallyDiffCalcLibs
{
    public struct Chart : IDisposable
    {
        public float[][] notes;
        public string[][] bgdata;
        public Dictionary<float, List<Note>> notesDict;
        public List<string> note_color_start;
        public List<string> note_color_end;
        public float endpoint;
        public float savednotespacing;
        public float tempo;
        public string timesig;
        public string trackRef;
        public string name;
        public string shortName;
        public string author;
        public string genre;
        public string description;
        public string difficulty;
        public string year;
        public int maxScore;
        public int gameMaxScore;
        public Dictionary<int, int> indexToMaxScoreDict;
        public Dictionary<int, int> indexToNoteCountDict;

        public ChartPerformances performances;

        public TimeSpan calculationTime;
        public int sliderCount;
        public float songLength, songLengthMult;

        public void ProcessLite()
        {
            notesDict = new Dictionary<float, List<Note>>();
            CreateNotes(0, 1);
            songLengthMult = GetSongLengthMult(notesDict[0]);
            sliderCount = GetNoteCount();
            performances = new ChartPerformances(notesDict[0].Count, sliderCount);
            performances.Calculate(0, notesDict[0], songLengthMult);
        }

        public void Process()
        {
            notesDict = new Dictionary<float, List<Note>>();
            for (int i = 0; i < Utils.GAME_SPEED.Length; i++)
            {
                CreateNotes(i, Utils.GAME_SPEED[i]);
            }
            songLengthMult = GetSongLengthMult(notesDict[2]);
            sliderCount = GetNoteCount();
            performances = new ChartPerformances(notesDict[0].Count, sliderCount);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < Utils.GAME_SPEED.Length; i++)
            {
                performances.Calculate(i, notesDict[i], songLengthMult);
            }
            stopwatch.Stop();
            calculationTime = stopwatch.Elapsed;
            CalcScores();
        }

        private void CreateNotes(int i, float gamespeed)
        {
            var newTempo = tempo * gamespeed;
            int count = 1;
            notesDict[i] = new List<Note>(notes.Length) { new Note(0, 0, .015f, 0, 0, 0, false) };
            var sortedNotes = notes.OrderBy(x => x[0]).ToArray();
            for (int j = 0; j < sortedNotes.Length; j++)
            {
                float length = sortedNotes[j][1];
                if (length <= 0)//minLength only applies if the note is less or equal to 0 beats, else it keeps its "lower than minimum" length
                    length = 0.015f;
                bool isSlider;
                if (i > 0)
                    isSlider = notesDict[0][j + 1].isSlider;
                else
                    isSlider = j + 1 < sortedNotes.Length && IsSlider(sortedNotes[j], sortedNotes[j + 1]);

                notesDict[i].Add(new Note(count, BeatToSeconds2(sortedNotes[j][0], newTempo), BeatToSeconds2(length, newTempo), sortedNotes[j][2], sortedNotes[j][3], sortedNotes[j][4], isSlider));
                count++;
            }
        }

        private float GetSongLengthMult(List<Note> notes)
        {
            if (notes.Count > 2)
                songLength = notes.Last().position - notes[1].position;
            if (songLength < 1) songLength = 1;
            return Mathf.Pow((songLength + 20f) / 7f, -(float)Math.E * .14f) + .675f; //https://www.desmos.com/calculator/sn1tqkq4gf
        }

        public static float GetLength(float length) => Mathf.Clamp(length, .2f, 5f) * 8f + 10f;

        public int GetNoteCount()
        {
            var noteCount = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                while (i + 1 < notes.Length && IsSlider(notes[i], notes[i + 1])) { i++; }
                noteCount++;
            }
            return noteCount;
        }

        public void CalcScores()
        {
            maxScore = 0;
            gameMaxScore = 0;
            indexToMaxScoreDict = new Dictionary<int, int>();
            indexToNoteCountDict = new Dictionary<int, int>();
            var noteCount = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                var length = notes[i][1];
                while (i + 1 < notes.Length && notes[i][0] + notes[i][1] + .025f >= notes[i + 1][0])
                {
                    length += notes[i + 1][1];
                    i++;
                }
                var champBonus = noteCount > 23 ? 1.5d : 0d;
                var realCoefficient = (Math.Min(noteCount, 10) + champBonus) * 0.1d + 1d;
                var clampedLength = GetLength(length);
                var noteScore = (int)(Math.Floor((float)((double)clampedLength * 100d * realCoefficient)) * 10f);
                maxScore += noteScore;
                gameMaxScore += (int)Math.Floor(Math.Floor(clampedLength * 100f * 1.315f) * 10f);
                indexToMaxScoreDict.Add(i, maxScore);
                noteCount++;
                indexToNoteCountDict.Add(i, noteCount);
            }
        }

        // between 0.5f to 2f
        public float GetBaseTT(float speed) => Utils.CalculateBaseTT(GetDiffRating(Mathf.Clamp(speed, 0.5f, 2f)));

        //Returns the lerped star rating
        public float GetDiffRating(float speed) => performances.GetDiffRating(Mathf.Clamp(speed, 0.5f, 2f));

        public float GetDynamicDiffRating(float speed, float percent, string[] modifiers = null) => performances.GetDynamicDiffRating(percent, speed, modifiers);

        public float GetLerpedStarRating(float speed) => performances.GetDiffRating(Mathf.Clamp(speed, 0.5f, 2f));

        public float GetAimPerformance(float speed) => performances.aimAnalyticsDict[SpeedToIndex(speed)].perfWeightedAverage;
        public float GetTapPerformance(float speed) => performances.tapAnalyticsDict[SpeedToIndex(speed)].perfWeightedAverage;

        public float GetStarRating(float speed) => performances.starRatingDict[SpeedToIndex(speed)];

        public int SpeedToIndex(float speed) => (int)((Mathf.Clamp(speed, 0.5f, 2f) - 0.5f) / .25f);

        public class Lyrics
        {
            public string bar;
            public string text;
        }

        public static float BeatToSeconds2(float beat, float bpm) => 60f / bpm * beat;
        public static bool IsSlider(float[] currNote, float[] nextNote) => currNote[0] + currNote[1] + .025f >= nextNote[0];
        public static float GetHealthDiff(float acc) => Mathf.Clamp((acc - 79f) * 0.2193f, -15f, 4.34f);
        public static int GetScore(float acc, float totalLength, float mult, bool champ)
        {
            var baseScore = Mathf.Clamp(totalLength, 0.2f, 5f) * 8f + 10f;
            return (int)Math.Floor(baseScore * acc * ((mult + (champ ? 1.5f : 0f)) * .1f + 1f)) * 10;
        }

        public void Dispose()
        {
            notes = null;
            bgdata = null;
            notesDict?.Clear();
            performances.Dispose();
            indexToMaxScoreDict?.Clear();
            indexToNoteCountDict?.Clear();
        }

        public class LengthAccPair
        {
            public float length, acc;

            public LengthAccPair(float length, float acc)
            {
                this.length = length;
                this.acc = acc;
            }
        }

    }
}
