//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Globalization;
    using System;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));// A = 255

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        private WriteableBitmap colorBitMap;

        private WriteableBitmap writeableBitmap = null;

        // Start.png
        private BitmapImage StartImage;

        private byte[] colorPixels;

        // 撮影ボタンのフラグ
        bool btnflg;
        // タイマークラス
        DispatcherTimer timer;
        // 時間をカウントする変数
        int timecount;
        // Start.pngのファイルパスを取得し格納する変数
        string StartImage_Filename;

        RenderTargetBitmap renderBitmap = new RenderTargetBitmap(640, 480, 96f, 96f, PixelFormats.Default);
        RenderTargetBitmap target = new RenderTargetBitmap(640, 480, 96f, 96f, PixelFormats.Default);
        //RenderTargetBitmap target;
        DrawingVisual dv = new DrawingVisual();
        DrawingVisual dv2 = new DrawingVisual(); 
        //DrawingVisual dv;

        // 一次情報を記録する
        BitmapImage bmapImage_Temp;

        int count;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Title = "Kinect Animation";
            button1.Content = "Start.png\nを読み込む";
            button2.Content = "撮影を開始する";
            ModeCheckBox.Content = "千手観音モード";
            Team_Bottun.Content = "バージョン/チームメンバー紹介";

            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timecount = 0;
            timer.Tick += timer_Tick;
            btnflg = false;
            count = 0;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if(StartImage != null)
            {
                ShotPicture(3, timecount);
            }
            else
            {
                ShotPicture(2, timecount);
            }

            timecount++;
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            //this.imageSource = new DrawingImage(this.drawingGroup);
            this.imageSource = new DrawingImage(this.drawingGroup);

            //target = new RenderTargetBitmap(640, 480, 96f, 96f, PixelFormats.Default);
            //dv = new DrawingVisual();

            //this.imageSource2 =

            // Display the drawing using our image control
            //Image.Source = this.imageSource;

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
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // ColorStreamを有効にする
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // センサーから得たフルカラーの画像情報を取得する(ピクセル)
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                
                // センサーから得たフルカラーの画像情報を取得する
                this.colorBitMap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                // Add an event handler to be called whenever there is new color frame data
                //this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                
                // イメージに描画する
                //Image.Source = this.target;
                //target.Clear();
                //Image.Source = ImageDrawing;
                //Image.Source = null;
                //target = null;

                //Image.Source = this.colorBitMap;

                //Image.Source = this.imageSource;

                // すべてのフレームを処理するイベントメソッドの追加
                this.sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(sensor_AllFramesReady);
                
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
                MessageBox.Show("Kinectセンサーが接続されていません。\n[OK]を押し、Kinectセンサーを再度接続してプリケーションを再起動してくだい。"
                    , "Kinectの接続エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
                MainWindow1.Close();
                return;
            }


            // センサーの角度を0にする
            this.sensor.ElevationAngle = 0;

            if (1 == (int)MessageBox.Show("背景を撮影します。\n人物が写らないようにし、準備ができたら『OK』を押してください。", "背景撮影ダイアログ", MessageBoxButton.OKCancel, MessageBoxImage.Information))
            {
                ShotPicture(0, 0);
            }
            else
            {
                MainWindow1.Close();
                return;
            }
        }

        // すべてのフレームを処理するイベントメソッド
        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // ColorFrame（フルカラーの画像が表示される）
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    this.colorBitMap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitMap.PixelWidth, this.colorBitMap.PixelHeight),
                        this.colorPixels,
                        this.colorBitMap.PixelWidth * sizeof(int),
                        0);
                }
            }

            // Skeletonの配列を格納する
            Skeleton[] skeletons = new Skeleton[0];

            // Skeletonの取得
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            // Skeletonの描画や骨格情報を取得するメソッド
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // 一次情報のインスタンス化
                bmapImage_Temp = new BitmapImage();
                // Draw a transparent background to set the render size
                ImageBrush imageBrush = new ImageBrush();   // 描画するための仮想ブラシを作成

                if (StartImage != null)
                {
                    imageBrush.ImageSource = new BitmapImage(new Uri(@StartImage_Filename, UriKind.Relative));  // Start.pngで画面を描画
                }

                // StartImage_Filenameの空文字判定の描画
                if (StartImage != null)
                {
                    //Image.Source = StartImage;
                }
                else
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                }

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);
                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
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
            }
        }

        ///  Skeletonの骨格情報の取得
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
 
            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        
        ///  Skeletonの描画

        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            BitmapImage BMPIMG = new BitmapImage();
            BMPIMG.BeginInit();
            BMPIMG.UriSource = new Uri("stop.png", UriKind.Relative);
            BMPIMG.EndInit();
            //RenderTargetBitmap target = new RenderTargetBitmap(640, 480, 96f, 96f, PixelFormats.Default);
            //DrawingVisual dv = new DrawingVisual();
            //drawingContext.DrawRectangle(Brushes.White, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            // 描画した骨格情報をBitmapImageに変換する（※重要）
            //using (drawingContext = dv.RenderOpen())
            //{
                Joint joint0 = skeleton.Joints[jointType0];
                Joint joint1 = skeleton.Joints[jointType1];

                // If we can't find either of these joints, exit
                if (joint0.TrackingState == JointTrackingState.NotTracked ||
                    joint1.TrackingState == JointTrackingState.NotTracked)
                {
                    return;
                }

                // Don't draw if both points are inferred
                if (joint0.TrackingState == JointTrackingState.Inferred &&
                    joint1.TrackingState == JointTrackingState.Inferred)
                {
                    return;
                }

                // We assume all drawn bones are inferred unless BOTH joints are tracked
                Pen drawPen = this.inferredBonePen;
                if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
                {
                    drawPen = this.trackedBonePen;
                }

                //target.Clear();

                // 各骨格を結ぶ線を描画するメソッド

                using (drawingContext = dv.RenderOpen())
                {
                    //drawingContext.DrawImage(StartImage, new Rect(0, 0, 640, 480));
                    //System.Threading.Thread.Sleep(500);
                    drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
                }
                
                target.Render(dv);

                if (StartImage != null)
                {
                    using (drawingContext = dv2.RenderOpen())
                    {
                        BackgroundImage.Source = StartImage;
                        VisualBrush backgroundBrush = new VisualBrush(BackgroundImage);
                        drawingContext.DrawRectangle(backgroundBrush, null, new Rect(new Point(), new Size(640f, 480f)));

                        Image.Source = target;
                        VisualBrush Skeletonbrush = new VisualBrush(Image);
                        drawingContext.DrawRectangle(Skeletonbrush, null, new Rect(new Point(), new Size(640f, 480f)));

                    }

                    renderBitmap.Render(dv2);
                }

                if (StartImage != null)
                {
                    if (ModeCheckBox.IsChecked == true)
                    {
                        if (count % 1500 == 0)
                        {
                            target.Clear();
                            renderBitmap.Clear();
                            count = 0;
                        }
                        else
                        {
                            ImageTotal.Source = renderBitmap;
                        }
                    }
                    else
                    {

                        if (count % 100 == 0)
                        {
                            target.Clear();
                            renderBitmap.Clear();
                            count = 0;
                        }
                        else
                        {
                            ImageTotal.Source = renderBitmap;
                        }
                    }
                }
                else
                {
                    if (ModeCheckBox.IsChecked == true)
                    {
                        if (count % 1500 == 0)
                        {
                            target.Clear();
                            count = 0;
                        }
                        else
                        {
                            ImageTotal.Source = target;
                        }
                    }
                    else
                    {

                        if (count % 100 == 0)
                        {
                            target.Clear();
                            count = 0;
                        }
                        else
                        {
                            ImageTotal.Source = target;
                        }
                    }
                }

                    Console.WriteLine("Count:{0}", count);

                    // Countの一定数値になった時の初期化
                    count++;
                    //count++;
                
            
            //drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        // スクリーンショットの撮影メソッド
        private void ShotPicture(int flg, int timecount)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();
            BitmapEncoder encoder2 = new PngBitmapEncoder();
            BitmapEncoder encoder3 = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(this.colorBitMap));
            encoder2.Frames.Add(BitmapFrame.Create(target));
            encoder3.Frames.Add(BitmapFrame.Create(renderBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string path = Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");

            // 開始時のスクリーンショットの撮影のファイル名
            string path2 = Path.Combine(myPhotos, "Start.png");

            string path3 = Path.Combine(myPhotos, "Slide" + timecount + ".png");

            // write the new file to disk
            try
            {
                switch (flg)
                {
                    case 0:
                    case 1:
                         using (FileStream fs = new FileStream(path2, FileMode.Create))
                         {
                            encoder.Save(fs);
                         }
                        
                         this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path2);
                    break;

                    case 2:
                         using (FileStream fs = new FileStream(path3, FileMode.Create))
                        {
                            encoder2.Save(fs);
                        }

                        this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path3);
                    break;

                    case 3:
                        //encoder2.Frames.Add(BitmapFrame.Create(this.target));
                        using (FileStream fs = new FileStream(path3, FileMode.Create))
                        {
                            encoder3.Save(fs);
                        }

                        this.statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path3);
                    break;

                    default:
                        break;

                }
            }
            catch (IOException)
            {
                this.statusBarText.Text = "画像の保存に失敗しました。";
            }
        }

        // スクリーンショットを読み込む
        public void LoadPicture()
        {
            // ファイルを開くダイアログを表示して画像を読み込む
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.FileName = "Start.png";
            dlg.DefaultExt = "Start.png";
            dlg.Filter = "PNG画像(*.png)|*.png";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                string filename = dlg.FileName;

                // すでに読み込まれていたら開放する
                if (StartImage != null)
                {
                    StartImage = null;
                }

                // ファイルの名前を引き渡す
                StartImage_Filename = filename;

                // BitmapImageにファイルから画像を読み込む
                StartImage = new BitmapImage();
                StartImage.BeginInit();
                StartImage.UriSource = new Uri(filename);
                StartImage.EndInit();

                // imageに画像を表示
                //BackgroundImage.Source = StartImage;
                //EffectPicture(target, StartImage);
            }
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            if (btnflg != true)
            {
                timer.Start();
                btnflg = true;
                button2.Content = "撮影中";
                button2.Background = Brushes.Red;
            }
            else
            {
                btnflg = false;
                timer.Stop();
                button2.Background = Background;
                button2.Content = "撮影を開始する。";
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            LoadPicture();
        }

        // Skeleton Color を合成する関数
        public void EffectPicture(RenderTargetBitmap dig, BitmapImage bmg)
        {
        }

        private void Team_Bottun_Click(object sender, RoutedEventArgs e)
        {
            TeamWindow team = new TeamWindow();

            team.Show();
        }
    }
}