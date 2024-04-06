using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TootTallyDiffCalcLibs
{
    public static class DiffCalcGlobals
    {
        public static Chart selectedChart;
        public static Action<Chart> OnSelectedChartSetEvent;
    }
}
