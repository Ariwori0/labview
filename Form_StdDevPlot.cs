using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;
using CoreLinkSys1.Analysis;

namespace CoreLinkSys1.UI
{
    public partial class Form_StdDevPlot : Form
    {
        private ZedGraphControl zedGraphMain;      // 右下: 比較
        private ZedGraphControl zedGraphPedal;     // 左上: Pedal時系列（既存）
        private ZedGraphControl zedGraphVelocity;  // 左下: 速度時系列（新設）
        private ZedGraphControl zedGraphJerk;      // 右上: ジャーク時系列（新設）

        private GraphPane graphPaneMain;
        private GraphPane graphPanePedal;
        private GraphPane graphPaneVelocity;
        private GraphPane graphPaneJerk;

        // 時系列データ保持用
        private Dictionary<string, List<double>> pedalTimeSeriesData = new Dictionary<string, List<double>>(); // 元データ
        private Dictionary<string, List<double>> pedalSmoothedData = new Dictionary<string, List<double>>(); // スムージング済みデータ
        private Dictionary<string, List<double>> pedalVelocityData = new Dictionary<string, List<double>>(); // 速度データ
        private Dictionary<string, List<double>> copPxTimeSeriesData = new Dictionary<string, List<double>>();
        private Dictionary<string, List<double>> copPyTimeSeriesData = new Dictionary<string, List<double>>();
        private Dictionary<string, List<string>> pedalPhaseLabels = new Dictionary<string, List<string>>();
        private Dictionary<string, List<double>> pedalJerkData = new Dictionary<string, List<double>>();

        // ペダル開始インデックス管理用
        private Dictionary<string, int> pedalStartIndices = new Dictionary<string, int>();

        // 区間データ保持用フィールドを追加
        private Dictionary<string, List<PedalSegment>> pedalSegments = new Dictionary<string, List<PedalSegment>>();

        public Form_StdDevPlot()
        {
            InitializeComponent();
            InitializeGraphs();
        }

        private void InitializeComponent()
        {
            this.Text = "Comparison & Time Series Plot";
            this.ClientSize = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            // TableLayoutPanelを使用してレイアウトを管理
            TableLayoutPanel tableLayout = new TableLayoutPanel();
            tableLayout.Dock = DockStyle.Fill;
            tableLayout.RowCount = 2;
            tableLayout.ColumnCount = 2;

            // 行・列の比率設定（均等に分割）
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // 上段
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // 下段
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // 左列
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // 右列

            //上段左: Pedal時系列グラフ
            // 上段左: Pedal時系列（既存）
            zedGraphPedal = new ZedGraphControl();
            zedGraphPedal.Dock = DockStyle.Fill;
            zedGraphPedal.IsShowPointValues = false;
            tableLayout.Controls.Add(zedGraphPedal, 0, 0);

            // 上段右: ジャーク時系列（新設）
            zedGraphJerk = new ZedGraphControl();
            zedGraphJerk.Dock = DockStyle.Fill;
            tableLayout.Controls.Add(zedGraphJerk, 1, 0);

            // 下段左: 速度時系列（新設）
            zedGraphVelocity = new ZedGraphControl();
            zedGraphVelocity.Dock = DockStyle.Fill;
            tableLayout.Controls.Add(zedGraphVelocity, 0, 1);

            // 下段右: 比較（既存）
            zedGraphMain = new ZedGraphControl();
            zedGraphMain.Dock = DockStyle.Fill;
            zedGraphMain.IsShowPointValues = true;
            tableLayout.Controls.Add(zedGraphMain, 1, 1);

            this.Controls.Add(tableLayout);
        }

