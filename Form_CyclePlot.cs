using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;

namespace CoreLinkSys1.UI
{
    public partial class Form_CyclePlot : Form
    {
        private ZedGraphControl zedGraphAcc;   // GlobalAccZ 用
        private ZedGraphControl zedGraphForward; // ForwardAcc 用

        public Form_CyclePlot()
        {
            InitializeComponent();
            InitializeGraphs();
        }

        private void InitializeComponent()
        {
            this.Text = "Normalized Gait Cycles (AccZ & ForwardAcc)";
            this.ClientSize = new Size(800, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 上段 (AccZ)
            zedGraphAcc = new ZedGraphControl();
            zedGraphAcc.Dock = DockStyle.Top;
            zedGraphAcc.Height = this.ClientSize.Height / 2;

            // 下段 (ForwardAcc)
            zedGraphForward = new ZedGraphControl();
            zedGraphForward.Dock = DockStyle.Fill;

            this.Controls.Add(zedGraphForward);
            this.Controls.Add(zedGraphAcc);
        }

        private void InitializeGraphs()
        {
            GraphPane paneAcc = zedGraphAcc.GraphPane;
            paneAcc.Title.Text = "Normalized Cycles - GlobalAccZ";
            paneAcc.XAxis.Title.Text = "Normalized Gait Cycle [%]";
            paneAcc.YAxis.Title.Text = "GlobalAccZ";

            GraphPane paneFwd = zedGraphForward.GraphPane;
            paneFwd.Title.Text = "Normalized Cycles - ForwardAcc";
            paneFwd.XAxis.Title.Text = "Normalized Gait Cycle [%]";
            paneFwd.YAxis.Title.Text = "ForwardAcc";
        }

        /// <summary>
        /// 2種類の信号を同じ谷区間で正規化して重ね描きする
        /// </summary>
        public void PlotNormalizedCycles(
            List<double> accZ,
            List<double> forwardAcc,
            List<double> time,
            List<int> valleys,
            int cycleLength = 100)
        {
            GraphPane paneAcc = zedGraphAcc.GraphPane;
            GraphPane paneFwd = zedGraphForward.GraphPane;
            paneAcc.CurveList.Clear();
            paneFwd.CurveList.Clear();

            Random rnd = new Random();

            for (int j = 0; j < valleys.Count - 2; j += 2)
            {
                int startIdx = valleys[j];
                int endIdx = valleys[j + 2];
                if (endIdx >= accZ.Count || startIdx < 0) continue;

                // === GlobalAccZ セグメント ===
                var segTime = time.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
                var segAccZ = accZ.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
                var segFwd = forwardAcc.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();

                double t0 = segTime.First();
                double t1 = segTime.Last();
                List<double> normT = segTime.Select(t => (t - t0) / (t1 - t0)).ToList();

                // リサンプリング関数
                Func<List<double>, double[]> Resample = (seg) =>
                {
                    double[] resampled = new double[cycleLength];
                    for (int k = 0; k < cycleLength; k++)
                    {
                        double targetT = (double)k / (cycleLength - 1);
                        int idx = normT.FindLastIndex(v => v <= targetT);
                        if (idx < 0) idx = 0;
                        if (idx >= normT.Count - 1) idx = normT.Count - 2;

                        double tA = normT[idx];
                        double tB = normT[idx + 1];
                        double vA = seg[idx];
                        double vB = seg[idx + 1];

                        double alpha = (targetT - tA) / (tB - tA);
                        resampled[k] = vA + alpha * (vB - vA);
                    }
                    return resampled;
                };

                double[] accZ_resampled = Resample(segAccZ);
                double[] fwd_resampled = Resample(segFwd);

                double[] xVals = new double[cycleLength];
                for (int k = 0; k < cycleLength; k++)
                    xVals[k] = (double)k / (cycleLength - 1) * 100.0;

                Color col = Color.FromArgb(120, rnd.Next(50, 200), rnd.Next(50, 200), rnd.Next(50, 200));

                // GlobalAccZ 曲線
                LineItem curveAcc = paneAcc.AddCurve($"Cycle {j / 2}", xVals, accZ_resampled, col, SymbolType.None);
                curveAcc.Line.Width = 1.5f;

                // ForwardAcc 曲線
                LineItem curveFwd = paneFwd.AddCurve($"Cycle {j / 2}", xVals, fwd_resampled, col, SymbolType.None);
                curveFwd.Line.Width = 1.5f;
            }

            paneAcc.AxisChange();
            paneFwd.AxisChange();
            zedGraphAcc.Invalidate();
            zedGraphForward.Invalidate();
        }
    }
}
