using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ookii.Dialogs.WinForms;

namespace BlinkDetect
{
    static class Program
    {
        public const string versionNumber = "1.0.2.2";
        public const string version = "BlinkDetect: " + versionNumber;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class SettingsHolder : IDisposable
    {

        private static SettingsHolder instance;
        private static object syncroot = new Object();
        private int _numberOfBlinksToAlarm;
        private int _numberOfSeccondsToAlarm;
        private int _durationOfAlarm;
        private int _numOfFramesForAverage;
        private int _imageProcessingMethod;

        public static SettingsHolder Instance
        {
            get
            {
                // If the instance is null then create one
                if (instance == null)
                {
                    lock (syncroot)
                    {
                        if (instance == null)
                        {
                            instance = new SettingsHolder();
                            instance.comPort = "COM7";
                            instance.FPS = 15;
                            instance._durationOfAlarm = 20;
                            instance._numberOfBlinksToAlarm = 5;
                            instance._numberOfSeccondsToAlarm = 10;
                            instance._numOfFramesForAverage = 5;
                            instance._imageProcessingMethod = 0;
                        }
                    }
                }
                return instance;
            }

        }



        private SettingsHolder()
        {

        }


        [Category("1. General Properties")]
        [DisplayName("Com port")]
        [ReadOnly(false)]
        [Description("Buzzer relay com port")]
        public string comPort { get; set; }

        [Category("2. Image processing Properties")]
        [DisplayName("FPS")]
        [ReadOnly(false)]
        [Description("Required camera FPS")]
        public int FPS { get; set; }

        [Category("2. Image processing Properties")]
        [ReadOnly(false)]
        [DisplayName("Number Of Frames For Average")]
        [Description("Number Of frames to average")]
        public int NumberOfFramesForAvrg
        {
            get
            {
                return _numOfFramesForAverage;
            }
            set
            {
                _numOfFramesForAverage = value;
            }
        }

        [Category("2. Image processing Properties")]
        [ReadOnly(false)]
        [DisplayName("Image processing method")]
        [Description(" Clahe=0 \r\n Average=1 \r\n Clahe And Average=2 \r\n Dark noise correction=3 \r\n HSV=4")]
        public int ImageProcessingMethod
        {
            get
            {
                return _imageProcessingMethod;
            }
            set
            {
                _imageProcessingMethod = value;
            }
        }


        [Category("3. Alarm Properties")]
        [ReadOnly(false)]
        [DisplayName("Number of Blinks")]
        [Description("Number of blinks in X secconds to trigger alarm")]
        public int NumberOfBlinksToAlarm
        {
            get
            {
                return _numberOfBlinksToAlarm;
            }
            set
            {
                _numberOfBlinksToAlarm = value;
            }
        }

        [Category("3. Alarm Properties")]
        [ReadOnly(false)]
        [DisplayName("Number of secconds")]
        [Description("Number of secconds to test for blinks")]
        public int NumberOfSeccondsToAlarm
        {
            get
            {
                return _numberOfSeccondsToAlarm;
            }
            set
            {
                _numberOfSeccondsToAlarm = value;
            }
        }

        [Category("3. Alarm Properties")]
        [ReadOnly(false)]
        [DisplayName("Alarm duration in ms ")]
        [Description("Number of milisecconds to buzz the alarm")]
        public int NumberOfmsBuzzer
        {
            get
            {
                return _durationOfAlarm;
            }
            set
            {
                _durationOfAlarm = value;
            }
        }



        public void Dispose()
        {
            lock (syncroot)
            {
                instance = null;
            }
        }

        internal class myFileBrowser : UITypeEditor
        {
            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return UITypeEditorEditStyle.Modal;
            }

            public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
            {
                using (Ookii.Dialogs.WinForms.VistaFileDialog ofd = new VistaOpenFileDialog())
                {
                    string[] s1Descript = context.PropertyDescriptor.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    ofd.Filter = @"|*.csv";

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        return ofd.FileName;
                    }
                }
                return value;

            }
        }
    }
}
