using Steamworks;
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
             1.0000f, 0.9000f, 0.8100f, 0.7290f, 0.6561f, 0.5905f, 0.5314f, 0.4783f,
             0.4305f, 0.3874f, 0.3487f, 0.3138f, 0.2824f, 0.2542f, 0.2288f, 0.2059f,
             0.1853f, 0.1668f, 0.1501f, 0.1351f, 0.1216f, 0.1094f, 0.0985f, 0.0887f,
             0.0798f, 0.0718f, 0.0646f, 0.0582f, 0.0524f, 0.0472f, 0.0425f, 0.0383f,
             0.0345f, 0.0311f, 0.0280f, 0.0252f, 0.0227f, 0.0204f, 0.0184f, 0.0166f,
             0.0149f, 0.0134f, 0.0121f, 0.0109f, 0.0098f, 0.0088f, 0.0079f, 0.0071f,
             0.0064f, 0.0057f, 0.0051f, 0.0046f, 0.0041f, 0.0037f, 0.0033f, 0.0030f,
             0.0027f, 0.0024f, 0.0022f, 0.0020f, 0.0018f, 0.0016f, 0.0015f, 0.0013f // :)
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

        private readonly int ALL_NOTE_COUNT, NOTE_COUNT;

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
            ALL_NOTE_COUNT = noteCount;
            NOTE_COUNT = sliderCount;
        }

        public const float AIM_DIV = 8;
        public const float TAP_DIV = 14;
        public const float ACC_DIV = 12;
        public const float MAX_DIST = 5f;
        public const int MAX_NOTE_COUNT = 16;

        public void CalculatePerformances(int speedIndex, List<Note> noteList)
        {
            float aimEnd = 0, aimSta = 0, tapEnd = 0, tapSta = 0;
            for (int i = 1; i < ALL_NOTE_COUNT; i++) //Main Forward Loop
            {
                int noteCount = 0;
                float aimStrain = 0, tapStrain = 0;
                float weightSum = 1;
                var n1Current = noteList[i];
                var n2Prev = noteList[i - 1];
                Note n1Prev = default;
                n1Prev.count = -1;
                for (int j = i - 1; j >= 0 && noteCount < MAX_NOTE_COUNT && (Mathf.Abs(n1Current.position - n2Prev.position) <= MAX_DIST || i - j <= 2); j--) //Secondary Backward Loop
                {
                    n2Prev = noteList[j];
                    var n2Next = noteList[j + 1];
                    noteCount++;
                    var weight = weights[noteCount * 2];
                    if (n2Prev.position >= n2Next.position) break;
                    var lengthSum = n2Prev.length;
                    var slideCount = 0f;
                    var slideVelocity = 0f;
                    var flatLength = 0f;
                    if (Mathf.Abs(n2Prev.pitchDelta) >= CHEESABLE_THRESHOLD / 10f)
                    {
                        slideCount++;
                        var pitchDelta = Mathf.Abs(n2Prev.pitchDelta);
                        var deltaSlide = Mathf.Sqrt(NormalizePitch(pitchDelta)) * (pitchDelta >= CHEESABLE_THRESHOLD ? .45f : .1f);
                        slideVelocity += deltaSlide / Mathf.Pow(n2Prev.length, 1.38f);
                    }
                    else
                        flatLength += n2Prev.length * .2f;
                    while (n2Prev.isSlider) //Merge all sliders into one note
                    {
                        if (j-- <= 0)
                            break;
                        n2Prev = noteList[j];
                        n2Next = noteList[j + 1];

                        lengthSum += n2Prev.length;
                        if (Mathf.Abs(n2Prev.pitchDelta) >= CHEESABLE_THRESHOLD / 10f)
                        {
                            slideCount++;
                            var pitchDelta = Mathf.Abs(n2Prev.pitchDelta);
                            var deltaSlide = Mathf.Sqrt(NormalizePitch(pitchDelta)) * (pitchDelta >= CHEESABLE_THRESHOLD ? .75f : .1f);
                            slideVelocity += deltaSlide / Mathf.Pow(n2Prev.length, 1.38f);
                        }
                        else
                            flatLength += n2Prev.length * .2f;
                    }

                    if (n1Prev.count == -1)
                        n1Prev = n2Prev;

                    //Slide
                    if (slideCount != 0)
                    {
                        slideVelocity /= slideCount;
                        aimStrain += (slideVelocity * weight) / ACC_DIV;
                        aimStrain *= (lengthSum - flatLength) / lengthSum;
                    }

                    //Aim
                    var deltaTime = n2Next.position - n2Prev.position;
                    var aimDistance = Mathf.Abs(NormalizePitch(n2Next.pitchStart - n2Prev.pitchEnd));
                    if (aimDistance != 0)
                    {
                        var currVelocity = (Mathf.Sqrt(aimDistance) * .45f) / Mathf.Pow(deltaTime, 1.32f);
                        aimStrain += (currVelocity * weight) / AIM_DIV;
                    }

                    //Tap
                    var baseValue = (Mathf.Sqrt(aimDistance) / 15f) + .075f;
                    tapStrain += ((baseValue / Mathf.Pow(deltaTime, 1.39f)) * weight) / TAP_DIV;
                    weightSum += weight; 
                }

                var tapDelta = Mathf.Sqrt(n1Current.position - n1Prev.position);

                tapSta = ComputeStamina(tapStrain * 1.85f, tapSta, tapDelta);
                tapEnd = ComputeEndurance(tapSta * 1.55f, tapEnd, tapDelta);

                aimSta = ComputeStamina(aimStrain * .55f, aimSta, tapDelta);
                aimEnd = ComputeEndurance(aimSta * 1.55f, aimEnd, tapDelta);

                aimPerfDict[speedIndex].Add(new DataVector(n1Current.position, aimStrain, aimSta, aimEnd, weightSum));
                tapPerfDict[speedIndex].Add(new DataVector(n1Current.position, tapStrain, tapSta, tapEnd, weightSum));
            }
            sortedAimPerfDict[speedIndex] = aimPerfDict[speedIndex].OrderBy(x => x.strain + x.stamina + x.endurance).ToList();
            sortedTapPerfDict[speedIndex] = tapPerfDict[speedIndex].OrderBy(x => x.strain + x.stamina + x.endurance).ToList();
        }
        //public static bool IsSlider(float deltaTime) => !(Mathf.Round(deltaTime, 3) > 0);

        private const float PLAY_AREA_RANGE = 360;
        public static float NormalizePitch(float pitch) => pitch / PLAY_AREA_RANGE;


        //https://www.desmos.com/calculator/e4kskdn8mu

        public static float ComputeVelocityDebuff(float lastVelocity, float currentVelocity) => Mathf.Min(Mathf.Abs(currentVelocity - lastVelocity) * .03f + .45f, 1f);

        const float STA_RISE_RATE = 1.45f;
        const float STA_DECAY_RATE = .25f;
        const float STA_DIV = 5f;
        const float END_RISE_RATE = .15f;
        const float END_DECAY_RATE = .15f;
        const float END_DIV = 25f;

        public static float ComputeStamina(float strain, float stamina, float tapDelta)
        {
            return stamina + ((strain - stamina) / STA_DIV) * ((strain > stamina) ?
                                              1f - Mathf.Pow((float)Math.E, -STA_RISE_RATE * tapDelta) :
                                              1f - Mathf.Pow((float)Math.E, -STA_DECAY_RATE * tapDelta));
            //return newStam < 0 ? 0 : newStam;
        }
        public static float ComputeEndurance(float stamina, float endurance, float tapDelta)
        {
            return endurance + ((stamina - endurance) / END_DIV) * ((stamina > endurance) ?
                                              1f - Mathf.Pow((float)Math.E, -END_RISE_RATE * tapDelta) :
                                              1f - Mathf.Pow((float)Math.E, -END_DECAY_RATE * tapDelta));
            //return newEnd < 0 ? 0 : newEnd;
        }

        public void Calculate(int speedIndex, List<Note> noteList)
        {
            CalculatePerformances(speedIndex, noteList);
            CalculateAnalytics(speedIndex);
            CalculateRatings(speedIndex);
        }

        public void CalculateAnalytics(int speedIndex)
        {
            aimAnalyticsDict[speedIndex] = new DataVectorAnalytics(aimPerfDict[speedIndex]);
            tapAnalyticsDict[speedIndex] = new DataVectorAnalytics(tapPerfDict[speedIndex]);
        }


        #region Rating Calc
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
            if (speed == 0) speed = 1f;
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
                maxRange = (int)Mathf.Clamp(skillRatingArray.Count * ((percent - MACC) * ((1f - MAP) / (1f - MACC)) + MAP), 1, skillRatingArray.Count);

            var array = skillRatingArray.GetRange(0, maxRange);
            var analytics = new DataVectorAnalytics(array);
            return analytics.perfWeightedAverage + .01f;
        }

        public const float AIM_WEIGHT = 1.25f;
        public const float TAP_WEIGHT = 1f;
        public const float BIAS = 1f;

        public static readonly float[] HDWeights = { .11f, .09f };
        public static readonly float[] FLWeights = { .12f, .1f };
        public static readonly float[] EZWeights = { -.48f, -.25f };

        public float GetDynamicDiffRating(float percent, float gamespeed, string[] modifiers = null)
        {
            var aimRating = GetDynamicAimRating(percent, gamespeed);
            var tapRating = GetDynamicTapRating(percent, gamespeed);


            if (aimRating == 0 && tapRating == 0) return 0f;

            if (modifiers != null)
            {
                var aimPow = 1f;
                var tapPow = 1f;
                var isEZModeOn = modifiers.Contains("EZ") || modifiers.Contains("AP");
                var mult = isEZModeOn ? .25f : 1f;
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

                if (modifiers.Contains("AP"))
                {
                    aimPow = 0;
                    tapRating *= .55f;
                }
                if (modifiers.Contains("RX"))
                {
                    tapPow = 0;
                    aimRating *= .55f;
                }
                if (modifiers.Contains("RK"))
                    tapRating *= .1f;

                if (aimPow < 0) aimPow = .01f;
                if (tapPow < 0) tapPow = .01f;

                aimRating *= aimPow;
                tapRating *= tapPow;
            }
            var totalRating = aimRating + tapRating;
            if (totalRating <= 0) return 0;
            var aimPerc = aimRating / totalRating;
            var tapPerc = tapRating / totalRating;
            var aimWeight = (aimPerc + BIAS) * AIM_WEIGHT;
            var tapWeight = (tapPerc + BIAS) * TAP_WEIGHT;
            var totalWeight = aimWeight + tapWeight;

            return ((aimRating * aimWeight) + (tapRating * tapWeight)) / totalWeight;
        }
        #endregion

        #region tt calc
        public float GetDynamicAimTT(float percent, float speed) => GetDynamicTTRating(percent, speed, sortedAimPerfDict);
        public float GetDynamicTapTT(float percent, float speed) => GetDynamicTTRating(percent, speed, sortedTapPerfDict);

        private float GetDynamicTTRating(float percent, float speed, List<DataVector>[] skillRatingMatrix)
        {
            if (speed == 0) speed = 1f;
            var index = (int)((speed - 0.5f) / .25f);
            if (skillRatingMatrix[index].Count <= 1 || percent <= 0)
                return 0;
            else if (speed % .5f == 0)
                return CalcTTRating(percent, skillRatingMatrix[index]);

            var r1 = CalcTTRating(percent, skillRatingMatrix[index]);
            var r2 = CalcTTRating(percent, skillRatingMatrix[index + 1]);

            var minSpeed = Utils.GAME_SPEED[index];
            var maxSpeed = Utils.GAME_SPEED[index + 1];
            var by = (speed - minSpeed) / (maxSpeed - minSpeed);
            return Utils.Lerp(r1, r2, by);
        }

        private float CalcTTRating(float percent, List<DataVector> skillRatingArray)
        {
            int maxRange;

            if (percent <= MACC)
                maxRange = (int)Mathf.Clamp(skillRatingArray.Count * (percent * (MAP / MACC)), 1, skillRatingArray.Count);
            else
                maxRange = (int)Mathf.Clamp(skillRatingArray.Count * ((percent - MACC) * ((1f - MAP) / (1f - MACC)) + MAP), 1, skillRatingArray.Count);

            var array = skillRatingArray.GetRange(0, maxRange);
            var analytics = new DataVectorAnalytics(array);
            return analytics.sumTT + .01f;
        }

        public float GetDynamicTTRating(float percent, float gamespeed, float multiplier, string[] modifiers = null)
        {
            var aimTT = GetDynamicAimTT(percent, gamespeed);
            var tapTT = GetDynamicTapTT(percent, gamespeed);

            if (aimTT == 0 && tapTT == 0) return 0f;

            if (modifiers != null)
            {
                var aimPow = 1f;
                var tapPow = 1f;
                var isEZModeOn = modifiers.Contains("EZ") || modifiers.Contains("AP");
                var mult = isEZModeOn ? .25f : 1f;
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

                if (modifiers.Contains("AP"))
                {
                    aimPow = 0;
                    tapTT *= .55f;
                }
                if (modifiers.Contains("RX"))
                {
                    tapPow = 0;
                    aimTT *= .55f;
                }
                if (modifiers.Contains("RK"))
                    tapTT *= .1f;

                if (aimPow < 0) aimPow = .01f;
                if (tapPow < 0) tapPow = .01f;



                aimTT *= aimPow;
                tapTT *= tapPow;
            }

            var totalRating = aimTT + tapTT;
            if (totalRating <= 0) return 0;
            var aimPerc = aimTT / totalRating;
            var tapPerc = tapTT / totalRating;
            var aimWeight = (aimPerc + BIAS) * AIM_WEIGHT;
            var tapWeight = (tapPerc + BIAS) * TAP_WEIGHT;
            var totalWeight = aimWeight + tapWeight;


            return multiplier * ((aimTT * aimWeight) + (tapTT * tapWeight)) / totalWeight;
        }

        #endregion

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

        public struct DataVector(float time, float strain, float stamina, float endurance, float weight)
        {
            public float time = time;
            public float stamina = stamina;
            public float endurance = endurance;
            public float strain = strain;
            public float weight = weight;
        }

        public struct DataVectorAnalytics
        {
            public float perfMax, perfSum, perfWeightedAverage;
            public float weightSum;
            public float sumTT;
            public const float STAR_MULT = 4f;

            public DataVectorAnalytics(List<DataVector> dataVectorList)
            {
                perfMax = perfWeightedAverage = 0;
                weightSum = 200;
                sumTT = 0;

                if (dataVectorList.Count <= 0) return;

                CalculateWeightSum(dataVectorList);
                CalculateData(dataVectorList);
            }

            public void CalculateWeightSum(List<DataVector> dataVectorList)
            {
                for (int i = 0; i < dataVectorList.Count; i++)
                    weightSum += dataVectorList[i].weight;
            }

            public void CalculateData(List<DataVector> dataVectorList)
            {
                for (int i = 0; i < dataVectorList.Count; i++)
                {
                    var weight = dataVectorList[i].weight / weightSum;
                    var perf = dataVectorList[i].strain + dataVectorList[i].stamina + dataVectorList[i].endurance;
                    if (perfMax < perf)
                        perfMax = perf;
                    perfSum += perf * weight * STAR_MULT;
                    sumTT += CalcStrainTT(dataVectorList[i].strain * weight) + CalcStamTT(dataVectorList[i].stamina * weight) + CalcEnduTT(dataVectorList[i].endurance * weight);
                }
                perfWeightedAverage = perfSum;
            }

            public static float CalcStrainTT(float performance) => performance * 850f;
            public static float CalcStamTT(float stamina) => stamina * 375f;
            public static float CalcEnduTT(float endurance) => endurance * 375f;
        }
        public static float BeatToSeconds2(float beat, float bpm) => 60f / bpm * beat;

    }

}
