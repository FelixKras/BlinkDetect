using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlinkDetect
{
    static class cExtMethods
    {
        public static double CalcEarStatistics(CircularQueue<double> doubles)
        {
            double avrg = 0;
            int numOfElem = doubles.Length;
            for (int ii = 0; ii < numOfElem; ii++)
            {
                avrg += doubles.peekAt(ii) / numOfElem;
            }
            return avrg;
        }
    }
}
