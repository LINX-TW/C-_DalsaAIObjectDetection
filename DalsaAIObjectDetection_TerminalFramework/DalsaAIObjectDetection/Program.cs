using System;
using DALSA.SaperaProcessing.CProAi;
using DALSA.SaperaProcessing.CPro;
using System.Collections.Generic;
using DALSA.SaperaLT.SapClassBasic;

namespace DalsaAIObjectDetection
{
    class Program
    {
        static CProAiInferenceObjectDetection m_inferenceEngine = new CProAiInferenceObjectDetection();
        static CProAiInference.ModelAttributes m_modelAttributes = new CProAiInference.ModelAttributes();

        public class Result
        {
            public Result()
            {
                profilerTime = 0.0f;
            }

            public class ObjectInfo
            {
                public ObjectInfo()
                {
                    referenceClassIndex = 0;
                    confidenceScore = 0.0f;
                }

                public uint referenceClassIndex;
                public CProRect boundingBox;
                public float confidenceScore;
            };

            public CProImage proImage;
            public List<ObjectInfo> objectInfo = new List<ObjectInfo>();
            public float profilerTime;
        };

        const double OneGigabyte = 1024.0 * 1024.0 * 1024.0;

        static void Main(string[] args)
        {
            CProImage image = new CProImage();
            image.Load("C:\\Program Files\\Teledyne DALSA\\Sapera Processing\\Images\\AI\\hardware\\obj0000.jpg");
            string modelFilename = "C:\\Program Files\\Teledyne DALSA\\Sapera Processing\\Images\\AI\\hardware\\Hardware.mod";
            int result = 0;

            // Load AI Object Detection
            result += AI_ObjectDetection_Load(modelFilename);
            if (result == 0)
            {
                Console.WriteLine("***Load AI Object Detection PASS.***");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("***Load AI Object Detection FAIL.***");
                Console.WriteLine();
                Console.ReadLine(); //Pause;
                Environment.Exit(1);
            }

            // Build AI Object Detection
            result += AI_ObjectDetection_Build();
            if (result == 0)
            {
                Console.WriteLine("***Build AI Object Detection PASS.***");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("***Build AI Object Detection FAIL.***");
                Console.WriteLine();
                Console.ReadLine(); //Pause;
                Environment.Exit(1);
            }

            // Process AI Object Detection
            Result ImagesResult = new Result();
            result += AI_ObjectDetection_Process(image, ref ImagesResult);
            if (result == 0)
            {
                Console.WriteLine("***Process AI Object Detection PASS.***");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("***Process AI Object Detection FAIL.***");
                Console.WriteLine();
                Console.ReadLine(); //Pause;
                Environment.Exit(1);
            }

            // Draw the result image 
            CProImage DestinationImage = new CProImage();
            DrawResult(image, ref DestinationImage, ref ImagesResult);

            string outputFile = "Output_Result.bmp";
            DestinationImage.Save(outputFile, CProImage.FileType.FileBmp);  //Save Inference Result

            Console.ReadLine();
        }

        //#*****************************************************************************
        //# Function   : DrawResult
        //# Description : Draw the bounding box and class in the distnation image.
        //# Inputs : CProImage image, ref CProImage DestinationImage, ref Result ImagesResult
        //# Outputs : Pass : return 0 ; Fail : return 1
        //# Notice : None
        //#*****************************************************************************
        static void DrawResult(CProImage SourceImage, ref CProImage DestinationImage, ref Result ImagesResult)
        {
            // Get the source image's width and height.
            int width = SourceImage.Width;
            int height = SourceImage.Height;

            //// Set the Drowing Parameters.
            IntPtr sourcePtr = SourceImage.GetData();
            IntPtr[] destIntPtr = new IntPtr[1];
            destIntPtr[0] = sourcePtr;

            SapBuffer destBuffer = new SapBuffer(1, destIntPtr, width, height,
                SapFormat.RGB8888, SapBuffer.MemoryType.ScatterGather);

            destBuffer.Create();
            SapView m_pView = new SapView(destBuffer);
            SapGraphic m_Graphic = new SapGraphic();
            SapDataMono color = new SapDataMono((255 << 16) | (0 << 8) | 0);
            m_Graphic.Color = color;
            m_Graphic.Create();

            for (int k = 0; k < ImagesResult.objectInfo.Count; k++)
            {
                Result.ObjectInfo tempInfo = ImagesResult.objectInfo[k];

                // Set the threshold of the confidence score
                if (tempInfo.confidenceScore < 0.10)
                    continue;

                // Draw the bounding box and class name in the target buffer
                CProRect roi = new CProRect(tempInfo.boundingBox.x, tempInfo.boundingBox.y, tempInfo.boundingBox.width, tempInfo.boundingBox.height);
                m_Graphic.Rectangle(destBuffer, tempInfo.boundingBox.x, tempInfo.boundingBox.y, // Drawing
                    tempInfo.boundingBox.x + tempInfo.boundingBox.width, tempInfo.boundingBox.y + tempInfo.boundingBox.height, false);

                if (tempInfo.boundingBox.y - 18 <= 0)
                {
                    m_Graphic.Text(destBuffer
                        , tempInfo.boundingBox.x
                        , tempInfo.boundingBox.y + tempInfo.boundingBox.height
                        , m_modelAttributes.GetReferenceClassName(tempInfo.referenceClassIndex)); //Drawing Text
                }
                else
                {
                    m_Graphic.Text(destBuffer
                        , tempInfo.boundingBox.x
                        , tempInfo.boundingBox.y - 18
                        , m_modelAttributes.GetReferenceClassName(tempInfo.referenceClassIndex)); //Drawing Text
                }
            }

            SaperaBufferToCProImage(destBuffer, ref DestinationImage);
            m_pView.Create();
            m_pView.Show();

        }

