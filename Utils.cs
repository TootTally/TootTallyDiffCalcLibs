using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Analytics;

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

        public static float CalculateScoreTT(Chart chart, float replaySpeed, int hitCount, int noteCount, float percent, string[] modifiers = null) =>
            chart.GetDynamicTTRating(replaySpeed, (float)hitCount / noteCount, GetMultiplier(percent, modifiers), modifiers);

        //OLD: https://www.desmos.com/calculator/6rle1shggs
        public static readonly Dictionary<float, float> accToEZMultDict = new Dictionary<float, float>()
        {
            { 1f, 1f },//{ 1f, 1f },
            { .999f, .999f },//{ .999f, .999f },
            { .996f, .985f },//{ .996f, .98f },
            { .993f, .98f },//{ .993f, .96f },
            { .99f, .975f },//{ .99f, .93f },
            { .985f, .96f },//{ .985f, .9f },
            { .98f, .94f },//{ .98f, .875f },
            { .97f, .9f },//{ .97f, .835f },
            { .96f, .86f },//{ .96f, .8f },
            { .95f, .82f },//{ .95f, .765f },
            { .925f, .75f },//{ .925f, .7f },
            { .9f, .69f },//{ .9f, .645f },
            { .875f, .64f },//{ .875f, .59f },
            { .85f, .6f },//{ .85f, .55f },
            { .8f, .52f },//{ .8f, .55f },
            { .7f, .39f },//{ .7f, .45f },
            { .6f, .29f },//{ .6f, .4f },
            { .5f, .22f },//{ .5f, .35f },
            { .25f, .1f },//{ .25f, .2f },
            { 0, 0 },//{ 0, 0 },
        };

        public static readonly Dictionary<float, float> accToMultDict = new Dictionary<float, float>()
        {
            { 1f, 1.9f },
            { .999f, 1.8f },
            { .996f, 1.65f },
            { .993f, 1.5f },
            { .99f, 1.35f },
            { .985f, 1.25f },
            { .98f, 1.15f },
            { .97f, 1f },
            { .96f, .9f },
            { .95f, .8f },
            { .925f, .7f },
            { .9f, .625f },
            { .875f, .565f },
            { .85f, .52f },
            { .8f, .45f },
            { .7f, .33f },
            { .6f, .25f },
            { .5f, .2f },
            { .25f, .125f },
            { 0, 0 },
        };

        public static float GetMultiplier(float percent, string[] modifiers = null)
        {
            var multDict = (modifiers != null && (modifiers.Contains("EZ") || modifiers.Contains("AP"))) ? accToEZMultDict : accToMultDict;
            int index;
            for (index = 1; index < multDict.Count && multDict.Keys.ElementAt(index) > percent; index++) ;
            var percMax = multDict.Keys.ElementAt(index);
            var percMin = multDict.Keys.ElementAt(index - 1);
            var by = (percent - percMin) / (percMax - percMin);
            var mult = Utils.Lerp(multDict[percMin], multDict[percMax], by);
            var nmAPMult = (modifiers != null && modifiers.Contains("AP") && !modifiers.Contains("EZ")) ? 1.2f : 1f;
            return mult * nmAPMult;
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
