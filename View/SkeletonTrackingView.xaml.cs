﻿using Microsoft.Kinect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kinect2Server.View
{
    /// <summary>
    /// Interaction logic for GestureRecognition.xaml
    /// </summary>
    public partial class SkeletonTrackingView : UserControl
    {
        private MainWindow mw;
        private SkeletonTracking st;
        private const double HandSize = 30;
        private const double JointThickness = 3;
        private const double ClipBoundsThickness = 10;
        private const float InferredZPositionClamp = 0.1f;
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;
        private List<Tuple<JointType, JointType>> bones;
        private Dictionary<JointType, object> dicoPos;
        private Dictionary<JointType, Point> jointPoints;
        private Dictionary<JointType, Vector4> dicoOr;
        private IReadOnlyDictionary<JointType, Joint> joints;
        private Dictionary<ulong, Dictionary<JointType, object>> dicoBodies;
        private Dictionary<JointType, Joint> filteredJoints;
        private int displayWidth;
        private int displayHeight;
        private List<Pen> bodyColors;
        private string statusText;
        private Boolean grStatus = false;
        private float smoothingParam = 0.5f;
        private KinectJointFilter filter;

        public SkeletonTrackingView()
        {
            this.mw = (MainWindow)Application.Current.MainWindow;
            this.st = this.mw.GestureRecognition;

            // get the depth (display) extents
            FrameDescription frameDescription = this.mw.KinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            this.filter = new KinectJointFilter(smoothingParam, smoothingParam, smoothingParam);
            this.filter.Init(smoothingParam, smoothingParam, smoothingParam);


            // initialize the components (controls) of the window
            InitializeComponent();
        }

        private void switchGR(object sender, RoutedEventArgs e)
        {
            this.switchGestureRecognition(sender, e);
        }

        private void switchGestureRecognition(object sender, RoutedEventArgs e)
        {
            if (!grStatus)
            {
                this.st.addGRListener(this.Reader_FrameArrived);
                setButtonOn(this.stackGR);
                this.grStatus = true;
            }
            else
            {
                this.st.removeGRListener(this.Reader_FrameArrived);
                setButtonOff(this.stackGR);
                this.grStatus = false;
            }
            
        }

        private void submitSmoothing(object sender, RoutedEventArgs e)
        {
            if (this.smoothingSelector.Value == null)
            {
                this.smoothingSelector.Value = (double)this.smoothingParam;
            }
            else
            {
                this.smoothingParam = (float)this.smoothingSelector.Value;
                this.filter.Init(smoothingParam, smoothingParam, smoothingParam);
            }
        }

        private void setButtonOff(StackPanel stack)
        {
            Image img = new Image();
            stack.Children.Clear();
            img.Source = new BitmapImage(new Uri(@"../Images/switch_off.png", UriKind.Relative));
            stack.Children.Add(img);
        }

        private void setButtonOn(StackPanel stack)
        {
            Image img = new Image();
            stack.Children.Clear();
            img.Source = new BitmapImage(new Uri(@"../Images/switch_on.png", UriKind.Relative));
            stack.Children.Add(img);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            if (!this.grStatus)
                return;

            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.st.Bodies == null)
                    {
                        this.st.Bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.st.Bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    int penIndex = 0;
                    foreach (Body body in this.st.Bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        dicoBodies = new Dictionary<ulong, Dictionary<JointType, object>>();

                        if (body.IsTracked)
                        {

                            this.DrawClippedEdges(body, dc);
                            this.jointPoints = new Dictionary<JointType, Point>();

                            if (smoothingParam != 0.0f)
                            {
                                filter.UpdateFilter(body);
                                filteredJoints = filter.GetFilteredJoints();
                                frameTreatement(filteredJoints, body, dc, drawPen);
                            }
                            else
                            {
                                this.joints = body.Joints;
                                frameTreatement(joints, body, dc, drawPen);
                            }
                        }
                    }
                    for (int i = 1; i <= this.st.Bodies.Length; i++)
                    {
                        string slot = "slot" + i;
                        TextBlock tb = (TextBlock)this.FindName(slot);
                        tb.Foreground = this.bodyColors[i - 1].Brush;
                        tb.Text = "Tracking Id : " + this.st.Bodies[i - 1].TrackingId;
                    }
                }
            }
        }

        public void frameTreatement(IReadOnlyDictionary<JointType, Joint> joints, Body body, DrawingContext dc, Pen drawPen)
        {
            this.dicoPos = new Dictionary<JointType, object>();
            this.dicoOr = this.st.chainQuat(body);

            foreach (JointType jointType in joints.Keys)
            {
                // sometimes the depth(Z) of an inferred joint may show as negative
                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                CameraSpacePoint point = joints[jointType].Position;

                if (point.Z < 0)
                {
                    point.Z = InferredZPositionClamp;
                }

                object ob;
                if (jointType == JointType.HandRight)
                {
                    ob = new { Position = point, Orientation = dicoOr[jointType], HandState = body.HandRightState.ToString().ToLower() };
                }
                else if (jointType == JointType.HandLeft)
                {
                    ob = new { Position = point, Orientation = dicoOr[jointType], HandState = body.HandLeftState.ToString().ToLower() };
                }
                else
                {
                    ob = new { Position = point , Orientation = dicoOr[jointType] };
                }
                dicoPos[jointType] = ob;

                DepthSpacePoint depthSpacePoint = this.st.CoordinateMapper.MapCameraPointToDepthSpace(point);
                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);

            }

            dicoBodies[body.TrackingId] = dicoPos;
            string json = JsonConvert.SerializeObject(dicoBodies);
            this.st.NetworkPublisher.SendJSON(json, "skeleton");


            this.DrawBody(joints, jointPoints, dc, drawPen);
            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

            // prevent drawing outside of our render area
            this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

        }

        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, Dictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, Dictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }
    }
}
