using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Waveplus.DaqSys;
using Waveplus.DaqSysInterface;
using ZedGraph;
using System.Threading;
using WaveplusLab.Shared.Definitions;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Tao.OpenGl;
using System.Drawing.Imaging;
using Tao.Platform.Windows;
using CoreLinkSys1.UI;
using CoreLinkSys1.AHRS;
using CoreLinkSys1.Analysis;
using CoreLinkSys1.Devices;
using CoreLinkSys1.Data;
using CoreLinkSys1.Utilities;
using System.Reflection;

namespace CoreLinkSys1
{
    public partial class MainForm : Form
    {
        static AHRS.MadgwickAHRS AHRS = new AHRS.MadgwickAHRS(1f / 100f, 0.3f);

        // 複数のCSVデータと処理済みデータを管理
        static List<CsvRecord> csvDataBeforeA = new List<CsvRecord>();
        static List<CsvRecord> csvDataAfterA = new List<CsvRecord>();
        static List<CsvRecord> csvDataBeforeB = new List<CsvRecord>();
        static List<CsvRecord> csvDataAfterB = new List<CsvRecord>();

        static List<ProcessedRecord> processedDataBeforeA = new List<ProcessedRecord>();
        static List<ProcessedRecord> processedDataAfterA = new List<ProcessedRecord>();
        static List<ProcessedRecord> processedDataBeforeB = new List<ProcessedRecord>();
        static List<ProcessedRecord> processedDataAfterB = new List<ProcessedRecord>();

        static int trim = 0;

        // UI コンポーネント
        private Button btnLoadBeforeA;
        private Button btnLoadAfterA;
        private Button btnLoadBeforeB;
        private Button btnLoadAfterB;
        private Button btnProcess;
        private TextBox txtLog;
        private Button btnShowCycles;

        // 4つのグラフフォーム
        private Form_2Dgraph graphFormBeforeA;
        private Form_2Dgraph graphFormAfterA;
        private Form_2Dgraph graphFormBeforeB;
        private Form_2Dgraph graphFormAfterB;

        private BaysianSurpriseKalmanFilter _bayesianSurprise;

        public MainForm()
        {
            InitializeComponent();

            // 4つのグラフフォームを初期化
            InitializeGraphForms();
        }

        private void InitializeGraphForms()
        {
            graphFormBeforeA = new Form_2Dgraph();
            graphFormBeforeA.Text = "Before A - Analysis Results";
            graphFormBeforeA.Location = new Point(50, 50);
            graphFormBeforeA.Show();

            graphFormAfterA = new Form_2Dgraph();
            graphFormAfterA.Text = "After A - Analysis Results";
            graphFormAfterA.Location = new Point(650, 50);
            graphFormAfterA.Show();

            graphFormBeforeB = new Form_2Dgraph();
            graphFormBeforeB.Text = "Before B - Analysis Results";
            graphFormBeforeB.Location = new Point(50, 450);
            graphFormBeforeB.Show();

            graphFormAfterB = new Form_2Dgraph();
            graphFormAfterB.Text = "After B - Analysis Results";
            graphFormAfterB.Location = new Point(650, 450);
            graphFormAfterB.Show();
        }

