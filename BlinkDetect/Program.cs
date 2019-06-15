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
        public const string versionNumber = "1.0.0.1";
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
                            instance.prop1 = 1.0;
                            instance.prop2 = 2;
                            instance.prop3 = 3.0F;
                            
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
        [DisplayName("Property 1")]
        [ReadOnly(false)]
        [Description("Property 1 description")]
        public double prop1 { get; set; }

        [Category("1. General Properties")]
        [DisplayName("Property 1")]
        [ReadOnly(false)]
        [Description("Property 2 description")]
        public int prop2 { get; set; }

        
        [Category("1. General Properties")]
        [ReadOnly(false)]
        [DisplayName("Property 3")]
        [Description("Property 3 description")]
        public float prop3 { get; set; }

        


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
