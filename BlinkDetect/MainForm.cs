using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DlibDotNet;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;


namespace BlinkDetect
{
    public partial class MainForm : Form
    {
        
        public MainForm()
        {
            InitializeComponent();
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingsForm setForm=new SettingsForm();
            setForm.Visible = true;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Emgu.CV.VideoCapture capture = new VideoCapture(0,VideoCapture.API.DShow);
            capture.ImageGrabbed += Capture_ImageGrabbed;
            capture.Start();
        }

        private void Capture_ImageGrabbed(object sender, EventArgs e)
        {
            VideoCapture capture = (sender as VideoCapture);
            if (capture != null)
            {
                Image<Bgr, Byte> frame = new Image<Bgr, byte>(capture.Width,capture.Height);
                capture.Retrieve(frame);
                var a1 = new Array2D<RgbPixel>(capture.Width, capture.Height);
                ImageUtils.Detect(a1);
                imageBox1.Image = frame;
            }
            
            
        }
    }
}
