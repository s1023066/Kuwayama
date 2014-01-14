//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.CoordinateMappingBasics
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Documents;
    using System.Collections.Generic;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // RGBカメラの解像度・フレームレート
        private const ColorImageFormat rgbFotmat = ColorImageFormat.RgbResolution640x480Fps30;
        // Kinectセンサーからの画像情報を受け取るバッファ
        private byte[] pixelBuffer = null;
        // Kinectセンサーからの骨格情報を受け取るバッファ
        private Skeleton[] skeletonBuffer = null;
        // 画面に表示するビットマップ
        private RenderTargetBitmap bmpBuffer = null;
        // 顔のビットマップイメージ
        private BitmapImage maskImage = null;
        // ビットマップへの描写用drawVisual
        private DrawingVisual drawVisual = new DrawingVisual();
        // Format we will use for the depth stream
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;
        // Active Kinect sensor
        private KinectSensor sensor;
        // Bitmap that will hold color information
        private WriteableBitmap colorBitmap;
        // Bitmap that will hold opacity mask information
        private WriteableBitmap playerOpacityMaskImage = null;
        
        // Intermediate storage for the depth data received from the sensor
        private DepthImagePixel[] depthPixels;
        // Intermediate storage for the player opacity mask
        private int[] playerPixelData;
        // Intermediate storage for the depth to color mapping
        private ColorImagePoint[] colorCoordinates;
        
        // Inverse scaling factor between color and depth
        private int colorToDepthDivisor;
        // Width of the depth image
        private int depthWidth;
        // Height of the depth image
        private int depthHeight;
        // Indicates opaque in an opacity mask
        private int opaquePixelValue = -1;
        // Initializes a new instance of the MainWindow class.

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //Kinectセンサーの取得
            KinectSensor kinect = KinectSensor.KinectSensors[0];
            //画像の読み込み
            Uri imgUri = new Uri("pack://application:,,,/Images/mask.png");
            maskImage = new BitmapImage(imgUri);
            // カラー、骨格ストリームの有効化
            ColorImageStream cltStream = kinect.ColorStream;
            cltStream.Enable(rgbFotmat);
            SkeletonStream skelStream = kinect.SkeletonStream;
            skelStream.Enable();
            // バッファの初期化
            pixelBuffer = new byte[cltStream.FramePixelDataLength];
            skeletonBuffer = new Skeleton[skelStream.FrameSkeletonArrayLength];
            bmpBuffer = new RenderTargetBitmap(cltStream.FrameWidth, cltStream.FrameHeight, 96, 96, PixelFormats.Default);
            MaskedColor.Source = bmpBuffer;
            //イベントハンドラの登録
            kinect.AllFramesReady += AllFramesReady;
            //Kinectセンサーからのストリーム取得を開始
            kinect.Start();

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);

                this.depthWidth = this.sensor.DepthStream.FrameWidth;

                this.depthHeight = this.sensor.DepthStream.FrameHeight;

                this.sensor.ColorStream.Enable(rgbFotmat);

                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;

                this.colorToDepthDivisor = colorWidth / this.depthWidth;

                // Turn on to get player masks
                this.sensor.SkeletonStream.Enable();

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.pixelBuffer = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.playerPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];

                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.MaskedColor.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }


            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }
        // FrameReady イベントのハンドラ
        private void AllFramesReady(object senter, AllFramesReadyEventArgs e)
        {
            KinectSensor kinect = senter as KinectSensor;
            List<SkeletonPoint> headList = null;

            // 骨格情報から頭の座標リストを作成
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                    headList = getHeadPoints(skeletonFrame);
            }

            // カメラの画像情報に頭の位置にマスクを上書きして描画
            using (ColorImageFrame imageFrame = e.OpenColorImageFrame())
            {
                if (imageFrame != null)
                    fillBitmap(kinect, imageFrame, headList);
            }
        }

        // 骨格情報から頭の位置を取得しリストに入れて返す
        private List<SkeletonPoint> getHeadPoints(SkeletonFrame skelFrame)
        {
            // 処理結果のリストを空の状態で作成
            List<SkeletonPoint> results = new List<SkeletonPoint>();

            // 骨格情報をバッファにコピー
            skelFrame.CopySkeletonDataTo(skeletonBuffer);

            // 取得出来た骨格毎にループ
            foreach (Skeleton skeleton in skeletonBuffer)
            {
                // トラッキング出来ない骨格は処理しない
                if (Skeleton.TrackingState != SkeletonTrackingState.Tracked)
                    continue;

                // 骨格から頭を取得
                Joint head = Skeleton.Joints[JointType.Head];

                // 頭の位置が取得出来ない場合は処理しない
                if (head.TrackingState != JointTrackingState.Tracked
                    && head.TrackingState != JointTrackingState.Inferred)
                    continue;

                // 頭の位置を保存
                results.Add(head.Position);
            }
            return results;
        }

        // RGBカメラの画像情報に顔の位置にマスクを上書きして描画する
        private void fillBitmap(KinectSensor kinect, ColorImageFrame imgFrame,
            List<SkeletonPoint> headList)
        {
            // 描画の準備
            var drawContext = drawVisual.RenderOpen();
            int frmWidth = imgFrame.Width;
            int frmHeight = imgFrame.Height;

            // 画像情報をバッファにコピー
            imgFrame.CopyPixelDataTo(pixelBuffer);

            // カメラの画像情報から背景のビットマップを作成して描写
            var bgImg = new WriteableBitmap(frmWidth, frmHeight, 96, 96,
                PixelFormats.Bgr32, null);
            bgImg.WritePixels(new Int32Rect(0, 0, frmWidth, frmHeight),
                pixelBuffer, frmWidth * 4, 0);
            drawContext.DrawImage(bgImg, new Rect(0, 0, frmWidth, frmHeight));

            // getHeadPointsで取得した各頭部(の位置)毎にループ
            for (int idx = 0; headList != null && idx < headList.Count; ++idx)
            {
                //骨格の座標から画像情報の座標に変換
                ColorImagePoint headPt = kinect.MapSkeletonPointToColor(headList[idx], rgbFotmat);

                // 頭の位置にマスク画像を描画
                Rect rect = new Rect(headPt.X - 64, headPt.Y - 64, 128, 128);
                drawContext.DrawImage(maskImage, rect);
            }

            //画像に表示するビットマップに描画
            drawContext.Close();
            bmpBuffer.Render(drawVisual);
        }


        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == this.sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.pixelBuffer);

                    colorReceived = true;
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == depthReceived)
            {
                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                    DepthFormat,
                    this.depthPixels,
                    rgbFotmat,
                    this.colorCoordinates);

                Array.Clear(this.playerPixelData, 0, this.playerPixelData.Length);

                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    for (int x = 0; x < this.depthWidth; ++x)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);

                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];

                        int player = depthPixel.PlayerIndex;

                        // if we're tracking a player for the current pixel, sets it opacity to full
                        if (player > 0)
                        {
                            // retrieve the depth to color mapping for the current depth pixel
                            ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

                            // scale color coordinates to depth resolution
                            int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

                            // make sure the depth pixel maps to a valid point in color space
                            // check y > 0 and y < depthHeight to make sure we don't write outside of the array
                            // check x > 0 instead of >= 0 since to fill gaps we set opaque current pixel plus the one to the left
                            // because of how the sensor works it is more correct to do it this way than to set to the right
                            if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
                            {
                                // calculate index into the player mask pixel array
                                int playerPixelIndex = colorInDepthX + (colorInDepthY * this.depthWidth);

                                // set opaque
                                this.playerPixelData[playerPixelIndex] = opaquePixelValue;

                                // compensate for depth/color not corresponding exactly by setting the pixel 
                                // to the left to opaque as well
                                this.playerPixelData[playerPixelIndex - 1] = opaquePixelValue;
                            }
                        }
                    }
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == colorReceived)
            {
                // Write the pixel data into our bitmap
                this.colorBitmap.WritePixels(
                    new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                    this.pixelBuffer,
                    this.colorBitmap.PixelWidth * sizeof(int),
                    0);

                if (this.playerOpacityMaskImage == null)
                {
                    this.playerOpacityMaskImage = new WriteableBitmap(
                        this.depthWidth,
                        this.depthHeight,
                        96,
                        96,
                        PixelFormats.Bgra32,
                        null);

                    MaskedColor.OpacityMask = new ImageBrush { ImageSource = this.playerOpacityMaskImage };
                }

                this.playerOpacityMaskImage.WritePixels(
                    new Int32Rect(0, 0, this.depthWidth, this.depthHeight),
                    this.playerPixelData,
                    this.depthWidth * ((this.playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                    0);
            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            int colorWidth = this.sensor.ColorStream.FrameWidth;
            int colorHeight = this.sensor.ColorStream.FrameHeight;

            // create a render target that we'll render our controls to
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                // render the backdrop
                VisualBrush backdropBrush = new VisualBrush(Backdrop);
                dc.DrawRectangle(backdropBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));

                // render the color image masked out by players
                VisualBrush colorBrush = new VisualBrush(MaskedColor);
                dc.DrawRectangle(colorBrush, null, new Rect(new Point(), new Size(colorWidth, colorHeight)));
            }

            renderBitmap.Render(dv);

            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string path = Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }
        }

        /// <summary>
        /// Handles the checking or unchecking of the near mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxNearModeChanged(object sender, RoutedEventArgs e)
        {
            if (this.sensor != null)
            {
                // will not function on non-Kinect for Windows devices
                try
                {
                    if (this.checkBoxNearMode.IsChecked.GetValueOrDefault())
                    {
                        this.sensor.DepthStream.Range = DepthRange.Near;
                    }
                    else
                    {
                        this.sensor.DepthStream.Range = DepthRange.Default;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }
}