        //#*****************************************************************************
        //# Function   : SaperaBufferToCProImage
        //# Description : Transfer Sapera Buffer to CProImage Buffer.
        //# Inputs : SapBuffer inBuf, ref CProImage image
        //# Outputs : Pass : return 0 ; Fail : return 1
        //# Notice : None
        //#*****************************************************************************
        static bool SaperaBufferToCProImage(SapBuffer inBuf, ref CProImage image)
        {

            int width = inBuf.Width;
            int height = inBuf.Height;


            CProData.FormatEnum format;
            switch (inBuf.Format)
            {
                case SapFormat.Mono8:
                    format = CProData.FormatEnum.FormatUByte;
                    break;
                case SapFormat.Mono16:
                    format = CProData.FormatEnum.FormatUShort;
                    break;
                case SapFormat.RGB8888:
                    format = CProData.FormatEnum.FormatRgb;
                    break;
                default:
                    format = CProData.FormatEnum.FormatUnknown;
                    break;
            };

            IntPtr sourcePtr = new IntPtr();
            inBuf.GetAddress(out sourcePtr);
            image = new CProImage(width, height, format, sourcePtr, true);

            return true;
        }

        //#*****************************************************************************
        //# Function   : AI_ObjectDetection_Process
        //# Description : Use AI Object Detection to execute the target image.
        //# Inputs : CProImage image, ref Result ImagesResults
        //# Outputs : Pass : return 0 ; Fail : return 1
        //# Notice : None
        //#*****************************************************************************
        static int AI_ObjectDetection_Process(CProImage image, ref Result ImagesResults)
        {
            Console.WriteLine("[ Start to process AI Object Detection ]");
            Result res = new Result();

            // Enable the profiler when doing the inference
            m_inferenceEngine.EnableProfiler(true);

            // Save input image
            res.proImage = image.Clone();

            // Save profiler time
            res.profilerTime = m_inferenceEngine.GetProfilerTimeInMillis(CProAiInference.ProfilerSection.All);


            // Do the inference on the input image
            if (!m_inferenceEngine.Execute(image, m_modelAttributes))
            {
                Console.WriteLine("Fail to execute inference on the image");
                return 1;
            }


            if (m_inferenceEngine.IsProfilerEnabled())
            {
                Console.WriteLine(" PreProcessing: {0} ms", m_inferenceEngine.GetProfilerTimeInMillis(CProAiInference.ProfilerSection.PreProcessing));
                Console.WriteLine(" Inference: {0} ms", m_inferenceEngine.GetProfilerTimeInMillis(CProAiInference.ProfilerSection.Inference));
                Console.WriteLine(" PostProcesssing: {0} ms", m_inferenceEngine.GetProfilerTimeInMillis(CProAiInference.ProfilerSection.PostProcessing));
                Console.WriteLine();
            }

            // Get the inference's result for object detection
            CProAiInferenceObjectDetection.Result predictionResult = m_inferenceEngine.GetResult();
            uint objectCount = predictionResult.ObjectCount;


            // Get and save the reference class index, the confidence score and the bounding box for each of them
            Console.WriteLine("Nunber of Object Count: {0}", objectCount);
            Console.WriteLine("-----------------------");
            for (uint objectIndex = 0; objectIndex < objectCount; objectIndex++)
            {
                Result.ObjectInfo objectInfo = new Result.ObjectInfo();

                objectInfo.referenceClassIndex = predictionResult.GetObjectReferenceClassIndex(objectIndex);
                objectInfo.confidenceScore = predictionResult.GetObjectConfidenceScore(objectIndex);
                objectInfo.boundingBox = predictionResult.GetObjectBoundingBox(objectIndex);
                res.objectInfo.Add(objectInfo);

                Console.WriteLine("Reference Class Index: {0}", objectInfo.referenceClassIndex);
                Console.WriteLine("Confidence Score: {0}", objectInfo.confidenceScore);
                Console.WriteLine("Bounding Box : ({0}, {1}), Width: {2} Height: {3}",
                    objectInfo.boundingBox.x, objectInfo.boundingBox.y, objectInfo.boundingBox.width, objectInfo.boundingBox.height);
                Console.WriteLine();
            }

            ImagesResults = res;
            return 0;
        }