        private void InitializeGraphs()
        {
            // Pedal時系列
            graphPanePedal = zedGraphPedal.GraphPane;
            graphPanePedal.Title.Text = "Pedal Time Series (100Hz)";
            graphPanePedal.XAxis.Title.Text = "Time (s)";
            graphPanePedal.YAxis.Title.Text = "Pedal Value";
            graphPanePedal.Chart.Fill = new Fill(Color.White, Color.LightBlue, 45f);
            graphPanePedal.Legend.IsVisible = true;
            graphPanePedal.XAxis.MajorGrid.IsVisible = true;
            graphPanePedal.YAxis.MajorGrid.IsVisible = true;
            // 右軸はペダル速度を重ね描き（従来通り）
            graphPanePedal.Y2Axis.IsVisible = true;
            graphPanePedal.Y2Axis.Title.Text = "Pedal Velocity";
            graphPanePedal.Y2Axis.MajorGrid.IsVisible = false;
            graphPanePedal.Y2Axis.Scale.AlignH = AlignH.Right;
            graphPanePedal.Y2Axis.Scale.FontSpec.FontColor = Color.Orange;
            graphPanePedal.Y2Axis.Title.FontSpec.FontColor = Color.Orange;

            // 速度時系列（左下）
            graphPaneVelocity = zedGraphVelocity.GraphPane;
            graphPaneVelocity.Title.Text = "Velocity Time Series (y')";
            graphPaneVelocity.XAxis.Title.Text = "Time (s)";
            graphPaneVelocity.YAxis.Title.Text = "Velocity";
            graphPaneVelocity.Chart.Fill = new Fill(Color.White, Color.Honeydew, 45f);
            graphPaneVelocity.Legend.IsVisible = true;
            graphPaneVelocity.XAxis.MajorGrid.IsVisible = true;
            graphPaneVelocity.YAxis.MajorGrid.IsVisible = true;

            // ジャーク時系列（右上）
            graphPaneJerk = zedGraphJerk.GraphPane;
            graphPaneJerk.Title.Text = "Jerk Time Series (y‴)";
            graphPaneJerk.XAxis.Title.Text = "Time (s)";
            graphPaneJerk.YAxis.Title.Text = "Jerk";
            graphPaneJerk.Chart.Fill = new Fill(Color.White, Color.MistyRose, 45f);
            graphPaneJerk.Legend.IsVisible = true;
            graphPaneJerk.XAxis.MajorGrid.IsVisible = true;
            graphPaneJerk.YAxis.MajorGrid.IsVisible = true;

            // 比較（右下）
            graphPaneMain = zedGraphMain.GraphPane;
            graphPaneMain.Title.Text = "比較";
            graphPaneMain.XAxis.Title.Text = "力の使い方(CoP 総移動量)";
            graphPaneMain.YAxis.Title.Text = "滑らか運転(指標)";
            graphPaneMain.Chart.Fill = new Fill(Color.White, Color.LightGray, 45f);
            graphPaneMain.Legend.IsVisible = true;
            graphPaneMain.Legend.Position = LegendPos.TopCenter;
            graphPaneMain.XAxis.Scale.Min = 0;
            graphPaneMain.XAxis.MajorGrid.IsVisible = true;
            graphPaneMain.YAxis.MajorGrid.IsVisible = true;

            // 反映
            zedGraphPedal.AxisChange();
            zedGraphVelocity.AxisChange();
            zedGraphJerk.AxisChange();
            zedGraphMain.AxisChange();

        }

        // 従来の速度計算なしメソッド（下位互換性のため残す）
        public void SetTimeSeriesData(
            string label,
            List<double> pedalData,
            List<double> copPxData,
            List<double> copPyData,
            List<string> pedalLabels,
            int startIndex
        )
        {
            // 速度データがない場合は、ここで計算（非推奨だが下位互換性のため）
            var smoothedData = CalcMethod.Smooth(pedalData, 50);
            var velocityData = CalcMethod.ComputeVelocity2(smoothedData, 0.01, 2);
            SetTimeSeriesDataWithVelocity(label, pedalData, smoothedData, velocityData, copPxData, copPyData, pedalLabels, startIndex);
        }

