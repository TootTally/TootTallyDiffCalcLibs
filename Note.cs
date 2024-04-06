using System;

namespace TootTallyDiffCalcLibs
{
    public struct Note
    {
        public int count;
        public float pitchStart, pitchDelta, pitchEnd;
        public float position, length;
        public bool isSlider;

        public Note(int count, float position, float length, float pitchStart, float pitchDelta, float pitchEnd, bool isSlider)
        {
            this.count = count;
            this.position = position;
            this.length = length;
            this.pitchStart = pitchStart;
            this.pitchDelta = pitchDelta;
            this.pitchEnd = pitchEnd;
            this.isSlider = isSlider;
        }

    }
}