        //#*****************************************************************************
        //# Function   : AI_ObjectDetection_Load
        //# Description : Load AI Object Detection from the modelFilename (*.mod).
        //# Inputs : string modelFilename
        //# Outputs : Pass : return 0 ; Fail : return 1
        //# Notice : None
        //#*****************************************************************************
        static int AI_ObjectDetection_Load(string modelFilename)
        {
            Console.WriteLine("[ Start to load AI Object Detection ]");

            string ModelFilename;
            CProAiInference.ModelAttributes modelAttributes = CProAiInference.GetModelAttributes(modelFilename);

            // Check Object Detection Model
            if (modelAttributes.modelType == CProAiInference.ModelType.ObjectDetection)
                ModelFilename = modelFilename;
            else
            {
                Console.WriteLine("This Model is not Object Detection Model !");
                return 1;
            }

            Console.Write("Loading the model ...");
            // Load the object detection model into the inference engine device
            if (!m_inferenceEngine.LoadModel(ModelFilename))
            {
                Console.WriteLine("Fail to load the model '{0}'", ModelFilename);
                return 1;
            }
            Console.Write(" Done");
            Console.WriteLine(" ( {0} ms)", m_inferenceEngine.GetProfilerTimeInMillis(CProAiInference.ProfilerSection.LoadModel));

            // Retrieve and display default values for object detection parameters
            int m_candidateNumberMax = m_inferenceEngine.CandidateNumberMax;
            float m_confidenceThreshold = m_inferenceEngine.ConfidenceThreshold;
            float m_overlapThreshold = m_inferenceEngine.OverlapThreshold;

            Console.WriteLine("Model Parameters");
            Console.WriteLine(" Candidate Number Max: {0}", m_candidateNumberMax);
            Console.WriteLine(" Confidence Threshold: {0}", m_confidenceThreshold);
            Console.WriteLine(" Overlap Threshold: {0}", m_overlapThreshold);
            Console.WriteLine();
            Console.WriteLine();

            // Retrieve the model attributes
            m_modelAttributes = m_inferenceEngine.GetModelAttributes();

            return 0;
        }

        //#*****************************************************************************
        //# Function   : AI_ObjectDetection_Build
        //# Description : Build AI Object Detection.
        //# Inputs : None
        //# Outputs : Pass : return 0 ; Fail : return 1
        //# Notice : None
        //#*****************************************************************************
        static int AI_ObjectDetection_Build()
        {
            Console.WriteLine("[ Start to build AI Object Detection ]");

            Console.Write("Building the model...");
            // Build/optimize the model
            if (!m_inferenceEngine.BuildModel(m_modelAttributes))
            {
                Console.WriteLine("Fail to build the model");
                return 1;
            }
            Console.Write(" Done");
            Console.WriteLine(" ( {0} ms)", m_inferenceEngine.GetProfilerTimeInMillis(CProAiInference.ProfilerSection.BuildModel));
            Console.WriteLine();

            Console.WriteLine("Model Attributes");

            Console.WriteLine("\tModel Type: {0}", m_modelAttributes.modelType);
            Console.WriteLine("\tModel Name: {0}", m_modelAttributes.ModelName);

            Console.WriteLine("\tInput Size: {0}", m_modelAttributes.InputSize);
            Console.WriteLine("\tInput Channel Number: ", m_modelAttributes.InputChannelNumber);
            Console.WriteLine("\tInput Resize Method: ", m_modelAttributes.InputResizeMethodName);
            Console.WriteLine("\tInput Normalization: ", (m_modelAttributes.IsInputNormalized ? "Yes" : "No"));

            if (m_modelAttributes.MemoryUsageInference > 0)
                Console.WriteLine("\tMemory Usage Inference (estimation): {0} GB", m_modelAttributes.MemoryUsageInference / OneGigabyte);

            uint referenceClassesCount = m_modelAttributes.ReferenceClassCount;
            if (referenceClassesCount > 0)
                Console.WriteLine("\tClass Number: {0}", referenceClassesCount);

            for (uint index = 0u; index < referenceClassesCount; index++)
            {
                Console.WriteLine("\t\tClass Index: {0} Name: {1}", index, m_modelAttributes.GetReferenceClassName(index));
            }

            Console.WriteLine();

            return 0;
        }
    }
}
