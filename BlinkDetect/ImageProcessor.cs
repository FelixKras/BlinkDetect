using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DlibDotNet;
using Emgu.CV;
using Emgu.CV.Plot;
using Emgu.CV.Structure;
using Point = System.Drawing.Point;

namespace BlinkDetect
{
    public  class ImageUtils
    {
        private  ShapePredictor sp;
        private  FrontalFaceDetector detector;

        public ImageUtils()
        {
            detector = Dlib.GetFrontalFaceDetector();
            sp =
                ShapePredictor.Deserialize(
                    @"C:\Users\Felix\source\repos\BlinkDetect\External\shape_predictor_68_face_landmarks.dat");
        }

        public void DetectEyes(Array2D<byte> image, ref System.Drawing.Point[][] eyes,ref bool IsDetected)
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
                    IsDetected=true;
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
            var rightDx = DistancebtwPoints(eyes[1][0],  eyes[1][3]);


            return ((leftDy1+leftDy2)/2F/(leftDx)+ (rightDy1 + rightDy2) / 2F / (rightDx))/2;
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

        public void ImproveImage(ref Image<Gray, byte> processedResizedFrame,ImproveMethods enmImprove)
        {
            switch (enmImprove)
            {
                case ImproveMethods.Clahe:
                    Image<Gray, byte> afterCLAHE = new Image<Gray, byte>(processedResizedFrame.Size);
                    CvInvoke.CLAHE(processedResizedFrame, 40, new Size(8, 8), afterCLAHE);
                    processedResizedFrame = afterCLAHE;
                    break;
                default:
                    break;
            }
            
        }
    }
}
