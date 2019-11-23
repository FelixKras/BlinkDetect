using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public static void Raise(this EventHandler handler, object sender, EventArgs args = null)
        {
            EventHandler localHandlerCopy = handler;
            if (args == null)
            {
                args = EventArgs.Empty;
            }
            if (localHandlerCopy != null)
            {
                localHandlerCopy(sender, args);
            }
        }

        public static void InvokeIfRequired(this ISynchronizeInvoke obj, MethodInvoker action)
        {
            if (obj.InvokeRequired)
            {
                object[] args = new object[0];
                obj.Invoke(action, args);
            }
            else
            {
                action();
            }
        }
    }
}
