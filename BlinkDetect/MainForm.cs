using DlibDotNet;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;


namespace BlinkDetect
{
    public enum ImproveMethods
    {
        Averaging,
        Clahe,
        AveragingAndClahe,
        ClaheAndAveraging,
        HSVandClahe
    }
    public partial class MainForm : Form
    {

        private ImageUtils oImageUtils;
        private Stopwatch swGlobal;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private AutoResetEvent areGetNewImage = new AutoResetEvent(false);

        private ConcurrentQueue<Image<Bgr, byte>> imagesQueue;
        private Image<Bgr, Byte> currentFrame;
        private Image<Bgr, Byte> processedFrame;
        private Image<Gray, Byte> processedResizedFrame;

        ConcurrentQueue<double> eyeRatios = new ConcurrentQueue<double>();
        ConcurrentQueue<double> FPSque = new ConcurrentQueue<double>();

        [DllImport("msvcrt.dll", SetLastError = false)]
        static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
        public MainForm()
        {
            InitializeComponent();
            Application.Idle += OnApplicationOnIdle;
        }

        private void OnApplicationOnIdle(object sender, EventArgs e)
        {
            if (eyeRatios.Count >= 2)
            {
                label3.Text = eyeRatios.Average().ToString("F2");
            }

            if (FPSque.Count >= 2)
            {
                while (FPSque.Count > 20)
                {
                    double dtemp;
                    FPSque.TryDequeue(out dtemp);
                }
                label1.Text = FPSque.Average().ToString("F2");
            }
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingsForm setForm = new SettingsForm();
            setForm.Visible = true;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            Emgu.CV.VideoCapture capture = new VideoCapture(0, VideoCapture.API.DShow);
            capture.SetCaptureProperty(CapProp.FrameWidth, 640);
            capture.SetCaptureProperty(CapProp.FrameHeight, 480);
            currentFrame = new Image<Bgr, byte>(capture.Width, capture.Height);
            processedFrame = new Image<Bgr, byte>(capture.Width, capture.Height);
            processedResizedFrame = new Image<Gray, byte>(capture.Width / 2, capture.Height / 2);
            imagesQueue = new ConcurrentQueue<Image<Bgr, byte>>();
            oImageUtils = new ImageUtils();
            capture.ImageGrabbed += Capture_ImageGrabbed;
            capture.Start();
            StartImageProcessing();
            swGlobal = Stopwatch.StartNew();
        }

        private void StartImageProcessing()
        {
            Thread processThread = new Thread(ImageProcessing);
            processThread.IsBackground = true;
            processThread.Start();
        }


        private void ImageProcessing()
        {
            double dScale = 0.5;
            while (!tokenSource.Token.IsCancellationRequested)
            {
                swGlobal.Restart();
                //areGetNewImage.WaitOne();
                if (imagesQueue.TryDequeue(out processedFrame))
                {
                    System.Drawing.Point[][] eyes = new System.Drawing.Point[2][];
                    bool IsDetected = false;
                    double EAR = 0;
                    processedResizedFrame = processedFrame.Resize(dScale, Inter.Linear).Convert<Gray, byte>();
                    processedResizedFrame._EqualizeHist();

                    var a1 = new Array2D<byte>(processedResizedFrame.Width, processedResizedFrame.Height);
                    oImageUtils.ImproveImage(ref processedResizedFrame, ImproveMethods.Averaging);
                    CopyBitmapToArray2D(ref a1, processedResizedFrame);
                    oImageUtils.DetectEyes(a1, ref eyes, ref IsDetected);

                    if (IsDetected)
                    {
                        oImageUtils.ScaleEyes(ref eyes, dScale);
                        EAR = oImageUtils.CalculateEar(eyes);
                        eyeRatios.Enqueue(EAR);
                        double avrgEAR = CalcEarStatistics(eyeRatios);
                        processedFrame.DrawPolyline(eyes[0], true, new Bgr(Color.Blue), 2, LineType.FourConnected);
                        processedFrame.DrawPolyline(eyes[1], true, new Bgr(Color.Blue), 2, LineType.FourConnected);
                    }
                    swGlobal.Stop();
                    FPSque.Enqueue(1000F / swGlobal.ElapsedMilliseconds);
                    //processedFrame.Draw((1000F / swGlobal.ElapsedMilliseconds).ToString("F2") + " fps", new System.Drawing.Point(10, 20), FontFace.HersheyPlain, 2, new Bgr(Color.Red));
                    //processedFrame.Draw("Ear: + " + EAR.ToString("F2"), new System.Drawing.Point(10, 40), FontFace.HersheyPlain, 2, new Bgr(Color.Red));
                    imageBox1.Image = processedFrame.ConcateHorizontal(processedResizedFrame.Convert<Bgr, byte>());
                }

            }

        }

        private double CalcEarStatistics(ConcurrentQueue<double> doubles)
        {
            double result;
            while (doubles.Count > 5)
            {
                double dtemp;
                doubles.TryDequeue(out dtemp);
            }

            if (doubles.Count < 5)
            {
                result = doubles.Last();
            }
            else
            {
                result = doubles.Average();
                if (result < 0.2)
                {

                }
            }

            return result;

        }


        private void Capture_ImageGrabbed(object sender, EventArgs e)
        {
            VideoCapture capture = (sender as VideoCapture);
            if (capture != null)
            {
                capture.Retrieve(currentFrame);
                imagesQueue.Enqueue(currentFrame);
                areGetNewImage.Set();
            }


        }

        private void CopyBitmapToArray2D(ref Array2D<byte> a1, Image<Gray, byte> frameBitmap)
        {
            try
            {
                byte[] array = new byte[frameBitmap.Mat.Width * frameBitmap.Mat.Height * frameBitmap.Mat.ElementSize];
                Marshal.Copy(frameBitmap.Mat.DataPointer, array, 0, array.Length);
                //memcpy(a1., frameBitmap.Mat.DataPointer, array.Length);
                a1 = Dlib.LoadImageData<byte>(array, (uint)frameBitmap.Mat.Rows,
                    (uint)frameBitmap.Mat.Cols,
                   (uint)(frameBitmap.Mat.Width * frameBitmap.Mat.ElementSize));
            }
            finally
            {

            }

        }
    }
}
