using System;
using System.Collections.Generic;
using DlibDotNet;
using DlibDotNet.ImageDatasetMetadata;

namespace ImageUtils
{

    public static class ImageUtils
    {

        public static void Detect(Array2D<RgbPixel> image)
        {


            using (var detector = Dlib.GetFrontalFaceDetector())
            using (var sp =
                ShapePredictor.Deserialize(
                    @"C:\Users\Felix\source\repos\BlinkDetect\External\shape_predictor_68_face_landmarks.dat"))
            {
                var dets = detector.Operator(image);
                var shapes = new List<FullObjectDetection>();
                foreach (var rect in dets)
                {
                    var shape = sp.Detect(image, rect);
                    Console.WriteLine($"number of parts: {shape.Parts}");
                    if (shape.Parts > 2)
                    {
                        Console.WriteLine($"pixel position of first part:  {shape.GetPart(0)}");
                        Console.WriteLine($"pixel position of second part: {shape.GetPart(1)}");
                        shapes.Add(shape);
                    }
                    var chipLocations = Dlib.GetFaceChipDetails(shapes);
                }
            }

        }
    }


}