        // 新しい速度データ付きメソッド（MainFormから呼ばれる推奨方法）
        public void SetTimeSeriesDataWithVelocity(
            string label,
            List<double> pedalData,        // 元の生データ
            List<double> pedalSmoothed,    // スムージング済みデータ
            List<double> pedalVelocity,    // MainFormで計算済みの速度データ
            List<double> copPxData,
            List<double> copPyData,
            List<string> pedalLabels,
            int startIndex
        )
        {
            pedalTimeSeriesData[label] = pedalData;
            pedalSmoothedData[label] = pedalSmoothed; // スムージング済みデータを保存
            pedalVelocityData[label] = pedalVelocity; // 速度データを保存
            copPxTimeSeriesData[label] = copPxData;
            copPyTimeSeriesData[label] = copPyData;

            if (pedalLabels != null)
                pedalPhaseLabels[label] = pedalLabels;

            pedalStartIndices[label] = startIndex;

            // 区間データを計算して保持（スムージング済みデータを使用）
            pedalSegments[label] = CalcMethod.SegmentPedalWaveformDynamic(pedalSmoothed, 20, 0.08, -0.08, 0.03, 0.8);
        }

        // 既存のPlotDataPointsメソッドを拡張
        public void PlotDataPoints(Dictionary<string, StdDevPair> dataPoints)
        {
            if (dataPoints == null || dataPoints.Count == 0)
                return;

            // 時系列の更新（Pedal/Velocity/Jerk）
            PlotTimeSeriesData();

            // 比較散布図のみ描画
            //PlotStandardDeviationGraph(dataPoints);
        }

