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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV.UI;


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

        private ConcurrentQueue<Image<Bgr, byte>> concurrImagesQueue;
        private CircularQueue<Image<Bgr, byte>> imagesCircQueue;
        private CircularImagesQueue imagesCircQ;

        private Image<Bgr, Byte> currentFrame;
        private Image<Bgr, Byte> processedFrame;
        private Image<Gray, Byte> processedResizedFrame;


        private Image<Bgr, byte> darkImage;
        ConcurrentQueue<double> eyeRatios = new ConcurrentQueue<double>();
        ConcurrentQueue<double> FPSque = new ConcurrentQueue<double>();

        [DllImport("msvcrt.dll", SetLastError = false)]
        static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);

        public MainForm()
        {
            InitializeComponent();
            Application.Idle += OnApplicationOnIdle;
        }

        string[] MethodNames = new string[]
        {
            "Clahe",
            "Average",
            "DarkImageCorrection",
            "HSV"
        };

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

            label5.Text = MethodNames[iFilterMethod];


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
            //imagesQueue = new ConcurrentQueue<Image<Bgr, byte>>();
            //imagesCircQueue = new CircularQueue<Image<Bgr, byte>>(10);
            imagesCircQ = new CircularImagesQueue(10, new Size(capture.Width, capture.Height));
            oImageUtils = new ImageUtils();
            capture.ImageGrabbed += Capture_ImageGrabbed;
            capture.Start();

            darkImage = SaveDarkFieldImage("darkfield.bmp");
            oImageUtils.SetDarkFieldImage(darkImage);
            StartImageProcessing();
            swGlobal = Stopwatch.StartNew();
        }

        private void StartImageProcessing()
        {
            Thread processThread = new Thread(ImageProcessing);
            processThread.IsBackground = true;
            processThread.Start();
        }

        private int iFilterMethod;

        private void ImageProcessing()
        {
            double dScale = 0.5;

            Image<Bgr,byte> imgForDiplay=new Image<Bgr, byte>(new Size((int)(processedFrame.Width * (1+dScale)), (int)(processedFrame.Height)));
            List<ImageUtils.FilterAction> actFilters = new List<ImageUtils.FilterAction>();
            actFilters.Add(oImageUtils.ClaheImprove);
            actFilters.Add(oImageUtils.AverageImprove);
            actFilters.Add(oImageUtils.DarkImageCorrection);
            actFilters.Add(oImageUtils.HSVImprove);
            int iFilter = 0;
            var a1 = new Array2D<byte>((int)(processedFrame.Width*dScale), (int)(processedFrame.Height* dScale));

            while (!tokenSource.Token.IsCancellationRequested)
            {
                swGlobal.Restart();
                //areGetNewImage.WaitOne();
                if (imagesCircQ.GetTail(ref processedFrame))//imagesCircQueue.GetTail(out processedFrame)) //imagesQueue.TryDequeue(out processedFrame))
                {
                    System.Drawing.Point[][] eyes = new System.Drawing.Point[2][];
                    bool IsDetected = false;
                    double EAR = 0;
                    processedResizedFrame = processedFrame.Resize(dScale, Inter.Linear).Convert<Gray, byte>();
                    //processedResizedFrame._EqualizeHist();
                    //oImageUtils.ImproveImage(ref processedResizedFrame, ImproveMethods.Averaging);

                    var temp = (IImage)processedResizedFrame;
                    iFilter++;
                    iFilterMethod = (iFilter / 200) % actFilters.Count;
                    //oImageUtils.ImproveImage(ref temp, actFilters);
                    actFilters[iFilterMethod](ref temp);
                    processedResizedFrame = (Image<Gray, byte>)temp;

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
                    
                    imgForDiplay = processedFrame.ConcateHorizontal(processedResizedFrame.Convert<Bgr, byte>()); 
                    imageBox1.Image = imgForDiplay;
                }

            }

        }

        private Image<Bgr, byte> SaveDarkFieldImage(string darkfieldBmpPath)
        {
            const int cnstNumOfImages = 20;
            Image<Bgr, byte>[] imgsToSum = new Image<Bgr, byte>[cnstNumOfImages];
            Image<Bgr, byte> result;
            if (!File.Exists(darkfieldBmpPath))
            {
                int ii = 0;
                while (ii < 20)
                {
                    if (concurrImagesQueue.TryDequeue(out processedFrame))
                    {
                        imgsToSum[ii] = processedFrame;
                        ii++;
                    }
                }

                result = new Image<Bgr, byte>(processedFrame[0].Size);
                for (ii = 0; ii < cnstNumOfImages; ii++)
                {
                    result += imgsToSum[ii] / cnstNumOfImages;
                }

                result.Save(darkfieldBmpPath);
            }
            else
            {

                result = CvInvoke.Imread(darkfieldBmpPath).ToImage<Bgr, byte>();
            }

            return result;

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
                //imagesQueue.Enqueue(currentFrame);
                //imagesCircQueue.Enqueue(currentFrame);
                imagesCircQ.Enqueue(currentFrame);
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

    public class CircularImagesQueue
    {
        Image<Bgr, byte>[] m_Buffer;
        int m_NextWrite, m_Tail, m_Head, m_CurrRead;
        readonly object _Locker = new object();

        public CircularImagesQueue(int length, Size imgSize)
        {
            m_Buffer = new Image<Bgr, byte>[length];
            m_NextWrite = 0;
            m_Head = m_Tail = m_CurrRead = -1;
            for (int ii = 0; ii < m_Buffer.Length; ii++)
            {
                m_Buffer[ii] = new Image<Bgr, byte>(imgSize);
            }
        }

        public int Length
        {
            get { return m_Buffer.Length; }
        }

        public void Enqueue(Image<Bgr, byte> o)
        {
            bool _entered = Monitor.TryEnter(_Locker, TimeSpan.FromMilliseconds(500));
            if (_entered)
            {
                try
                {
                    if (m_Head == -1) // initial state
                    {
                        m_Head = 0;
                        m_Tail = 0;
                        m_CurrRead = 0;
                    }
                    else
                    {
                        m_Tail = m_NextWrite;
                        if (m_Head == m_Tail)
                            m_Head = mod(m_Tail + 1, m_Buffer.Length);
                        if (m_CurrRead == m_Tail)
                            m_CurrRead = -1;

                    }

                    o.Mat.CopyTo(m_Buffer[m_NextWrite].Mat);
                    m_NextWrite = mod(m_NextWrite + 1, m_Buffer.Length);
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    Monitor.Exit(_Locker);
                }
            }
            else
            {
                
            }
        }

        public bool GetTail(ref Image<Bgr, byte> item)
        {
            bool _entered = Monitor.TryEnter(_Locker, TimeSpan.FromMilliseconds(500));
            bool bRes;
            if (_entered)
            {
                try
                {
                    if (m_Head == -1)
                    {
                        item = null;
                        bRes = false;
                    }
                    else
                    {
                        m_CurrRead = m_Tail;
                        m_Buffer[m_Tail].Mat.CopyTo(item.Mat);
                        bRes = true;
                    }
                }
                catch (Exception ex)
                {
                    bRes = false;
                }
                finally
                {
                    Monitor.Exit(_Locker);
                }
            }
            else
            {
                bRes = false;
            }

            return bRes;
        }

        public Image<Bgr, byte> GetHead()
        {
            if (m_Head == -1)
                return null;

            m_CurrRead = m_Head;
            return m_Buffer[m_Head];
        }



        public Image<Bgr, byte> GetNext()
        {
            if (m_CurrRead == -1 || m_CurrRead == m_Tail)
                return null;

            m_CurrRead = mod(m_CurrRead + 1, m_Buffer.Length);

            return m_Buffer[m_CurrRead];
        }

        public Image<Bgr, byte> GetPrev()
        {
            if (m_CurrRead == -1 || m_CurrRead == m_Head)
                return null;

            m_CurrRead = mod(m_CurrRead - 1, m_Buffer.Length);
            return m_Buffer[m_CurrRead];
        }

        private int mod(int x, int m) // x mod m works for both positive and negative x (unlike x % m).
        {
            return (x % m + m) % m;
        }
    }

    public class CircularQueue<T>
    {
        T[] m_Buffer;
        int m_NextWrite, m_Tail, m_Head, m_CurrRead;
        ReaderWriterLockSlim slimLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        public CircularQueue(int length)
        {
            m_Buffer = new T[length];
            m_NextWrite = 0;
            m_Head = m_Tail = m_CurrRead = -1;
        }

        public int Length
        {
            get { return m_Buffer.Length; }
        }

        public void Enqueue(T o)
        {
            if (m_Head == -1) // initial state
            {
                m_Head = 0;
                m_Tail = 0;
                m_CurrRead = 0;
            }
            else
            {
                m_Tail = m_NextWrite;
                if (m_Head == m_Tail)
                    m_Head = mod(m_Tail + 1, m_Buffer.Length);
                if (m_CurrRead == m_Tail)
                    m_CurrRead = -1;
            }

            m_Buffer[m_NextWrite] = o;
            m_NextWrite = mod(m_NextWrite + 1, m_Buffer.Length);
        }

        public T GetHead()
        {
            if (m_Head == -1)
                return default(T);

            m_CurrRead = m_Head;
            return m_Buffer[m_Head];
        }

        public bool GetTail(out T item)
        {
            bool bRes = false;
            if (m_Head == -1)
            {
                item = default(T);
                bRes = false;
            }
            else
            {
                m_CurrRead = m_Tail;
                item = m_Buffer[m_Tail];
                bRes = true;
            }

            return bRes;
        }

        public T GetNext()
        {
            if (m_CurrRead == -1 || m_CurrRead == m_Tail)
                return default(T);

            m_CurrRead = mod(m_CurrRead + 1, m_Buffer.Length);
            ;
            return m_Buffer[m_CurrRead];
        }

        public T GetPrev()
        {
            if (m_CurrRead == -1 || m_CurrRead == m_Head)
                return default(T);

            m_CurrRead = mod(m_CurrRead - 1, m_Buffer.Length);
            return m_Buffer[m_CurrRead];
        }

        private int mod(int x, int m) // x mod m works for both positive and negative x (unlike x % m).
        {
            return (x % m + m) % m;
        }
    }

}