        private void InitializeComponent()
        {
            this.btnLoadBeforeA = new Button();
            this.btnLoadAfterA = new Button();
            this.btnLoadBeforeB = new Button();
            this.btnLoadAfterB = new Button();
            this.btnProcess = new Button();
            this.txtLog = new TextBox();
            this.btnShowCycles = new Button();

            // Before A CSV読み込みボタン
            this.btnLoadBeforeA.Text = "Before A CSV";
            this.btnLoadBeforeA.Location = new System.Drawing.Point(20, 20);
            this.btnLoadBeforeA.Size = new System.Drawing.Size(100, 30);
            this.btnLoadBeforeA.Click += new EventHandler(this.BtnLoadBeforeA_Click);

            // After A CSV読み込みボタン
            this.btnLoadAfterA.Text = "After A CSV";
            this.btnLoadAfterA.Location = new System.Drawing.Point(130, 20);
            this.btnLoadAfterA.Size = new System.Drawing.Size(100, 30);
            this.btnLoadAfterA.Click += new EventHandler(this.BtnLoadAfterA_Click);

            // Before B CSV読み込みボタン
            this.btnLoadBeforeB.Text = "Before B CSV";
            this.btnLoadBeforeB.Location = new System.Drawing.Point(240, 20);
            this.btnLoadBeforeB.Size = new System.Drawing.Size(100, 30);
            this.btnLoadBeforeB.Click += new EventHandler(this.BtnLoadBeforeB_Click);

            // After B CSV読み込みボタン
            this.btnLoadAfterB.Text = "After B CSV";
            this.btnLoadAfterB.Location = new System.Drawing.Point(350, 20);
            this.btnLoadAfterB.Size = new System.Drawing.Size(100, 30);
            this.btnLoadAfterB.Click += new EventHandler(this.BtnLoadAfterB_Click);

            // 解析ボタン
            this.btnProcess.Text = "解析";
            this.btnProcess.Location = new System.Drawing.Point(20, 60);
            this.btnProcess.Size = new System.Drawing.Size(100, 30);
            this.btnProcess.Click += new EventHandler(this.BtnProcess_Click);

            // サイクル表示ボタン
            this.btnShowCycles.Text = "サイクル表示";
            this.btnShowCycles.Location = new System.Drawing.Point(130, 60);
            this.btnShowCycles.Size = new System.Drawing.Size(100, 30);
            this.btnShowCycles.Click += new EventHandler(this.BtnShowCycles_Click);

            // ログテキストボックス
            this.txtLog.Multiline = true;
            this.txtLog.ScrollBars = ScrollBars.Vertical;
            this.txtLog.Location = new System.Drawing.Point(20, 100);
            this.txtLog.Width = 500;
            this.txtLog.Height = 300;

            this.Text = "Offline AHRS Analyzer - Multiple CSV";
            this.Width = 600;
            this.Height = 450;
            this.Controls.Add(this.btnLoadBeforeA);
            this.Controls.Add(this.btnLoadAfterA);
            this.Controls.Add(this.btnLoadBeforeB);
            this.Controls.Add(this.btnLoadAfterB);
            this.Controls.Add(this.btnProcess);
            this.Controls.Add(this.btnShowCycles);
            this.Controls.Add(this.txtLog);
        }

