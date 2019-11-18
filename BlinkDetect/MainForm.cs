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
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV.UI;
using Timer = System.Timers.Timer;


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

        private CircularImagesQueue imagesCircQ;

        private Image<Bgr, byte> currentFrame;
        private Image<Bgr, byte> processedFrame;
        private Image<Gray, byte> processedResizedFrame;
        private Emgu.CV.VideoCapture capture;

        private Image<Bgr, byte> darkImage;
        CircularQueue<double> eyeRatios = new CircularQueue<double>((int)(SettingsHolder.Instance.FPS));

        ConcurrentQueue<double> FPSque = new ConcurrentQueue<double>();

        private EyeWatcher eyeWatcher;

        [DllImport("msvcrt.dll", SetLastError = false)]
        static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);

        private System.Windows.Forms.Timer updateUiTimer = new System.Windows.Forms.Timer();

        public MainForm()
        {
            InitializeComponent();
            InitObjects();
            //Application.Idle += OnApplicationOnIdle;
            updateUiTimer.Interval = 100;
            updateUiTimer.Tick += OnApplicationOnIdle;
            updateUiTimer.Start();
        }

        private void InitObjects()
        {
            capture = new VideoCapture(0, VideoCapture.API.DShow);
            capture.SetCaptureProperty(CapProp.FrameWidth, 640);
            capture.SetCaptureProperty(CapProp.FrameHeight, 480);
            capture.SetCaptureProperty(CapProp.Fps, SettingsHolder.Instance.FPS);
            capture.ImageGrabbed += Capture_ImageGrabbed;
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
            int numOfElem = eyeRatios.GetLength();
            double avrg = 0;
            if (numOfElem >= 2)
            {
                avrg = cExtMethods.CalcEarStatistics(eyeRatios);
                label3.Text = avrg.ToString("F2");
                if (eyeWatcher.TestIfAlarmNeeded())
                {
                    AlertService.SetAlarm();
                }
            }

            else if (numOfElem == 0)
            {
                label3.Text = "N/A";
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

        private bool IsRunning = false;
        private void Button1_Click(object sender, EventArgs e)
        {
            if (!IsRunning)
            {
                tokenSource = new CancellationTokenSource();

                currentFrame = new Image<Bgr, byte>(capture.Width, capture.Height);
                processedFrame = new Image<Bgr, byte>(capture.Width, capture.Height);
                processedResizedFrame = new Image<Gray, byte>(capture.Width / 2, capture.Height / 2);
                //imagesQueue = new ConcurrentQueue<Image<Bgr, byte>>();
                //imagesCircQueue = new CircularQueue<Image<Bgr, byte>>(10);
                imagesCircQ = new CircularImagesQueue(10, new Size(capture.Width, capture.Height));

                oImageUtils = new ImageUtils();


                capture.Start();

                darkImage = SaveDarkFieldImage("darkfield.bmp");
                oImageUtils.SetDarkFieldImage(darkImage);
                swGlobal = Stopwatch.StartNew();
                StartImageProcessing();
                button1.Text = "Stop";
                IsRunning = true;

                AlertService.Init();
                eyeWatcher = new EyeWatcher(ref eyeRatios, 1000);
            }
            else
            {
                tokenSource.Cancel();
                IsRunning = false;
                button1.Text = "Start";
                capture.Stop();
            }

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

            Image<Bgr, byte> imgForDiplay = new Image<Bgr, byte>(new Size((int)(processedFrame.Width * (1 + dScale)), (int)(processedFrame.Height)));
            List<ImageUtils.FilterAction> actFilters = new List<ImageUtils.FilterAction>();

            actFilters.Add(oImageUtils.ClaheImprove);
            actFilters.Add(oImageUtils.AverageImprove);
            actFilters.Add(oImageUtils.DarkImageCorrection);
            actFilters.Add(oImageUtils.HSVImprove);
            int iFrameCount = 0;


            Array2D<byte> dlibArrForEyesDetection = new Array2D<byte>((int)(processedFrame.Width * dScale), (int)(processedFrame.Height * dScale));

            while (!tokenSource.Token.IsCancellationRequested)
            {
                iFrameCount++;
                swGlobal.Restart();

                if (false)
                {
                    iFilterMethod = GetFilterIndex(iFrameCount, 200, actFilters.Count);
                }
                else
                {
                    iFilterMethod = 0;
                }

                if (imagesCircQ.GetTail(ref processedFrame))
                {
                    System.Drawing.Point[][] eyes = new System.Drawing.Point[2][];
                    bool IsDetected = false;
                    double EAR = 0;
                    //CvInvoke.Resize(processedFrame, processedResizedFrame, processedResizedFrame.Size);
                    processedResizedFrame = processedFrame.Resize(dScale, Inter.Linear).Convert<Gray, byte>();

                    IImage temp = (IImage)processedResizedFrame;

                    actFilters[iFilterMethod](ref temp);

                    processedResizedFrame = (Image<Gray, byte>)temp;

                    CopyBitmapToArray2D(ref dlibArrForEyesDetection, processedResizedFrame);
                    oImageUtils.DetectEyes(dlibArrForEyesDetection, ref eyes, ref IsDetected);

                    if (IsDetected)
                    {
                        oImageUtils.ScaleEyes(ref eyes, dScale);
                        EAR = oImageUtils.CalculateEar(eyes);
                        eyeRatios.Enqueue(EAR);
                        eyeWatcher.EARAdded();
                        //double avrgEAR = cExtMethods.CalcEarStatistics(eyeRatios);
                        processedFrame.DrawPolyline(eyes[0], true, new Bgr(Color.Blue), 2, LineType.FourConnected);
                        processedFrame.DrawPolyline(eyes[1], true, new Bgr(Color.Blue), 2, LineType.FourConnected);
                    }

                    swGlobal.Stop();
                    FPSque.Enqueue(1000F / swGlobal.ElapsedMilliseconds);

                    imgForDiplay = processedFrame.ConcateHorizontal(processedResizedFrame.Convert<Bgr, byte>());
                    imageBox1.Image = imgForDiplay;
                }

            }

        }

        private int GetFilterIndex(int iFilter, int FramesPerMethod, int MethodCount)
        {
            return (iFilter / FramesPerMethod) % MethodCount;
        }

        private Image<Bgr, byte> SaveDarkFieldImage(string darkfieldBmpPath)
        {
            int NumOfImages = SettingsHolder.Instance.FPS;
            Image<Bgr, byte>[] imgsToSum = new Image<Bgr, byte>[NumOfImages];
            Image<Bgr, byte> result;
            if (!File.Exists(darkfieldBmpPath))
            {
                int ii = 0;
                while (ii < 20)
                {
                    areGetNewImage.WaitOne();
                    imagesCircQ.GetTail(ref processedFrame);
                    if (processedFrame != null)
                    {
                        imgsToSum[ii] = processedFrame;
                        ii++;
                    }
                }

                result = new Image<Bgr, byte>(processedFrame[0].Size);
                for (ii = 0; ii < NumOfImages; ii++)
                {
                    result += imgsToSum[ii] / NumOfImages;
                }
                result.Save(darkfieldBmpPath);
            }
            else
            {
                result = CvInvoke.Imread(darkfieldBmpPath).ToImage<Bgr, byte>();
            }
            return result;
        }

        private void Capture_ImageGrabbed(object sender, EventArgs e)
        {
            VideoCapture capture = (sender as VideoCapture);
            if (capture != null)
            {

                capture.Retrieve(currentFrame);
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

    internal class EyeWatcher
    {
        private Thread thrTestEyeRatioThread;
        private AutoResetEvent arCheck;
        private CircularQueue<double> _eyeRatiosQue;
        private CancellationToken cancelToken;
        private int iMilliToWait;
        private CircularQueue<long> _blinks;
        private Stopwatch swMillisecondsFromStart;
        private const double msToSecconds=1000;
        public EyeWatcher(ref CircularQueue<double> eyes, int milisec)
        {
            arCheck = new AutoResetEvent(false);
            swMillisecondsFromStart = Stopwatch.StartNew();
            iMilliToWait = milisec;
            _eyeRatiosQue = eyes;
            _blinks=new CircularQueue<long>(SettingsHolder.Instance.NumberOfBlinksToAlarm);
            thrTestEyeRatioThread = new Thread(TestEyeRatios)
            { IsBackground = true, Name = "TestEyeRatioThread" };
            thrTestEyeRatioThread.Start();
            
        }

        public void EARAdded()
        {
            arCheck.Set();
        }
        private void TestEyeRatios()
        {
            long lastBlinkTime;
            long firstBlinkTime;
            bool bBlinkTriggered = false;
            while (!cancelToken.IsCancellationRequested)
            {
                arCheck.WaitOne();
                if (_eyeRatiosQue.GetLength() >= _eyeRatiosQue.Length)
                {
                    double averageEAR = cExtMethods.CalcEarStatistics(_eyeRatiosQue);
                    double lastEAR;
                    _eyeRatiosQue.GetTail(out lastEAR);
                    if (lastEAR < 0.6 * averageEAR & bBlinkTriggered==false)
                    {
                        bBlinkTriggered = true;

                        
                    }
                    else if (lastEAR >= 0.6 * averageEAR & bBlinkTriggered==true)
                    {
                        bBlinkTriggered = false;
                        _blinks.Enqueue(swMillisecondsFromStart.ElapsedMilliseconds);
                        if (_blinks.GetLength() > SettingsHolder.Instance.NumberOfBlinksToAlarm - 1)
                        {
                            firstBlinkTime = _blinks.GetHead();
                            _blinks.GetTail(out lastBlinkTime);
                            if ((lastBlinkTime - firstBlinkTime) / 1000D < SettingsHolder.Instance.NumberOfSeccondsToAlarm)
                            {
                                AlertService.SetAlarm();
                            }


                        }
                    }

                }
                

            }

        }

        public bool TestIfAlarmNeeded()
        {
            return false;
        }

        public void CloseAlertService()
        {
            CancellationTokenSource.CreateLinkedTokenSource(cancelToken).Cancel();


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
                    if (m_Head != -1)
                    {
                        m_Tail = m_NextWrite;
                        if (m_Head == m_Tail)
                            m_Head = mod(m_Tail + 1, m_Buffer.Length);
                        if (m_CurrRead == m_Tail)
                            m_CurrRead = -1;
                    }
                    else //initial state
                    {
                        m_Head = 0;
                        m_Tail = 0;
                        m_CurrRead = 0;
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
        int m_NextWrite, m_Tail, m_Head, m_CurrRead, ielem = 0;

        ReaderWriterLockSlim slimLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        public CircularQueue(int length)
        {
            m_Buffer = new T[length];
            m_NextWrite = 0;
            m_Head = m_Tail = m_CurrRead = -1;
        }

        public T peekAt(int elemNum)
        {
            return m_Buffer[elemNum];
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
            if (ielem < m_Buffer.Length)
            {
                ielem++;
            }
            m_NextWrite = mod(m_NextWrite + 1, m_Buffer.Length);
        }

        public int GetLength()
        {
            return ielem;

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

    public static class AlertService
    {
        private static SerialPort spAlert;
        private static Thread thrRing;
        private static AutoResetEvent arRingAllow;
        private static CancellationToken cancelToken;
        private static int iLengthAlarmMilisec = 100;

        public static bool Init()
        {

            bool bRes = false;
            try
            {
                thrRing = new Thread(Alarm);
                thrRing.Start();
                cancelToken = new CancellationToken(false);
                arRingAllow = new AutoResetEvent(false);
                spAlert = new SerialPort(SettingsHolder.Instance.comPort, 9600, Parity.None, 8, StopBits.One);
                spAlert.Open();
                spAlert.Write(new byte[] { 0xA0, 0x01, 0x01, 0xA2 }, 0, 4);

                Thread.Sleep(20);

                spAlert.Write(new byte[] { 0xA0, 0x01, 0x00, 0xA1 }, 0, 4);
                bRes = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return bRes;
        }

        public static void SetAlarm(int miliseconds)
        {
            iLengthAlarmMilisec = miliseconds;
            arRingAllow.Set();
        }

        public static void SetAlarm()
        {
            iLengthAlarmMilisec = SettingsHolder.Instance.NumberOfmsBuzzer;
            arRingAllow.Set();
        }

        private static void Alarm()
        {
            while (!cancelToken.IsCancellationRequested)
            {
                arRingAllow.WaitOne();
                spAlert.Write(new byte[] { 0xA0, 0x01, 0x01, 0xA2 }, 0, 4);
                Thread.Sleep(iLengthAlarmMilisec);
                spAlert.Write(new byte[] { 0xA0, 0x01, 0x00, 0xA1 }, 0, 4);

            }
        }

        public static void CloseAlertService()
        {
            CancellationTokenSource.CreateLinkedTokenSource(cancelToken).Cancel();

            spAlert.Close();
            spAlert.Dispose();
        }
    }
    public class CircularImagesQueueGray
    {
        Image<Gray, byte>[] m_Buffer;
        int m_NextWrite, m_Tail, m_Head, m_CurrRead;
        readonly object _Locker = new object();

        public CircularImagesQueueGray(int length, Size imgSize)
        {
            m_Buffer = new Image<Gray, byte>[length];
            m_NextWrite = 0;
            m_Head = m_Tail = m_CurrRead = -1;
            for (int ii = 0; ii < m_Buffer.Length; ii++)
            {
                m_Buffer[ii] = new Image<Gray, byte>(imgSize);
            }
        }

        public int Length
        {
            get { return m_Buffer.Length; }
        }

        public void Enqueue(Image<Gray, byte> o)
        {
            bool _entered = Monitor.TryEnter(_Locker, TimeSpan.FromMilliseconds(500));
            if (_entered)
            {
                try
                {
                    if (m_Head != -1)
                    {
                        m_Tail = m_NextWrite;
                        if (m_Head == m_Tail)
                            m_Head = mod(m_Tail + 1, m_Buffer.Length);
                        if (m_CurrRead == m_Tail)
                            m_CurrRead = -1;
                    }
                    else //initial state
                    {
                        m_Head = 0;
                        m_Tail = 0;
                        m_CurrRead = 0;
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

        public bool GetTail(ref Image<Gray, byte> item)
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

        public Image<Gray, byte> GetHead()
        {
            if (m_Head == -1)
                return null;

            m_CurrRead = m_Head;
            return m_Buffer[m_Head];
        }

        public Image<Gray, byte> GetNext()
        {
            if (m_CurrRead == -1 || m_CurrRead == m_Tail)
                return null;

            m_CurrRead = mod(m_CurrRead + 1, m_Buffer.Length);

            return m_Buffer[m_CurrRead];
        }

        public Image<Gray, byte> GetPrev()
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
}



