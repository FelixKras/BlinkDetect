using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DlibDotNet;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Plot;
using Emgu.CV.Structure;
using Point = System.Drawing.Point;

namespace BlinkDetect
{
    public class ImageUtils
    {
        private ShapePredictor sp;
        private FrontalFaceDetector detector;

        private Image<Gray, byte>[] imgsForAverage = new Image<Gray, byte>[SettingsHolder.Instance.NumberOfFramesForAvrg];
        private int indxForAverage = 0;
        private Image<Gray, byte> darkImage;

        public delegate void FilterAction(ref IImage b);

        
        public ImageUtils()
        {
            detector = Dlib.GetFrontalFaceDetector();
            sp =
                ShapePredictor.Deserialize(
                    @"C:\Users\Felix\source\repos\BlinkDetect\External\shape_predictor_68_face_landmarks.dat");
        }

        public void DetectEyes(Array2D<byte> image, ref System.Drawing.Point[][] eyes, ref bool IsDetected)
        {
            //ImageWindow win = new ImageWindow(image);
            //win.Show();

            var dets = detector.Operator(image);

            eyes[0] = new Point[6];
            eyes[1] = new Point[6];
            if (dets.Length > 0)
            {
                var shape = sp.Detect(image, dets[0]);
                if (shape.Parts > 60)
                {
                    for (int ii = 0; ii < 6; ii++)
                    {
                        var temp = shape.GetPart(36 + (uint)ii);
                        eyes[0][ii] = new Point(temp.X, temp.Y);

                        temp = shape.GetPart(42 + (uint)ii);
                        eyes[1][ii] = new Point(temp.X, temp.Y);
                    }
                    IsDetected = true;
                    //var chipLocations = Dlib.GetFaceChipDetails(shapes);
                }
                else
                {
                    IsDetected = false;
                }



            }
        }

