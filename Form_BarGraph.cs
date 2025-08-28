using System;
using System.Drawing;
using System.Windows.Forms;
using ZedGraph;

namespace CoreLinkSys1.UI
{
    public partial class Form_BarGraph : Form
    {
        private ZedGraphControl zedGraphBar;   // 棒グラフ
        private ZedGraphControl zedGraphScatter; // 散布図

        public Form_BarGraph()
        {
            InitializeComponent();
            InitializeGraphs();
        }

        private void InitializeComponent()
        {
            this.zedGraphBar = new ZedGraphControl();
            this.zedGraphScatter = new ZedGraphControl();
            this.SuspendLayout();

            // 上段：棒グラフ
            this.zedGraphBar.Dock = DockStyle.Top;
            this.zedGraphBar.Height = this.ClientSize.Height / 2;

            // 下段：散布図
            this.zedGraphScatter.Dock = DockStyle.Fill;

            this.Controls.Add(this.zedGraphScatter);
            this.Controls.Add(this.zedGraphBar);

            this.Text = "Before/After Comparison (ForwardAcc & Surprise)";
            this.Width = 600;
            this.Height = 800;

            this.ResumeLayout(false);
        }

        private void InitializeGraphs()
        {
            // 棒グラフ
            GraphPane paneBar = zedGraphBar.GraphPane;
            paneBar.Title.Text = "Before/After Comparison";
            paneBar.XAxis.Title.Text = "Condition";
            paneBar.YAxis.Title.Text = "ForwardAcc Mean";
            paneBar.Y2Axis.IsVisible = true;
            paneBar.Y2Axis.Title.Text = "Surprise Mean";

            // 散布図
            GraphPane paneScatter = zedGraphScatter.GraphPane;
            paneScatter.Title.Text = "ForwardAcc vs Surprise";
            paneScatter.XAxis.Title.Text = "ForwardAcc Mean";
            paneScatter.YAxis.Title.Text = "Surprise Mean";
        }

        /// <summary>
        /// 棒グラフと散布図を描画
        /// </summary>
        public void PlotBarGraph(float forwardBefore, float forwardAfter, float surpriseBefore, float surpriseAfter)
        {
            // ===== 棒グラフ =====
            GraphPane paneBar = zedGraphBar.GraphPane;
            paneBar.CurveList.Clear();

            string[] labels = { "Before", "After" };
            double[] x = { 0, 1 };

            // ForwardAcc 平均 (左Y軸)
            double[] forwardMeans = { forwardBefore, forwardAfter };
            BarItem curve1 = paneBar.AddBar("ForwardAcc Mean", x, forwardMeans, Color.Blue);
            curve1.Bar.Fill = new Fill(Color.Blue, Color.LightBlue, Color.Blue);
            curve1.YAxisIndex = 0;

            // Surprise 平均 (右Y軸)
            double[] surpriseMeans = { surpriseBefore, surpriseAfter };
            BarItem curve2 = paneBar.AddBar("Surprise Mean", x, surpriseMeans, Color.Red);
            curve2.Bar.Fill = new Fill(Color.Red, Color.Pink, Color.Red);
            curve2.IsY2Axis = true;

            // X軸ラベル
            paneBar.XAxis.Type = AxisType.Text;
            paneBar.XAxis.Scale.TextLabels = labels;
            paneBar.BarSettings.Type = BarType.Cluster;

            zedGraphBar.AxisChange();
            zedGraphBar.Invalidate();

            // ===== 散布図 =====
            GraphPane paneScatter = zedGraphScatter.GraphPane;
            paneScatter.CurveList.Clear();

            PointPairList list = new PointPairList();
            list.Add(forwardBefore, surpriseBefore); // Before
            list.Add(forwardAfter, surpriseAfter);   // After

            LineItem scatterCurve = paneScatter.AddCurve(
                "Before/After",
                list,
                Color.Black,
                SymbolType.Circle
            );
            scatterCurve.Line.IsVisible = false; // 点のみ
            scatterCurve.Symbol.Fill = new Fill(Color.Green);
            scatterCurve.Symbol.Size = 12;

            // ラベルを追加
            TextObj labelBefore = new TextObj("Before", forwardBefore, surpriseBefore,
                CoordType.AxisXYScale, AlignH.Left, AlignV.Bottom);
            labelBefore.FontSpec.Border.IsVisible = false;
            labelBefore.FontSpec.Fill.IsVisible = false;
            paneScatter.GraphObjList.Add(labelBefore);

            TextObj labelAfter = new TextObj("After", forwardAfter, surpriseAfter,
                CoordType.AxisXYScale, AlignH.Left, AlignV.Bottom);
            labelAfter.FontSpec.Border.IsVisible = false;
            labelAfter.FontSpec.Fill.IsVisible = false;
            paneScatter.GraphObjList.Add(labelAfter);

            zedGraphScatter.AxisChange();
            zedGraphScatter.Invalidate();
        }
    }
}
