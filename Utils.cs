﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TootTallyDiffCalcLibs
{
    public static class Utils
    {
        public static readonly float[] GAME_SPEED = { .5f, .75f, 1f, 1.25f, 1.5f, 1.75f, 2f };

        public static float Lerp(float firstFloat, float secondFloat, float by) //Linear easing
        {
            return firstFloat + (secondFloat - firstFloat) * by;
        }

        public static float FastPow(double num, int exp)
        {
            double result = 1.0;
            while (exp > 0)
            {
                if (exp % 2 == 1)
                    result *= num;
                exp >>= 1;
                num *= num;
            }
            return (float)result;
        }

        //TT for S rank (60% score)
        //https://www.desmos.com/calculator/rhwqyp21nr
        public static float CalculateBaseTT(float starRating)
        {
            return (0.5f * FastPow(starRating, 2) + (7f * starRating) + 0.05f);
            //y = (0.7x^2 + 12x + 0.05)/1.5
        }

        public static float CalculateScoreTT(Chart chart, float replaySpeed, int hitCount, int noteCount, float percent, string[] modifiers = null) =>
            CalculateBaseTT(chart.GetDynamicDiffRating(replaySpeed, Mathf.Min(hitCount * 1.1f / noteCount, 1f), modifiers)) * GetMultiplier(percent, modifiers);

        public static float CalculateScoreTT(float[] diffRatings, float replaySpeed, float percent, string[] modifiers = null) =>
            CalculateBaseTT(LerpDiff(diffRatings, replaySpeed)) * GetMultiplier(percent, modifiers);

        //OLD: https://www.desmos.com/calculator/6rle1shggs
        public static readonly Dictionary<float, float> accToMultDict = new Dictionary<float, float>()
        {
            { 1f, 40.2f },
            { .999f, 32.4f },
            { .996f, 27.2f },
            { .993f, 23.2f },
            { .99f, 20.5f },
            { .985f, 18.1f },
            { .98f, 16.1f },
            { .97f, 13.8f },
            { .96f, 11.8f },
            { .95f, 10.8f },
            { .925f, 9.6f },
            { .9f, 8.9f },
            { .875f, 8.3f },
            { .85f, 7.7f },
            { .8f, 6.6f },
            { .7f, 4.4f },
            { .6f, 2.4f },
            { .5f, 1.2f },
            { .25f, 0.5f },
            { 0, 0 }
        };

        public static readonly Dictionary<float, float> ezAccToMultDict = new Dictionary<float, float>()
        {
             { 1f, 15.2f },
             { .999f, 11.2f },
             { .996f, 10.8f },
             { .993f, 10.4f },
             { .99f, 10f },
             { .985f, 9.6f },
             { .98f, 9.3f },
             { .97f, 8.9f },
             { .96f, 8.6f },
             { .95f, 8.3f },
             { .925f, 7.8f },
             { .9f, 7.3f },
             { .875f, 6.9f },
             { .85f, 6.5f },
             { .8f, 5.25f },
             { .7f, 3.25f },
             { .6f, 1.75f },
             { .5f, .6f },
             { .25f, .03f },
             { 0, 0 },
        };

        public static float GetMultiplier(float percent, string[] modifiers = null)
        {
            var multDict = (modifiers != null && modifiers.Contains("EZ")) ? ezAccToMultDict : accToMultDict;
            int index;
            for (index = 1; index < multDict.Count && multDict.Keys.ElementAt(index) > percent; index++) ;
            var percMax = multDict.Keys.ElementAt(index);
            var percMin = multDict.Keys.ElementAt(index - 1);
            var by = (percent - percMin) / (percMax - percMin);
            return Lerp(multDict[percMin], multDict[percMax], by);
        }

        public static float LerpDiff(float[] diffRatings, float speed)
        {
            var index = (int)((speed - 0.5f) / .25f);
            if (speed % .25f == 0)
                return diffRatings[index];

            var minSpeed = GAME_SPEED[index];
            var maxSpeed = GAME_SPEED[index + 1];
            var by = (speed - minSpeed) / (maxSpeed - minSpeed);
            return Lerp(diffRatings[index], diffRatings[index + 1], by);
        }
    }
}