        private void PlotTimeSeriesData()
        {
            // ===== 左上：ペダル（現状どおり） =====
            graphPanePedal.CurveList.Clear();
            graphPanePedal.GraphObjList.Clear();

            Color[] colors = { Color.Blue, Color.Red, Color.Green, Color.Orange, Color.Purple };
            int colorIndex = 0;

            foreach (var kvp in pedalTimeSeriesData)
            {
                string label = kvp.Key;
                List<double> pedalData = kvp.Value;

                // 区間の背景塗り（既存）
                if (pedalSegments.ContainsKey(label))
                {
                    foreach (var segment in pedalSegments[label])
                    {
                        double x1 = segment.StartIndex * 0.01;
                        double x2 = segment.EndIndex * 0.01;
                        Color fillColor;
                        switch (segment.Type)
                        {
                            case "Rising": fillColor = Color.FromArgb(30, Color.Red); break;
                            case "Falling": fillColor = Color.FromArgb(30, Color.Blue); break;
                            case "Steady": fillColor = Color.FromArgb(30, Color.Green); break;
                            default: fillColor = Color.FromArgb(30, Color.Gray); break;
                        }
                        double yMin = graphPanePedal.YAxis.Scale.Min;
                        double yMax = graphPanePedal.YAxis.Scale.Max;
                        var rect = new BoxObj(x1, yMin, x2 - x1, yMax - yMin, Color.Empty, fillColor)
                        {
                            Fill = new Fill(fillColor),
                            ZOrder = ZOrder.E_BehindCurves,
                            IsClippedToChartRect = true
                        };
                        graphPanePedal.GraphObjList.Add(rect);
                    }
                }

                // 元データ
                var pedalPoints = new PointPairList();
                for (int i = 0; i < pedalData.Count; i++) pedalPoints.Add(i * 0.01, pedalData[i]);
                var pedalCurve = graphPanePedal.AddCurve(label + " Pedal (Raw)",
                    pedalPoints, Color.FromArgb(100, colors[colorIndex % colors.Length]), SymbolType.None);
                pedalCurve.Line.Width = 1;

                // スムージング
                if (pedalSmoothedData.ContainsKey(label))
                {
                    var sm = pedalSmoothedData[label];
                    var sp = new PointPairList();
                    for (int i = 0; i < sm.Count; i++) sp.Add(i * 0.01, sm[i]);
                    var smCurve = graphPanePedal.AddCurve(label + " Pedal (Smoothed)",
                        sp, colors[colorIndex % colors.Length], SymbolType.None);
                    smCurve.Line.Width = 3;
                }

                // 右軸に速度をオーバーレイ（既存）
                if (pedalVelocityData.ContainsKey(label))
                {
                    var v = pedalVelocityData[label];
                    var vp = new PointPairList();
                    for (int i = 0; i < v.Count; i++) vp.Add(i * 0.01, v[i]);
                    Color velColor = label.ToLower().Contains("after") ? Color.Orange : Color.Green;
                    var vCurve = graphPanePedal.AddCurve(label + " Pedal Velocity",
                        vp, velColor, SymbolType.None);
                    vCurve.IsY2Axis = true;
                    vCurve.Line.Width = 2;
                }

                // スタートライン
                if (pedalStartIndices.ContainsKey(label))
                {
                    int startIdx = pedalStartIndices[label];
                    if (startIdx >= 0 && startIdx < pedalData.Count)
                    {
                        double t0 = startIdx * 0.01;
                        var startLine = new LineObj(Color.DarkGreen, t0, graphPanePedal.YAxis.Scale.Min,
                                                    t0, graphPanePedal.YAxis.Scale.Max)
                        {
                            Line = { Style = System.Drawing.Drawing2D.DashStyle.Dash, Width = 5 },
                            IsClippedToChartRect = true
                        };
                        graphPanePedal.GraphObjList.Add(startLine);
                    }
                }

                colorIndex++;
            }

            graphPanePedal.AxisChange();
            zedGraphPedal.Invalidate();


            // ===== 左下：速度（新規描画） =====
            graphPaneVelocity.CurveList.Clear();
            colorIndex = 0;

            foreach (var kvp in pedalVelocityData)
            {
                string label = kvp.Key;
                var v = kvp.Value;
                var vp = new PointPairList();
                for (int i = 0; i < v.Count; i++) vp.Add(i * 0.01, v[i]);

                var vCurve = graphPaneVelocity.AddCurve(label + " Velocity",
                    vp, colors[colorIndex % colors.Length], SymbolType.None);
                vCurve.Line.Width = 2;
                colorIndex++;
            }

            graphPaneVelocity.AxisChange();
            zedGraphVelocity.Invalidate();


            // ===== 右上：ジャーク（新規描画：v→a→j を中央差分） =====
            graphPaneJerk.CurveList.Clear();
            colorIndex = 0;

            foreach (var kvp in pedalVelocityData)
            {
                string label = kvp.Key;
                List<double> j;
                if (pedalJerkData.TryGetValue(label, out var jProvided))
                    j = jProvided;                  // 明示的に渡されたものを優先
                else
                {
                    var a = CenteredDiff(kvp.Value, 0.01);
                    j = CenteredDiff(a, 0.01);     // なければ従来通り内部計算
                }

                var jp = new PointPairList();
                for (int i = 0; i < j.Count; i++) jp.Add(i * 0.01, j[i]);
                var jCurve = graphPaneJerk.AddCurve(label + " Jerk", jp, colors[colorIndex % colors.Length], SymbolType.None);
                jCurve.Line.Width = 2;
                colorIndex++;
            }

            graphPaneJerk.AxisChange();
            zedGraphJerk.Invalidate();
        }




        public void SetSegmentData(string label, List<PedalSegment> segments)
        {
            if (pedalSegments.ContainsKey(label))
            {
                pedalSegments[label] = segments;
            }
            else
            {
                pedalSegments.Add(label, segments);
            }
        }



        private static List<double> CenteredDiff(List<double> x, double dt)
        {
            int n = x?.Count ?? 0;
            var y = new List<double>(n);
            if (n == 0) return y;

            for (int i = 0; i < n; i++) y.Add(0.0);

            if (n >= 2)
            {
                y[0] = (x[1] - x[0]) / dt;
                y[n - 1] = (x[n - 1] - x[n - 2]) / dt;
            }
            for (int i = 1; i < n - 1; i++)
                y[i] = (x[i + 1] - x[i - 1]) / (2.0 * dt);

            return y;
        }

        // 受け取りメソッドを追加
        public void SetJerkData(string label, List<double> jerk)
        {
            pedalJerkData[label] = jerk;
        }



    }
}