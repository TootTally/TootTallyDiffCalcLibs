using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TootTallyDiffCalcLibs
{
    public struct ChartPerformances : IDisposable
    {
        public static readonly float[] weights = {
             1.0000f, 0.9200f, 0.8464f, 0.7787f, 0.7164f, 0.6591f, 0.6064f, 0.5578f,
            0.5132f, 0.4722f, 0.4344f, 0.3996f, 0.3677f, 0.3383f, 0.3112f, 0.2863f,
            0.2634f, 0.2423f, 0.2229f, 0.2051f, 0.1887f, 0.1736f, 0.1597f, 0.1469f,
            0.1352f, 0.1244f, 0.1144f, 0.1053f, 0.0968f, 0.0891f, 0.0820f, 0.0754f,
            0.0694f, 0.0638f, 0.0587f, 0.0540f, 0.0497f, 0.0457f, 0.0421f, 0.0387f,
            0.0356f, 0.0328f, 0.0301f, 0.0277f, 0.0255f, 0.0235f, 0.0216f, 0.0199f,
            0.0183f, 0.0168f, 0.0155f, 0.0142f, 0.0131f, 0.0120f, 0.0111f, 0.0102f,
            0.0094f, 0.0086f, 0.0079f, 0.0073f, 0.0067f, 0.0062f, 0.0057f, 0.0052f,
            0.0048f // :)
        };
        public const float CHEESABLE_THRESHOLD = 34.375f;

        public List<DataVector>[] aimPerfDict;
        public List<DataVector>[] sortedAimPerfDict;
        public DataVectorAnalytics[] aimAnalyticsDict;

        public List<DataVector>[] tapPerfDict;
        public List<DataVector>[] sortedTapPerfDict;
        public DataVectorAnalytics[] tapAnalyticsDict;

        public float[] aimRatingDict;
        public float[] tapRatingDict;
        public float[] starRatingDict;

        private readonly int NOTE_COUNT;

        public ChartPerformances(int noteCount, int sliderCount)
        {
            aimPerfDict = new List<DataVector>[7];
            sortedAimPerfDict = new List<DataVector>[7];
            tapPerfDict = new List<DataVector>[7];
            sortedTapPerfDict = new List<DataVector>[7];
            aimRatingDict = new float[7];
            tapRatingDict = new float[7];
            starRatingDict = new float[7];
            aimAnalyticsDict = new DataVectorAnalytics[7];
            tapAnalyticsDict = new DataVectorAnalytics[7];

            for (int i = 0; i < Utils.GAME_SPEED.Length; i++)
            {
                aimPerfDict[i] = new List<DataVector>(sliderCount);
                tapPerfDict[i] = new List<DataVector>(sliderCount);
            }
            NOTE_COUNT = noteCount;
        }

        public const float AIM_DIV = 90;
        public const float TAP_DIV = 22;
        public const float ACC_DIV = 375;
        public const float AIM_END = 900;
        public const float TAP_END = 50;
        public const float ACC_END = 400;
        public const float MUL_END = 50;
        public const float MAX_DIST = 8f;

        public void CalculatePerformances(int speedIndex, List<Note> noteList)
        {
            var aimEndurance = 0f;
            var tapEndurance = 0f;
            for (int i = 0; i < NOTE_COUNT; i++) //Main Forward Loop
            {
                var currentNote = noteList[i];
                int noteCount = 0;
                float weightSum = 0f;
                var aimStrain = 0f;
                var tapStrain = 0f;
                for (int j = i - 1; j >= 0 && noteCount < 64 && (Mathf.Abs(currentNote.position - noteList[j].position) <= MAX_DIST || i - j <= 2); j--)
                {
                    var prevNote = noteList[j];
                    var nextNote = noteList[j + 1];
                    if (prevNote.position >= nextNote.position) break;

                    var weight = weights[noteCount];
                    noteCount++;
                    weightSum += weight;

                    var lengthSum = prevNote.length;
                    var deltaSlideSum = Mathf.Abs(prevNote.pitchDelta);
                    if (deltaSlideSum <= CHEESABLE_THRESHOLD)
                        deltaSlideSum *= .35f;
                    while (prevNote.isSlider)
                    {
                        if (j-- <= 0)
                            break;
                        prevNote = noteList[j];
                        nextNote = noteList[j + 1];

                        if (prevNote.pitchDelta == 0)
                            lengthSum += prevNote.length * .85f;
                        else
                        {
                            var deltaSlide = Mathf.Abs(prevNote.pitchDelta);
                            lengthSum += prevNote.length;
                            if (deltaSlide <= CHEESABLE_THRESHOLD)
                                deltaSlide *= .25f;
                            deltaSlideSum += deltaSlide;
                        }

                    }
                    var deltaTime = nextNote.position - prevNote.position;

                    if (deltaSlideSum != 0)
                    {
                        //Acc Calc
                        aimStrain += CalcAccStrain(lengthSum, deltaSlideSum, weight);
                        aimEndurance += CalcAccEndurance(lengthSum, deltaSlideSum, weight);
                    }

                    //Aim Calc
                    var aimDistance = Mathf.Abs(nextNote.pitchStart - prevNote.pitchEnd);
                    var noteMoved = aimDistance != 0 || deltaSlideSum != 0;

                    if (noteMoved)
                    {
                        aimStrain += CalcAimStrain(aimDistance, weight, deltaTime);
                        aimEndurance += CalcAimEndurance(aimDistance, weight, deltaTime);
                    }

                    //Tap Calc
                    var tapDelta = nextNote.position - prevNote.position;

                    tapStrain += CalcTapStrain(tapDelta, weight, aimDistance);
                    tapEndurance += CalcTapEndurance(tapDelta, weight, aimDistance);
                }
                aimStrain = ComputeStrain(aimStrain) / AIM_DIV;
                tapStrain = ComputeStrain(tapStrain) / TAP_DIV;
                if (i > 0)
                {
                    var endDivider = 61f - Mathf.Min(currentNote.position - noteList[i - 1].position, 5f) * 12f;
                    var aimThreshold = Mathf.Sqrt(aimStrain) * 3f;
                    var tapThreshold = Mathf.Sqrt(tapStrain) * 3f;
                    if (aimEndurance >= aimThreshold)
                        ComputeEnduranceDecay(ref aimEndurance, (aimEndurance - aimThreshold) / endDivider);
                    if (tapEndurance >= tapThreshold)
                        ComputeEnduranceDecay(ref tapEndurance, (tapEndurance - tapThreshold) / endDivider);
                }

                aimPerfDict[speedIndex].Add(new DataVector(currentNote.position, aimStrain, aimEndurance, weightSum));
                tapPerfDict[speedIndex].Add(new DataVector(currentNote.position, tapStrain, tapEndurance, weightSum));
            }
            sortedAimPerfDict[speedIndex] = aimPerfDict[speedIndex].OrderBy(x => x.performance + x.endurance).ToList();
            sortedTapPerfDict[speedIndex] = tapPerfDict[speedIndex].OrderBy(x => x.performance + x.endurance).ToList();
        }
        //public static bool IsSlider(float deltaTime) => !(Mathf.Round(deltaTime, 3) > 0);

        //https://www.desmos.com/calculator/e4kskdn8mu
        public static float ComputeStrain(float strain) => a * Mathf.Pow(strain + 1, -.0325f * (float)Math.E) - a - (3f * strain) / a;
        private const float a = -90f;

        public static void ComputeEnduranceDecay(ref float endurance, float distanceFromLastNote)
        {
            endurance /= 1 + (.2f * distanceFromLastNote);
        }

        #region AIM
        public static float CalcAimStrain(float distance, float weight, float deltaTime)
        {
            var speed = (distance * .95f) / Mathf.Pow(deltaTime, 1.11f);
            return speed * weight;
        }

        public static float CalcAimEndurance(float distance, float weight, float deltaTime)
        {
            var speed = ((distance * .15f) / Mathf.Pow(deltaTime, 1.05f)) / (AIM_END * MUL_END);
            return speed * weight;
        }
        #endregion

        #region TAP
        public static float CalcTapStrain(float tapDelta, float weight, float aimDistance)
        {
            var baseValue = Mathf.Min(Utils.Lerp(7f, 12f, aimDistance / CHEESABLE_THRESHOLD), 14f);
            return (baseValue / Mathf.Pow(tapDelta, 1.45f)) * weight;
        }

        public static float CalcTapEndurance(float tapDelta, float weight, float aimDistance)
        {
            var baseValue = Mathf.Min(Utils.Lerp(.1f, .32f, aimDistance / CHEESABLE_THRESHOLD), .35f);
            return (baseValue / Mathf.Pow(tapDelta, 1.07f)) / (TAP_END * MUL_END) * weight;
        }
        #endregion

        #region ACC
        public static float CalcAccStrain(float lengthSum, float slideDelta, float weight)
        {
            var speed = (slideDelta * 1.25f) / Mathf.Pow(lengthSum, 1.25f);
            return speed * weight;
        }

        public float CalcAccEndurance(float lengthSum, float slideDelta, float weight)
        {
            var speed = ((slideDelta * .5f) / Mathf.Pow(lengthSum, 1.05f)) / (ACC_END * MUL_END);
            return speed * weight;
        }
        #endregion

        public void Calculate(int speedIndex, List<Note> noteList, float songLengthMult)
        {
            CalculatePerformances(speedIndex, noteList);
            CalculateAnalytics(speedIndex, songLengthMult);
            CalculateRatings(speedIndex);
        }

        public void CalculateAnalytics(int speedIndex, float songLengthMult = 1f)
        {
            tapAnalyticsDict[speedIndex] = new DataVectorAnalytics(tapPerfDict[speedIndex], songLengthMult);
            aimAnalyticsDict[speedIndex] = new DataVectorAnalytics(aimPerfDict[speedIndex], songLengthMult);
        }

        public const float BIAS = .75f;

        public void CalculateRatings(int speedIndex)
        {
            var aimRating = aimRatingDict[speedIndex] = aimAnalyticsDict[speedIndex].perfWeightedAverage + 0.01f;
            var tapRating = tapRatingDict[speedIndex] = tapAnalyticsDict[speedIndex].perfWeightedAverage + 0.01f;

            if (aimRating != 0 && tapRating != 0)
            {
                var totalRating = aimRating + tapRating;
                var aimPerc = aimRating / totalRating;
                var tapPerc = tapRating / totalRating;
                var aimWeight = (aimPerc + BIAS) * AIM_WEIGHT;
                var tapWeight = (tapPerc + BIAS) * TAP_WEIGHT;
                var totalWeight = aimWeight + tapWeight;
                starRatingDict[speedIndex] = ((aimRating * aimWeight) + (tapRating * tapWeight)) / totalWeight;
            }
            else
                starRatingDict[speedIndex] = 0f;
        }

        public float GetDynamicAimRating(float percent, float speed) => GetDynamicSkillRating(percent, speed, sortedAimPerfDict);
        public float GetDynamicTapRating(float percent, float speed) => GetDynamicSkillRating(percent, speed, sortedTapPerfDict);

        private float GetDynamicSkillRating(float percent, float speed, List<DataVector>[] skillRatingMatrix)
        {
            var index = (int)((speed - 0.5f) / .25f);

            if (skillRatingMatrix[index].Count <= 1 || percent <= 0)
                return 0;
            else if (speed % .25f == 0)
                return CalcSkillRating(percent, skillRatingMatrix[index]);

            var r1 = CalcSkillRating(percent, skillRatingMatrix[index]);
            var r2 = CalcSkillRating(percent, skillRatingMatrix[index + 1]);

            var minSpeed = Utils.GAME_SPEED[index];
            var maxSpeed = Utils.GAME_SPEED[index + 1];
            var by = (speed - minSpeed) / (maxSpeed - minSpeed);
            return Utils.Lerp(r1, r2, by);
        }

        public const float MAP = .05f;
        public const float MACC = .5f;

        private float CalcSkillRating(float percent, List<DataVector> skillRatingArray)
        {
            int maxRange;
            if (percent <= MACC)
                maxRange = (int)Mathf.Clamp(skillRatingArray.Count * (percent * (MAP / MACC)), 1, skillRatingArray.Count);
            else
                maxRange = (int)Mathf.Clamp(skillRatingArray.Count * ((percent - MACC) * ((1f-MAP)/(1f-MACC)) + MAP), 1, skillRatingArray.Count);

            var array = skillRatingArray.GetRange(0, maxRange);
            var analytics = new DataVectorAnalytics(array, DiffCalcGlobals.selectedChart.songLengthMult);
            return analytics.perfWeightedAverage + .01f;
        }

        public const float AIM_WEIGHT = 1.25f;
        public const float TAP_WEIGHT = 1.12f;

        public static readonly float[] HDWeights = { .33f, .02f };
        public static readonly float[] FLWeights = { .55f, .02f };
        public static readonly float[] EZWeights = { -.55f, -.02f };

        public float GetDynamicDiffRating(float percent, float gamespeed, string[] modifiers = null)
        {
            var aimRating = GetDynamicAimRating(percent, gamespeed);
            var tapRating = GetDynamicTapRating(percent, gamespeed);
            

            if (aimRating == 0 && tapRating == 0) return 0f;

            if (modifiers != null)
            {
                var aimPow = 1f;
                var tapPow = 1f;
                var isEZModeOn = modifiers.Contains("EZ");
                var mult = isEZModeOn ? .5f : 1f;
                if (modifiers.Contains("HD"))
                {
                    aimPow += HDWeights[0] * mult;
                    tapPow += HDWeights[1] * mult;
                }
                if (modifiers.Contains("FL"))
                {
                    aimPow += FLWeights[0] * mult;
                    tapPow += FLWeights[1] * mult;
                }
                if (isEZModeOn)
                {
                    aimPow += EZWeights[0];
                    tapPow += EZWeights[1];
                }

                if (aimPow <= 0) aimPow = .01f;
                if (tapPow <= 0) tapPow = .01f;

                aimRating *= aimPow;
                tapRating *= tapPow;
            }
            var totalRating = aimRating + tapRating;
            var aimPerc = aimRating / totalRating;
            var tapPerc = tapRating / totalRating;
            var aimWeight = (aimPerc + BIAS) * AIM_WEIGHT;
            var tapWeight = (tapPerc + BIAS) * TAP_WEIGHT;
            var totalWeight = aimWeight + tapWeight;

            return ((aimRating * aimWeight) + (tapRating * tapWeight)) / totalWeight;
        }

        public void Dispose()
        {
            aimPerfDict = null;
            sortedAimPerfDict = null;
            aimAnalyticsDict = null;
            aimRatingDict = null;
            tapPerfDict = null;
            sortedTapPerfDict = null;
            tapAnalyticsDict = null;
            tapRatingDict = null;
            starRatingDict = null;
        }

        public float GetDiffRating(float speed)
        {
            var index = (int)((speed - 0.5f) / .25f);
            if (speed % .25f == 0)
                return starRatingDict[index];

            var minSpeed = Utils.GAME_SPEED[index];
            var maxSpeed = Utils.GAME_SPEED[index + 1];
            var by = (speed - minSpeed) / (maxSpeed - minSpeed);
            return Utils.Lerp(starRatingDict[index], starRatingDict[index + 1], by);
        }

        public struct DataVector
        {
            public float performance;
            public float endurance;
            public float time;
            public float weight;

            public DataVector(float time, float performance, float endurance, float weight)
            {
                this.time = time;
                this.endurance = endurance;
                this.performance = performance;
                this.weight = weight;
            }
        }

        public struct DataVectorAnalytics
        {
            public float perfMax, perfWeightedAverage;
            public float weightSum;

            public DataVectorAnalytics(List<DataVector> dataVectorList, float songLengthMult)
            {
                perfMax = perfWeightedAverage = 0;
                weightSum = 1;

                if (dataVectorList.Count <= 0) return;

                CalculateWeightSum(dataVectorList, songLengthMult);
                CalculateData(dataVectorList);
            }

            public void CalculateWeightSum(List<DataVector> dataVectorList, float songLengthMult)
            {
                for(int i = 0; i < dataVectorList.Count; i++)
                    weightSum += dataVectorList[i].weight;
                weightSum *= songLengthMult;
            }

            public void CalculateData(List<DataVector> dataVectorList)
            {
                for (int i = 0; i < dataVectorList.Count; i++)
                {
                    if (dataVectorList[i].performance > perfMax)
                        perfMax = dataVectorList[i].performance;

                    perfWeightedAverage += (dataVectorList[i].performance + dataVectorList[i].endurance) * (dataVectorList[i].weight / weightSum);
                }
            }
        }
        public static float BeatToSeconds2(float beat, float bpm) => 60f / bpm * beat;

    }

}