        private double DistancebtwPoints(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.X - p2.X, 2));
        }
        public double CalculateEar(Point[][] eyes)
        {
            var leftDy1 = DistancebtwPoints(eyes[0][1], eyes[0][5]);
            var leftDy2 = DistancebtwPoints(eyes[0][2], eyes[0][4]);
            var leftDx = DistancebtwPoints(eyes[0][0], eyes[0][3]);

            var rightDy1 = DistancebtwPoints(eyes[1][1], eyes[1][5]);
            var rightDy2 = DistancebtwPoints(eyes[1][2], eyes[1][4]);
            var rightDx = DistancebtwPoints(eyes[1][0], eyes[1][3]);


            return ((leftDy1 + leftDy2) / 2F / (leftDx) + (rightDy1 + rightDy2) / 2F / (rightDx)) / 2;
        }

        public void ScaleEyes(ref Point[][] eyes, double dScale)
        {
            for (int i = 0; i < eyes.Length; i++)
            {
                for (int j = 0; j < eyes[i].Length; j++)
                {
                    eyes[i][j] = new Point((int)(eyes[i][j].X / dScale), (int)(eyes[i][j].Y / dScale));
                }

            }
        }

      

        
        
        private Image<Gray, byte> result;

        public void AverageImprove(ref IImage processedResizedFrame)
        {
            if ((processedResizedFrame as Image<Gray, byte>) == null)
            {


            }
            else
            {
                if (result == null)
                {
                    result =new Image<Gray, byte>(processedResizedFrame.Size);
                }
                else
                {
                    result.SetValue(new Gray(0));
                }

                imgsForAverage[indxForAverage] = (Image<Gray, byte>)processedResizedFrame;
                indxForAverage = (indxForAverage + 1) % imgsForAverage.Length;

                for (int i = 0; i < imgsForAverage.Length; i++)
                {
                    
                    if (imgsForAverage[i] != null)
                    {
                        result += imgsForAverage[i] / 5;
                    }
                }
                processedResizedFrame = result;
            }

        }

        public void ClaheImprove(ref IImage processedResizedFrame)
        {
            
            if (result == null)
            {
                result = new Image<Gray, byte>(processedResizedFrame.Size);
            }

            CvInvoke.CLAHE(processedResizedFrame, 40, new Size(8, 8), result);
            processedResizedFrame = result;
        }

        public void SetDarkFieldImage(IImage darkImage)
        {
            if ((darkImage as Image<Bgr, byte>) == null)
            {
                this.darkImage = ((Image<Bgr, byte>)darkImage).Convert<Gray, byte>();

            }
            else
            {
                this.darkImage = new Image<Gray, byte>(new Size(1920, 1080));
            }


        }

        private Image<Hsv, byte> HSVresult;
        public void HSVImprove(ref IImage processedResizedFrame)
        {
            if ((processedResizedFrame as Image<Hsv, byte>) == null)
            {
            }
            else
            {
                if (HSVresult == null)
                {
                    HSVresult = new Image<Hsv, byte>(processedResizedFrame.Size);
                }
                CvInvoke.CvtColor(processedResizedFrame, HSVresult, ColorConversion.Bgr2Hsv);
                IImage HSVonly = HSVresult[2];
                ClaheImprove(ref HSVonly);
                processedResizedFrame = HSVonly;
            }



        }

        public void DarkImageCorrection(ref IImage processedResizedFrame)
        {
            if ((processedResizedFrame as Image<Gray, byte>) == null)
            {
            }
            else
            {
                if (processedResizedFrame.Size != darkImage.Size)
                {
                    darkImage = darkImage.Resize(processedResizedFrame.Size.Width, processedResizedFrame.Size.Height, Inter.Cubic);
                }

                Image<Gray, byte> temp = (Image<Gray, byte>)processedResizedFrame;
                temp -= darkImage;
                processedResizedFrame = temp;
            }


        }

        public void ClaheAndAvrgImprove(ref IImage processedResizedFrame)
        {
            if ((processedResizedFrame as Image<Gray, byte>) == null)
            {


            }
            else
            {
                if (result == null)
                {
                    result = new Image<Gray, byte>(processedResizedFrame.Size);
                }
                else
                {
                    result.SetValue(new Gray(0));
                }

                imgsForAverage[indxForAverage] = (Image<Gray, byte>)processedResizedFrame;
                indxForAverage = (indxForAverage + 1) % imgsForAverage.Length;

                for (int i = 0; i < imgsForAverage.Length; i++)
                {

                    if (imgsForAverage[i] != null)
                    {
                        result += imgsForAverage[i] / SettingsHolder.Instance.NumberOfFramesForAvrg;
                    }
                }
                processedResizedFrame = result;
            }
            
            CvInvoke.CLAHE(processedResizedFrame, 40, new Size(8, 8), result);
            processedResizedFrame = result;
        }

    }

    public class ImgPool
    {
        private readonly ConcurrentDictionary<int, Image<Bgr,byte>> _objects;
        private List<int> _rentedBitmaps;
        private List<int> _freeBitmaps;
        private int _width, _height;
        
        public ImgPool(int Width, int Height, int MaxObjCount)
        {
            _objects = new ConcurrentDictionary<int, Image<Bgr, byte>>();
            _rentedBitmaps = new List<int>();
            _freeBitmaps = new List<int>();
            _height = Height;
            _width = Width;

            for (int ii = 0; ii < MaxObjCount; ii++)
            {
                _objects.TryAdd(ii, new Image<Bgr, byte>(_width, _height));
                _freeBitmaps.Add(ii);
            }
        }

        public Image<Bgr, byte> GetObject()
        {
            Image<Bgr, byte> item;
            if (_freeBitmaps.Count > 0)
            {
                if (_objects.TryGetValue(_freeBitmaps[0], out item))
                {
                    _rentedBitmaps.Add(_freeBitmaps[0]);
                    _freeBitmaps.RemoveAt(0);
                }
                else
                {
                    item = new Image<Bgr, byte>(_width, _height);

                    //create more or reclaim _rented
                }
            }
            else
            {
                List<int> temps;
                temps = _freeBitmaps;
                _freeBitmaps = _rentedBitmaps;
                _rentedBitmaps = temps;
                _objects.TryGetValue(_freeBitmaps[0], out item);

            }
            return item;
        }


    }
}
