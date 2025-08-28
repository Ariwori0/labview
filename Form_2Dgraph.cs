using System;
using System.Drawing;
using System.Windows.Forms;
using ZedGraph;

namespace CoreLinkSys1.UI
{
    public partial class Form_2Dgraph : Form
    {
        private ZedGraphControl zedGraph;
        private PointPairList gyroXList, gyroYList, gyroZList;
        private PointPairList axList, ayList, azList;
        private PointPairList rollList, pitchList, yawList;
        private PointPairList globalAxList, globalAyList, globalAzList;
        private PointPairList WalkList;
        private PointPairList StepLabelList;
        private PointPairList StepLabelForwardList;   // ForwardAccベース


        private PointPairList globalAzForStepPane; // StepPane 用 GlobalAccZ をフィールドに追加
        private PointPairList forwardAccForStepPane;

        public Form_2Dgraph()
        {
            InitializeComponent();
            InitializeGraphs();
        }

        private void InitializeComponent()
        {
            this.Text = "CoreLinkSystem";
            this.ClientSize = new Size(1000, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            zedGraph = new ZedGraphControl();
            zedGraph.Dock = DockStyle.Fill;
            this.Controls.Add(zedGraph);
        }

        private void InitializeGraphs()
        {
            MasterPane master = zedGraph.MasterPane;
            master.PaneList.Clear();
            master.Margin.All = 10;

            // Gyro
            var gyroPane = new GraphPane { Title = { Text = "Gyro" } };
            gyroPane.XAxis.Title.Text = "Time (s)";
            gyroPane.YAxis.Title.Text = "Gyro (deg/s)";
            gyroXList = new PointPairList();
            gyroYList = new PointPairList();
            gyroZList = new PointPairList();
            gyroPane.AddCurve("Gx", gyroXList, Color.Red, SymbolType.None);
            gyroPane.AddCurve("Gy", gyroYList, Color.Green, SymbolType.None);
            gyroPane.AddCurve("Gz", gyroZList, Color.Blue, SymbolType.None);

            // Raw Accel
            var accelPane = new GraphPane { Title = { Text = "Raw Acceleration" } };
            accelPane.XAxis.Title.Text = "Time (s)";
            accelPane.YAxis.Title.Text = "Acc (G)";
            axList = new PointPairList();
            ayList = new PointPairList();
            azList = new PointPairList();
            accelPane.AddCurve("Ax", axList, Color.Red, SymbolType.None);
            accelPane.AddCurve("Ay", ayList, Color.Green, SymbolType.None);
            accelPane.AddCurve("Az", azList, Color.Blue, SymbolType.None);

            // Global Acc
            var globalPane = new GraphPane { Title = { Text = "Global Acceleration" } };
            globalPane.XAxis.Title.Text = "Time (s)";
            globalPane.YAxis.Title.Text = "Global Acc (G)";
            globalAxList = new PointPairList();
            globalAyList = new PointPairList();
            globalAzList = new PointPairList();
            globalPane.AddCurve("GlobalAx", globalAxList, Color.DarkRed, SymbolType.None);
            globalPane.AddCurve("GlobalAy", globalAyList, Color.DarkGreen, SymbolType.None);
            globalPane.AddCurve("GlobalAz", globalAzList, Color.DarkBlue, SymbolType.None);

            // Euler Angles
            var eulerPane = new GraphPane { Title = { Text = "Euler Angles" } };
            eulerPane.XAxis.Title.Text = "Time (s)";
            eulerPane.YAxis.Title.Text = "Angle (deg)";
            rollList = new PointPairList();
            pitchList = new PointPairList();
            yawList = new PointPairList();
            eulerPane.AddCurve("Roll", rollList, Color.Red, SymbolType.None);
            eulerPane.AddCurve("Pitch", pitchList, Color.Green, SymbolType.None);
            eulerPane.AddCurve("Yaw", yawList, Color.Blue, SymbolType.None);

            // Walking Score
            var WalkSegmentPane = new GraphPane { Title = { Text = "Walk Score" } };
            WalkSegmentPane.XAxis.Title.Text = "Time (s)";
            WalkSegmentPane.YAxis.Title.Text = "WalkScore";
            WalkList = new PointPairList();
            WalkSegmentPane.AddCurve("WalkSegment", WalkList, Color.Purple, SymbolType.None).Line.Width = 2;


            // Step Label + GlobalAccZ
            var stepPane = new GraphPane { Title = { Text = "Step Label + GlobalAccZ" } };
            stepPane.XAxis.Title.Text = "Time (s)";
            stepPane.YAxis.Title.Text = "Step Label / GlobalAccZ";

            // accZベース (Green)
            StepLabelList = new PointPairList();
            stepPane.AddCurve("StepLabel", StepLabelList, Color.Green, SymbolType.Circle).Line.IsVisible = false;

            // ForwardAccベース (Orange)
            StepLabelForwardList = new PointPairList();
            stepPane.AddCurve("StepLabel (ForwardAcc)", StepLabelForwardList, Color.OrangeRed, SymbolType.Diamond).Line.IsVisible = false;

            // GlobalAccZ 波形 (Blue)
            globalAzForStepPane = new PointPairList();
            LineItem globalAccCurve = stepPane.AddCurve("GlobalAccZ", globalAzForStepPane, Color.Blue, SymbolType.None);
            globalAccCurve.Line.Width = 1.5f;
            globalAccCurve.Symbol.IsVisible = false;

            // ForwardAcc 波形 (Red)
            forwardAccForStepPane = new PointPairList();
            LineItem forwardAccCurve = stepPane.AddCurve("ForwardAcc", forwardAccForStepPane, Color.Red, SymbolType.None);
            forwardAccCurve.Line.Width = 1.5f;
            forwardAccCurve.Symbol.IsVisible = false;



            // 配置
            master.Add(gyroPane);
            master.Add(accelPane);
            master.Add(globalPane);
            master.Add(eulerPane);
            master.Add(WalkSegmentPane);
            master.Add(stepPane);

            master.SetLayout(zedGraph.CreateGraphics(), 3, 2);
        }

        public void AddData(double time,
                            double gx, double gy, double gz,
                            double ax, double ay, double az,
                            double gax, double gay, double gaz,
                            double roll, double pitch, double yaw,
                            double walkScore,
                            string stepLabel,
                            double forwardAcc)   // 👈 ForwardAccを追加
        {
            gyroXList.Add(time, gx); gyroYList.Add(time, gy); gyroZList.Add(time, gz);
            axList.Add(time, ax); ayList.Add(time, ay); azList.Add(time, az);
            globalAxList.Add(time, gax); globalAyList.Add(time, gay); globalAzList.Add(time, gaz);
            rollList.Add(time, roll); pitchList.Add(time, pitch); yawList.Add(time, yaw);
            WalkList.Add(time, walkScore);

            // GlobalAccZ 波形
            globalAzForStepPane.Add(time, gaz);
            // ForwardAcc 波形
            //forwardAccForStepPane.Add(time, forwardAcc);
            forwardAccForStepPane.Add(time, forwardAcc);

            // accZベースラベル
            int stepValue = 0;
            if (stepLabel == "R") stepValue = 1;
            else if (stepLabel == "L") stepValue = -1;
            StepLabelList.Add(time, stepValue);

            // ForwardAccベースラベル
            int forwardValue = 0;
            if (stepLabel == "R_F") forwardValue = 2;
            else if (stepLabel == "L_F") forwardValue = -2;
            StepLabelForwardList.Add(time, forwardValue);
        }

        //public void AddData(double time,
        //                    double gx, double gy, double gz,
        //                    double ax, double ay, double az,
        //                    double gax, double gay, double gaz,
        //                    double roll, double pitch, double yaw,
        //                    double walkScore)
        //{
        //    AddData(time, gx, gy, gz, ax, ay, az, gax, gay, gaz, roll, pitch, yaw, walkScore, "U");
        //}

        public void FinalizePlots()
        {
            foreach (var pane in zedGraph.MasterPane.PaneList)
            {
                pane.AxisChange();
            }
            zedGraph.AxisChange();
            zedGraph.Invalidate();
        }
    }
}