        private void BtnLoadBeforeA_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv";
                ofd.Title = "Before A CSVファイルを選択";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadCsvData(ofd.FileName, csvDataBeforeA);
                    Log("Before A CSV loaded: " + ofd.FileName);
                }
            }
        }

        private void BtnLoadAfterA_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv";
                ofd.Title = "After A CSVファイルを選択";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadCsvData(ofd.FileName, csvDataAfterA);
                    Log("After A CSV loaded: " + ofd.FileName);
                }
            }
        }

        private void BtnLoadBeforeB_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv";
                ofd.Title = "Before B CSVファイルを選択";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadCsvData(ofd.FileName, csvDataBeforeB);
                    Log("Before B CSV loaded: " + ofd.FileName);
                }
            }
        }

        private void BtnLoadAfterB_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv";
                ofd.Title = "After B CSVファイルを選択";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadCsvData(ofd.FileName, csvDataAfterB);
                    Log("After B CSV loaded: " + ofd.FileName);
                }
            }
        }

        private void BtnProcess_Click(object sender, EventArgs e)
        {
            Log("Processing all data...");

            // 各データセットを処理
            ProcessDataSet_A("After A", csvDataAfterA, processedDataAfterA, graphFormAfterA);
            ProcessDataSet_A("Before A", csvDataBeforeA, processedDataBeforeA, graphFormBeforeA);

            ProcessDataSet_B("Before B", csvDataBeforeB, processedDataBeforeB, graphFormBeforeB);
            ProcessDataSet_B("After B", csvDataAfterB, processedDataAfterB, graphFormAfterB);

            // ★ 棒グラフを表示
            ShowBarGraphComparison();


            Log("All processing finished!");
        }

        private void ProcessDataSet_A(string dataSetName, List<CsvRecord> csvData,
                                  List<ProcessedRecord> processedData, Form_2Dgraph graphForm)
        {
            if (csvData.Count == 0)
            {
                Log($"No CSV data loaded for {dataSetName}.");
                return;
            }

            Log($"Processing {dataSetName}...");
            processedData.Clear();

            var extractor = new LocalCoordinateExtractor(100f, 0.3f, AHRS);


            //---1ループ目暫定処理
            int decimation = 1; // 2000Hz→100Hz
            for (int i = 0; i < csvData.Count; i += decimation)
            {
                var record = csvData[i];

                var converted = MadgwickAHRS.ConvertSensorAxes(
                         record.GyroX, record.GyroY, record.GyroZ,
                         record.AccX, record.AccY, record.AccZ,
                         record.MagX, record.MagY, record.MagZ
                     );

                float convertedGyroX = converted[0];
                float convertedGyroY = converted[1];
                float convertedGyroZ = converted[2];
                float convertedAccX = converted[3];
                float convertedAccY = converted[4];
                float convertedAccZ = converted[5];
                float convertedMagX = converted[6];
                float convertedMagY = converted[7];
                float convertedMagZ = converted[8];

                // Gyroをrad/sに変換
                float gx = MadgwickAHRS.deg2rad(convertedGyroX);
                float gy = MadgwickAHRS.deg2rad(convertedGyroY);
                float gz = MadgwickAHRS.deg2rad(convertedGyroZ);

                // LocalCoordinateExtractorで処理
                var result = extractor.ProcessSample(gx, gy, gz, convertedAccX, convertedAccY, convertedAccZ);

                processedData.Add(new ProcessedRecord
                {
                    Time = record.Time,
                    RawAccX = convertedAccX,
                    RawAccY = convertedAccY,
                    RawAccZ = convertedAccZ,
                    RawGyroX = convertedGyroX,
                    RawGyroY = convertedGyroY,
                    RawGyroZ = convertedGyroZ,
                    RawMagX = convertedMagX,
                    RawMagY = convertedMagY,
                    RawMagZ = convertedMagZ,
                    LinearAccX = result.LinearAcc[0],
                    LinearAccY = result.LinearAcc[1],
                    LinearAccZ = result.LinearAcc[2],
                    GlobalAccX = result.GlobalHoriz[0],
                    GlobalAccY = result.GlobalHoriz[1],
                    GlobalAccZ = result.GlobalHoriz[2],
                    QuaternionX = result.Quaternion[0],
                    QuaternionY = result.Quaternion[1],
                    QuaternionZ = result.Quaternion[2],
                    QuaternionW = result.Quaternion[3],
                    ForwardAcc = result.ForwardAcc//暫定値
                });
            }

            
            // === ★ ForwardAcc を固定方向で再投影 ===
            if (processedData.Count > 1)
            {
                float dx = processedData.Last().GlobalAccX - processedData.First().GlobalAccX;
                float dy = processedData.Last().GlobalAccY - processedData.First().GlobalAccY;

                float norm = (float)Math.Sqrt(dx * dx + dy * dy);
                if (norm > 1e-6)
                {
                    float fx = dx / norm;
                    float fy = dy / norm;


                    for(int i = 0; i < csvData.Count; i += decimation)
                    {
                        var rec = processedData[i];
                        rec.ForwardAcc = rec.GlobalAccX * fx + rec.GlobalAccY * fy; // 再投影
                        processedData[i] = rec;
                    }
                }
            }



            // 歩行検出
            var detector = new RobustWalkingDetector(100);
            detector.DetectWalking(processedData);

            // GaitCycleDetector
            var gaitDetector = new RobustGaitCycleDetector(100);

            float[] accX = processedData.Select(r => r.RawAccX).ToArray();
            float[] accY = processedData.Select(r => r.RawAccY).ToArray();
            float[] accZ = processedData.Select(r => r.RawAccZ).ToArray();
            float[] gyroX = processedData.Select(r => r.RawGyroX).ToArray();
            float[] gyroY = processedData.Select(r => r.RawGyroY).ToArray();
            float[] gyroZ = processedData.Select(r => r.RawGyroZ).ToArray();
            float[] time = processedData.Select(r => r.Time).ToArray();

            float[] globalAccX = processedData.Select(r => r.GlobalAccX).ToArray();
            float[] forwardAcc = processedData.Select(r => r.ForwardAcc).ToArray();

            // ピーク検出(accZベース)
            var peaks = gaitDetector.RobustPeakDetection(accZ, time);

            // ピーク検出（ForwardAcc）
            var peaksForward = gaitDetector.RobustPeakDetection(globalAccX, time);

            // ForwardAcc に基づくラベル付与
            for (int i = 0; i < peaksForward.Count; i++)
            {
                int idx = peaksForward[i];
                var rec = processedData[idx];
                string forwardLabel = (i % 2 == 0) ? "R_F" : "L_F";
                rec.StepLabel = forwardLabel;
                rec.StepConfidence = 1.0f;
                processedData[idx] = rec;
            }

            // 4手法でラベル付け
            var labelsGyroZ = gaitDetector.LabelByGyroZ(gyroZ, peaks, 100);
            var labelsGyroVec = gaitDetector.LabelByGyroVector(gyroX, gyroY, gyroZ, peaks, 100);
            var labelsAcc = gaitDetector.LabelByAccPattern(accX, accY, peaks, 100);
            var labelsTemplate = gaitDetector.LabelByTemplate(gyroZ, peaks, 100);

            // アンサンブル統合
            var methodResults = new List<List<string>>() { labelsGyroZ, labelsGyroVec, labelsAcc, labelsTemplate };
            var ensemble = gaitDetector.EnsembleLabeling(methodResults, peaks.Count);

            // 交互性補正
            var corrected = gaitDetector.EnforceAlternatingPattern(ensemble.FinalLabels);

            // ForwardAcc ピーク値計算
            List<float> forwardPeakValues = new List<float>();
            foreach (int idx in peaksForward)
            {
                forwardPeakValues.Add(forwardAcc[idx]);
            }

            float forwardPeakMean = 0;
            if (forwardPeakValues.Count > 0)
                forwardPeakMean = forwardPeakValues.Average();

            Log(String.Format("{0} ForwardAcc ピーク平均値: {1:F3}", dataSetName, forwardPeakMean));

            // processedData に反映
            for (int i = 0; i < peaks.Count && i < corrected.Count; i++)
            {
                int idx = peaks[i];
                var rec = processedData[idx];
                rec.StepLabel = corrected[i];
                rec.StepConfidence = (float)ensemble.Confidence[i];
                processedData[idx] = rec;
            }

            // データ出力
            ExportProcessedData(processedData, dataSetName);

            // グラフに描画
            PlotData(processedData, graphForm);

            Log($"{dataSetName} processing completed!");
        }

        private void ProcessDataSet_B(string dataSetName, List<CsvRecord> csvData,
                              List<ProcessedRecord> processedData,
                              Form_2Dgraph graphForm)
        {
            if (csvData.Count == 0)
            {
                Log($"No CSV data loaded for {dataSetName}.");
                return;
            }

            Log($"Processing {dataSetName} with Bayesian Surprise...");

            processedData.Clear();

            // --- B用の初期化 ---
            int meanRange = 200; // 例: 初期姿勢推定に200サンプル使用
            _bayesianSurprise = new BaysianSurpriseKalmanFilter(1f / 100f, meanRange);

            // CSVデータを2次元配列に変換（Initializeに渡すため）
            float[,] initSignal = new float[Math.Min(meanRange, csvData.Count), 6];
            for (int i = 0; i < initSignal.GetLength(0); i++)
            {


                var record = csvData[i];

                var converted = MadgwickAHRS.ConvertSensorAxes(
                         record.GyroX, record.GyroY, record.GyroZ,
                         record.AccX, record.AccY, record.AccZ,
                         record.MagX, record.MagY, record.MagZ
                     );

                float convertedGyroX = converted[0];
                float convertedGyroY = converted[1];
                float convertedGyroZ = converted[2];
                float convertedAccX = converted[3];
                float convertedAccY = converted[4];
                float convertedAccZ = converted[5];
                float convertedMagX = converted[6];
                float convertedMagY = converted[7];
                float convertedMagZ = converted[8];


                initSignal[i, 0] = MadgwickAHRS.deg2rad(convertedGyroX);
                initSignal[i, 1] = MadgwickAHRS.deg2rad(convertedGyroY);
                initSignal[i, 2] = MadgwickAHRS.deg2rad(convertedGyroZ);
                initSignal[i, 3] = convertedAccX;
                initSignal[i, 4] = convertedAccY;
                initSignal[i, 5] = convertedAccZ;
            }


            _bayesianSurprise.Initialize(initSignal);

            // --- メインループ ---
            for (int i = 0; i < csvData.Count; i++)
            {
                var record = csvData[i];

                var converted = MadgwickAHRS.ConvertSensorAxes(
                         record.GyroX, record.GyroY, record.GyroZ,
                         record.AccX, record.AccY, record.AccZ,
                         record.MagX, record.MagY, record.MagZ
                     );

                float convertedGyroX = converted[0];
                float convertedGyroY = converted[1];
                float convertedGyroZ = converted[2];
                float convertedAccX = converted[3];
                float convertedAccY = converted[4];
                float convertedAccZ = converted[5];
                float convertedMagX = converted[6];
                float convertedMagY = converted[7];
                float convertedMagZ = converted[8];

                float surprise = _bayesianSurprise.ProcessSample(
                    record.Time,
                    MadgwickAHRS.deg2rad(convertedGyroX),
                    MadgwickAHRS.deg2rad(convertedGyroY),
                    MadgwickAHRS.deg2rad(convertedGyroZ),
                    convertedAccX,
                    convertedAccY,
                    convertedAccZ

                );

                processedData.Add(new ProcessedRecord
                {
                    Time = record.Time,
                    RawAccX = record.AccX,
                    RawAccY = record.AccY,
                    RawAccZ = record.AccZ,
                    RawGyroX = record.GyroX,
                    RawGyroY = record.GyroY,
                    RawGyroZ = record.GyroZ,
                    // Bayesian Surpriseの出力をWalkingScoreに格納（例）
                    WalkingScore = surprise
                });
            }

            // グラフに描画
            PlotData(processedData, graphForm);

            // CSV出力
            ExportProcessedData(processedData, dataSetName);

            Log($"{dataSetName} (Bayesian) processing completed!");
        }


        // ForwardAcc の正規化サイクルを別フォームで表示
        private void BtnShowCycles_Click(object sender, EventArgs e)
        {
            // Before Aデータでサイクル表示（他のデータセットでも同様に実装可能）
            if (processedDataBeforeA.Count > 0)
            {
                ShowCyclesForDataSet(processedDataBeforeA, "Before A");
            }
            if (processedDataAfterA.Count > 0)
            {
                ShowCyclesForDataSet(processedDataAfterA, "After A");
            }
        }

        private void ShowCyclesForDataSet(List<ProcessedRecord> processedData, string dataSetName)
        {
            float[] accZ = processedData.Select(r => r.GlobalAccZ).ToArray();
            float[] fwd = processedData.Select(r => r.ForwardAcc).ToArray();
            float[] time = processedData.Select(r => r.Time).ToArray();

            var gaitDetector = new RobustGaitCycleDetector(100);
            var valleys = gaitDetector.RobustValleyDetection(accZ, time);

            Form_CyclePlot cycleForm = new Form_CyclePlot();
            cycleForm.Text = $"{dataSetName} - Cycle Plot";
            cycleForm.Show();

            cycleForm.PlotNormalizedCycles(
                accZ.Select(v => (double)v).ToList(),
                fwd.Select(v => (double)v).ToList(),
                time.Select(t => (double)t).ToList(),
                valleys,
                100
            );
        }

        private void Log(string message)
        {
            txtLog.AppendText(message + Environment.NewLine);
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void LoadCsvData(string filePath, List<CsvRecord> targetList)
        {
            targetList.Clear();
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length >= 10)
                {
                    targetList.Add(new CsvRecord
                    {
                        Time = float.Parse(parts[0]),
                        AccX = float.Parse(parts[1]),
                        AccY = float.Parse(parts[2]),
                        AccZ = float.Parse(parts[3]),
                        GyroX = float.Parse(parts[4]),
                        GyroY = float.Parse(parts[5]),
                        GyroZ = float.Parse(parts[6]),
                        MagX = float.Parse(parts[7]),
                        MagY = float.Parse(parts[8]),
                        MagZ = float.Parse(parts[9])
                    });
                }
            }

            if (targetList.Count > trim)
                targetList = targetList.Skip(trim).ToList();
        }

        private void PlotData(List<ProcessedRecord> processedData, Form_2Dgraph graphForm)
        {
            foreach (var r in processedData)
            {
                graphForm.AddData(r.Time,
                  r.RawGyroX, r.RawGyroY, r.RawGyroZ,
                  r.RawAccX, r.RawAccY, r.RawAccZ,
                  r.GlobalAccX, r.GlobalAccY, r.GlobalAccZ,
                  r.EulerRoll, r.EulerPitch, r.EulerYaw,
                  r.WalkingScore,
                  r.StepLabel,
                  r.GlobalAccX
                  );
            }
            graphForm.FinalizePlots();
        }


        private float ComputeForwardImpulseAverage(
    List<ProcessedRecord> processedData,
    int fs = 100) // サンプリング周波数 [Hz]
        {
            if (processedData.Count == 0) return 0;

            // 必要なデータを抽出
            float[] accZ = processedData.Select(r => r.GlobalAccZ).ToArray();
            float[] forwardAcc = processedData.Select(r => r.ForwardAcc).ToArray();
            float[] time = processedData.Select(r => r.Time).ToArray();

            // --- GlobalAccZ でサイクル分割 (谷検出) ---
            var gaitDetector = new RobustGaitCycleDetector(fs);
            var valleys = gaitDetector.RobustValleyDetection(accZ, time);

            List<double> impulses = new List<double>();

            for (int j = 0; j < valleys.Count - 1; j++)
            {
                int startIdx = valleys[j];
                int endIdx = valleys[j + 1];

                if (endIdx >= forwardAcc.Length || startIdx < 0) continue;

                double impulse = 0;

                // --- ForwardAcc の正の部分だけ積分（台形公式） ---
                for (int i = startIdx; i < endIdx; i++)
                {
                    double a1 = Math.Max(forwardAcc[i], 0);
                    double a2 = Math.Max(forwardAcc[i + 1], 0);
                    double dt = time[i + 1] - time[i];
                    impulse += 0.5 * (a1 + a2) * dt;
                }

                impulses.Add(impulse);
            }

            // サイクル平均を返す
            return impulses.Count > 0 ? (float)impulses.Average() : 0f;
        }




        private void ShowBarGraphComparison()
        {
            // --- A系 (ForwardAcc平均) ---
            float forwardBefore = 0;
            float forwardAfter = 0;

            if (processedDataBeforeA.Count > 0)
            {
                //var values = processedDataBeforeA.Where(r => r.ForwardAcc != 0).Select(r => r.ForwardAcc).ToList();
                //if (values.Count > 0) forwardBefore = values.Average();
                forwardBefore = ComputeForwardImpulseAverage(processedDataBeforeA, 100);
                
            }

            if (processedDataAfterA.Count > 0)
            {
                //var values = processedDataAfterA.Where(r => r.ForwardAcc != 0).Select(r => r.ForwardAcc).ToList();
                //if (values.Count > 0) forwardAfter = values.Average();
                forwardAfter = ComputeForwardImpulseAverage(processedDataAfterA, 100);
            }

            // --- B系 (Surprise平均 → WalkingScoreに格納済み) ---
            float surpriseBefore = 0;
            float surpriseAfter = 0;

            if (processedDataBeforeB.Count > 0)
            {
                //var values = processedDataBeforeB.Select(r => r.WalkingScore).ToList();
                var values = processedDataBeforeB
                .Select(r => r.WalkingScore)
                .Where(v => v > 0)  // ★ 負の値を除外
                .Select(v => (float)Math.Log(v + 1)) // ★ log(surprise + 1) に変換
                .ToList();

                if (values.Count > 0) surpriseBefore = values.Average();
            }

            if (processedDataAfterB.Count > 0)
            {
                //var values = processedDataAfterB.Select(r => r.WalkingScore).ToList();
                var values = processedDataAfterB
                .Select(r => r.WalkingScore)
                .Where(v => v > 0)  // ★ 負の値を除外
                .Select(v => (float)Math.Log(v + 1)) // ★ log(surprise + 1) に変換
                .ToList();
                if (values.Count > 0) surpriseAfter = values.Average();
            }

            // --- 棒グラフフォーム表示 ---
            Form_BarGraph barForm = new Form_BarGraph();
            barForm.PlotBarGraph(forwardBefore, forwardAfter, surpriseBefore, surpriseAfter);
            barForm.Show();
        }

        private void ExportProcessedData(List<ProcessedRecord> processedData, string dataSetName)
        {
            if (processedData.Count == 0) return;

            string exportPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                $"processed_data_{dataSetName}_{DateTime.Now:yyyyMMddHHmmss}.csv");

            using (var writer = new StreamWriter(exportPath))
            {
                writer.WriteLine("time,RawAccX,RawAccY,RawAccZ,RawGyroX,RawGyroY,RawGyroZ,GlobalAccX,GlobalAccY,GlobalAccZ,Roll,Pitch,Yaw,StepLabel");

                foreach (var r in processedData)
                {
                    writer.WriteLine(String.Format(
                        "{0:F3},{1:F3},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3},{10:F2},{11:F2},{12:F2},{13}",
                        r.Time, r.RawAccX, r.RawAccY, r.RawAccZ,
                        r.RawGyroX, r.RawGyroY, r.RawGyroZ,
                        r.GlobalAccX, r.GlobalAccY, r.GlobalAccZ,
                        r.EulerRoll, r.EulerPitch, r.EulerYaw,
                        r.StepLabel ?? ""));
                }
            }
        }
    }

    public struct CsvRecord
    {
        public float Time, AccX, AccY, AccZ, GyroX, GyroY, GyroZ, MagX, MagY, MagZ;
    }

    public struct ProcessedRecord
    {
        public float Time;
        public float RawAccX, RawAccY, RawAccZ;
        public float RawGyroX, RawGyroY, RawGyroZ;
        public float RawMagX, RawMagY, RawMagZ;
        public float LinearAccX, LinearAccY, LinearAccZ;
        public float GlobalAccX, GlobalAccY, GlobalAccZ;
        public float QuaternionX, QuaternionY, QuaternionZ, QuaternionW;
        public float EulerRoll, EulerPitch, EulerYaw;

        // RobustWalkingDetector 出力
        public float WalkingScore;
        public int WalkingBinary;
        public int WalkId;

        // RobustGaitCycleDetector 出力
        public string StepLabel;
        public float StepConfidence;

        public float ForwardAcc;
    }
}