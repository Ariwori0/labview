using CoreLinkSys1.Analysis;
using CoreLinkSys1.UI;
using CoreLinkSys1.Utilities;
using CoreLinkSys1.LabviewBridge;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;

namespace CoreLinkSys1
{
    public partial class MainForm : Form
    {
        private Button btnLvmBeforeA;
        private Button btnLvmBeforeB;
        private Button btnLvmAfterA;
        private Button btnLvmAfterB;
        private Button btnShowGraph;
        private Button btnPreviewLVM;
        private Button btnDebugLVM;

        // ファイル名表示用ラベル
        private System.Windows.Forms.Label lblLvmBeforeA;
        private System.Windows.Forms.Label lblLvmBeforeB;
        private System.Windows.Forms.Label lblLvmAfterA;
        private System.Windows.Forms.Label lblLvmAfterB;
        private System.Windows.Forms.Label lblResult;

        // プログレス表示用コントロール
        private ProgressBar progressBar;
        private System.Windows.Forms.Label lblProgress;
        private bool isProcessing = false;

        private OpenFileDialog openFileDialog;
        private Form_StdDevPlot stdDevPlotForm;

        // LVMファイルパスを保持
        private string beforeALvmPath = string.Empty;
        private string beforeBLvmPath = string.Empty;
        private string afterALvmPath = string.Empty;
        private string afterBLvmPath = string.Empty;

        public MainForm()
        {
            InitializeComponent();
            InitializeUI();
            InitializeProgressControls();
        }

        private void InitializeUI()
        {
            this.Text = "LVM比較ツール - COP & Pedal";
            this.ClientSize = new Size(700, 550); // デバッグボタン分を拡大
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            Font uiFont = new Font("Meiryo UI", 10, FontStyle.Regular);

            // 計算結果表示ラベル
            lblResult = new System.Windows.Forms.Label
            {
                Location = new Point(20, 280),
                Size = new Size(650, 180),
                Font = new Font("Meiryo UI", 10, FontStyle.Regular),
                Text = "計算結果：未実行",
                TextAlign = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(lblResult);

            // Before A (Pedal)
            btnLvmBeforeA = new Button
            {
                Text = "Before Pedal CSV",
                Location = new Point(20, 30),
                Size = new Size(180, 35),
                Font = uiFont,
                BackColor = Color.LightBlue,
                FlatStyle = FlatStyle.Flat
            };
            // btnLvmBeforeA.Click += delegate { beforeALvmPath = SelectLvmFile(lblLvmBeforeA); };
            btnLvmBeforeA.Click += delegate { beforeALvmPath = SelectCsvFile(lblLvmBeforeA); };
            this.Controls.Add(btnLvmBeforeA);

            lblLvmBeforeA = new System.Windows.Forms.Label
            {
                Location = new Point(220, 35),
                Size = new Size(450, 25),
                Font = uiFont,
                Text = "未選択",
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblLvmBeforeA);

            // Before B (COP)
            btnLvmBeforeB = new Button
            {
                Text = "Before COP LVM",
                Location = new Point(20, 80),
                Size = new Size(180, 35),
                Font = uiFont,
                BackColor = Color.LightBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnLvmBeforeB.Click += delegate { beforeBLvmPath = SelectLvmFile(lblLvmBeforeB); };
            this.Controls.Add(btnLvmBeforeB);

            lblLvmBeforeB = new System.Windows.Forms.Label
            {
                Location = new Point(220, 85),
                Size = new Size(450, 25),
                Font = uiFont,
                Text = "未選択",
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblLvmBeforeB);

            // After A (Pedal)
            btnLvmAfterA = new Button
            {
                Text = "After Pedal CSV",
                Location = new Point(20, 130),
                Size = new Size(180, 35),
                Font = uiFont,
                BackColor = Color.LightCoral,
                FlatStyle = FlatStyle.Flat
            };
            // btnLvmAfterA.Click += delegate { afterALvmPath = SelectLvmFile(lblLvmAfterA); };
            btnLvmAfterA.Click  += delegate { afterALvmPath  = SelectCsvFile(lblLvmAfterA); };
            this.Controls.Add(btnLvmAfterA);

            lblLvmAfterA = new System.Windows.Forms.Label
            {
                Location = new Point(220, 135),
                Size = new Size(450, 25),
                Font = uiFont,
                Text = "未選択",
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblLvmAfterA);

            // After B (COP)
            btnLvmAfterB = new Button
            {
                Text = "After COP LVM",
                Location = new Point(20, 180),
                Size = new Size(180, 35),
                Font = uiFont,
                BackColor = Color.LightCoral,
                FlatStyle = FlatStyle.Flat
            };
            btnLvmAfterB.Click += delegate { afterBLvmPath = SelectLvmFile(lblLvmAfterB); };
            this.Controls.Add(btnLvmAfterB);

            lblLvmAfterB = new System.Windows.Forms.Label
            {
                Location = new Point(220, 185),
                Size = new Size(450, 25),
                Font = uiFont,
                Text = "未選択",
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(lblLvmAfterB);

            // グラフ表示ボタン
            btnShowGraph = new Button
            {
                Text = "グラフ表示",
                Location = new Point(250, 230),
                Size = new Size(200, 40),
                Font = new Font(uiFont, FontStyle.Bold),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat
            };
            btnShowGraph.Click += BtnShowGraph_Click;
            this.Controls.Add(btnShowGraph);

            // LVMファイル確認ボタン
            btnPreviewLVM = new Button
            {
                Text = "LVMファイルプレビュー",
                Location = new Point(100, 470),
                Size = new Size(180, 35),
                Font = uiFont,
                BackColor = Color.LightYellow,
                FlatStyle = FlatStyle.Flat
            };
            btnPreviewLVM.Click += BtnPreviewLVM_Click;
            this.Controls.Add(btnPreviewLVM);

            // LVM構造解析ボタン（デバッグ用）
            btnDebugLVM = new Button
            {
                Text = "LVM構造解析",
                Location = new Point(300, 470),
                Size = new Size(150, 35),
                Font = uiFont,
                BackColor = Color.LightCyan,
                FlatStyle = FlatStyle.Flat
            };
            btnDebugLVM.Click += BtnDebugLVM_Click;
            this.Controls.Add(btnDebugLVM);

            // メモリ使用量表示ボタン
            Button btnMemory = new Button
            {
                Text = "メモリ使用量",
                Location = new Point(470, 470),
                Size = new Size(120, 35),
                Font = uiFont,
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnMemory.Click += delegate
            {
                MessageBox.Show(UnifiedLvmProcessor.GetMemoryUsage(), "メモリ使用量",
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            this.Controls.Add(btnMemory);

            openFileDialog = new OpenFileDialog
            {
                Filter = "LVMファイル|*.lvm",
                Title = "LVMファイルを選択",
                Multiselect = false
            };
        }

        private void InitializeProgressControls()
        {
            // プログレスラベルの追加
            lblProgress = new System.Windows.Forms.Label
            {
                Location = new Point(20, 455),
                Size = new Size(650, 20),
                Text = "",
                Font = new Font("Meiryo UI", 9),
                Visible = false
            };
            this.Controls.Add(lblProgress);

            // プログレスバーの追加
            progressBar = new ProgressBar
            {
                Location = new Point(20, 455),
                Size = new Size(650, 15),
                Visible = false,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.Controls.Add(progressBar);
        }

        private string SelectLvmFile(System.Windows.Forms.Label targetLabel)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string path = openFileDialog.FileName;
                targetLabel.Text = Path.GetFileName(path);
                return path;
            }
            return string.Empty;
        }

        private void BtnShowGraph_Click(object sender, EventArgs e)
        {
            if (isProcessing)
            {
                MessageBox.Show("処理中", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(beforeALvmPath) || string.IsNullOrEmpty(beforeBLvmPath) ||
                    string.IsNullOrEmpty(afterALvmPath) || string.IsNullOrEmpty(afterBLvmPath))
                {
                    MessageBox.Show("全てのLVMファイルを選択してください", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                isProcessing = true;
                btnShowGraph.Enabled = false;
                progressBar.Visible = true;
                lblProgress.Visible = true;

                lblResult.Text = "処理開始中...\n" + UnifiedLvmProcessor.GetMemoryUsage();

                ProcessFilesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("エラーが発生しました:\n{0}", ex.Message),
                    "解析エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                ResetProcessingState();
            }
        }

        /// <summary>
        /// 単一のLVMファイルを非同期で処理する簡易ヘルパーメソッド。
        /// PedalファイルとCOPファイルで処理を分岐し、進捗更新と完了時コールバックを設定する。
        /// </summary>
        /// <param name="filePath">対象LVMファイルのパス</param>
        /// <param name="fileType">ファイルタイプ (例: "Before Pedal", "After COP")</param>
        /// <param name="baseProgress">全体進捗バーの基準値（0/25/50/75）</param>
        /// <param name="onComplete">処理完了時に呼ばれるコールバック (結果, エラー)</param>
        // private void ProcessFileSimple(string filePath, string fileType, int baseProgress,
        //                              Action<object, Exception> onComplete)
        // {
        //     // Pedalファイルの場合
        //     // → Pedal処理中の進捗を、UIのプログレスバーに反映させる
        //     if (fileType.Contains("Pedal"))
        //     {
        //         UnifiedLvmProcessor.ProgressUpdateDelegate progressDelegate =
        //             delegate (int progress, string message)
        //             {
        //                 // baseProgressを基準に、Pedal処理の進捗(0-25%)を4分割して反映
        //                 UpdateProgress(baseProgress + (progress / 4), fileType + ": " + message);
        //             };

        //         // 完了時コールバックデリゲートの定義
        //         // → 処理結果(result)と例外(error)を上位(onComplete)へ渡す
        //         UnifiedLvmProcessor.PedalProcessCompleteDelegate completeDelegate =
        //             (result, error) => onComplete(result, error);

        //         // 非同期でPedal抽出処理を実行
        //         UnifiedLvmProcessor.ExtractPedalWithTimeAsync(filePath, progressDelegate, completeDelegate);
        //     }
        //     else // COPファイルの場合
        //     {
        //         // 進捗更新デリゲートの定義
        //         // → COP処理中の進捗を、UIのプログレスバーに反映させる
        //         UnifiedLvmProcessor.ProgressUpdateDelegate progressDelegate =
        //             delegate (int progress, string message)
        //             {
        //                 UpdateProgress(baseProgress + (progress / 4), fileType + ": " + message);
        //             };

        //         // 完了時コールバックデリゲートの定義
        //         // → 処理結果(result)と例外(error)を上位(onComplete)へ渡す
        //         UnifiedLvmProcessor.CopProcessCompleteDelegate completeDelegate =
        //             (result, error) => onComplete(result, error);

        //         // 非同期でCOP計算処理を実行
        //         UnifiedLvmProcessor.ReadLvmAndComputeCopAsync(filePath, progressDelegate, completeDelegate);
        //     }
        // }
        private void ProcessFileSimple(string filePath, string fileType, int baseProgress,
                               Action<object, Exception> onComplete)
        {
            if (fileType.Contains("Pedal"))
            {
                try
                {
                    var result = CsvProcessor.ReadPedalCsv(filePath);
                    onComplete(result, null);
                }
                catch (Exception ex)
                {
                    onComplete(null, ex);
                }
            }
            else // COPは従来通りLVM処理
            {
                UnifiedLvmProcessor.ProgressUpdateDelegate progressDelegate =
                    delegate (int progress, string message)
                    {
                        UpdateProgress(baseProgress + (progress / 4), fileType + ": " + message);
                    };

                UnifiedLvmProcessor.CopProcessCompleteDelegate completeDelegate =
                    (result, error) => onComplete(result, error);

                UnifiedLvmProcessor.ReadLvmAndComputeCopAsync(filePath, progressDelegate, completeDelegate);
            }
        }


        private void ProcessFilesAsync()
        {
            var results = new Dictionary<string, object>();
            int completedFiles = 0;

            Action<string, object, Exception> handleCompletion = (key, data, error) =>
            {
                if (error != null)
                {

                    ShowError(string.Format("{0}処理エラー", key), error);
                    return;
                }

                lock (results)
                {
                    results[key] = data;
                    completedFiles++;

                    if (completedFiles == 4) ProcessCompleted(results);
                }
            };

            // 4つのファイルを並列処理
            ProcessFileSimple(beforeALvmPath, "Before Pedal", 0, (data, error) =>
                handleCompletion("beforePedal", data, error));

            ProcessFileSimple(beforeBLvmPath, "Before COP", 25, (data, error) =>
                handleCompletion("beforeCop", data, error));

            ProcessFileSimple(afterALvmPath, "After Pedal", 50, (data, error) =>
                handleCompletion("afterPedal", data, error));

            ProcessFileSimple(afterBLvmPath, "After COP", 75, (data, error) =>
                handleCompletion("afterCop", data, error));
        }

        private void UpdateProgress(int percentage, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, string>(UpdateProgress), percentage, message);
                return;
            }

            if (progressBar != null)
            {
                progressBar.Value = Math.Min(Math.Max(percentage, 0), 100);
            }

            if (lblProgress != null)
            {
                lblProgress.Text = message + " - " + UnifiedLvmProcessor.GetMemoryUsage();
            }
        }

        private void ShowError(string title, Exception error)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, Exception>(ShowError), title, error);
                return;
            }


            MessageBox.Show(
                string.Format("{0}:\n{1}", title, error.Message),
                "エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            ResetProcessingState();
        }

        /// <summary>
        /// 全LVMファイル処理完了時に呼び出される処理
        /// ・解析結果のデータを取得
        /// ・標準偏差を計算
        /// ・結果ラベル更新およびグラフ表示
        /// ・進捗表示の終了処理
        /// </summary>
        /// <param name="results">4ファイル分の解析結果（Pedal/COP Before/After）</param>
        private void ProcessCompleted(Dictionary<string, object> results)
        {
            // --- UIスレッド外から呼ばれた場合はInvokeしてUIスレッドで実行 ---
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<Dictionary<string, object>>(ProcessCompleted), results);
                return;
            }

            try
            {
                // データ取得（UnifiedLvmProcessor使用）
                // var beforePedalData = results["beforePedal"] as List<UnifiedLvmProcessor.PedalTimeData>; //[ {Time=0.00, PedalValue=0.12}, {Time=0.01, PedalValue=0.15}, ... ]
                var beforeCopData = results["beforeCop"] as List<UnifiedLvmProcessor.CopResult>;
                // var afterPedalData = results["afterPedal"] as List<UnifiedLvmProcessor.PedalTimeData>;
                var afterCopData = results["afterCop"] as List<UnifiedLvmProcessor.CopResult>;
                var beforePedalData = results["beforePedal"] as List<PedalTimeData>;
                var afterPedalData  = results["afterPedal"]  as List<PedalTimeData>;



                // データが1つでもnullならエラー表示して中断
                if (beforePedalData == null || beforeCopData == null ||
                    afterPedalData == null || afterCopData == null)
                {
                    ShowError("データ取得エラー", new Exception("一部のデータが正しく取得できませんでした"));
                    return;
                }

                //  指標計算用に値リスト抽出
                var beforePedalValues = beforePedalData.ConvertAll(x => x.PedalValue);
                var afterPedalValues = afterPedalData.ConvertAll(x => x.PedalValue);

                // COP Px, Py抽出
                var beforeCopPxValues = new List<double>();
                var beforeCopPyValues = new List<double>();
                foreach (var cop in beforeCopData)
                {
                    beforeCopPxValues.Add(cop.Px);
                    beforeCopPyValues.Add(cop.Py);
                }
                var afterCopPxValues = new List<double>();
                var afterCopPyValues = new List<double>();
                foreach (var cop in afterCopData)
                {
                    afterCopPxValues.Add(cop.Px);
                    afterCopPyValues.Add(cop.Py);
                }


                beforeCopPyValues = CalcMethod.SmoothExcludeLargeJumps(beforeCopPyValues, 100, 0.05);  // 窓幅5サンプル(0.05秒)
                afterCopPyValues = CalcMethod.SmoothExcludeLargeJumps(afterCopPyValues, 100, 0.05);
                beforeCopPxValues = CalcMethod.SmoothExcludeLargeJumps(beforeCopPxValues, 100, 0.05);  // 窓幅5サンプル(0.05秒)
                afterCopPxValues = CalcMethod.SmoothExcludeLargeJumps(afterCopPxValues, 100, 0.05);

                //ラベル
                var beforeLabels = CalcMethod.LabelPedalPhases(beforePedalValues, 0.01);
                var afterLabels = CalcMethod.LabelPedalPhases(afterPedalValues, 0.01);
                // ラベル集計
                int risingCount = beforeLabels.Count(l => l == "Rising");
                int fallingCount = beforeLabels.Count(l => l == "Falling");
                int flatCount = beforeLabels.Count(l => l == "Flat");

                // スタート検出
                int beforeStartIndex = CalcMethod.FindPedalStartIndex(beforePedalValues);
                int afterStartIndex = CalcMethod.FindPedalStartIndex(afterPedalValues);
                // スタートフラグ（bool）を設定
                bool beforeStarted = beforeStartIndex >= -0.2;
                bool afterStarted = afterStartIndex >= -0.2;
                // スタート時刻を計算（サンプリング周期: 100Hz → 0.01秒）
                double beforeStartTime = beforeStartIndex >= -0.2 ? beforeStartIndex * 0.01 : -1;
                double afterStartTime = afterStartIndex >= -0.2 ? afterStartIndex * 0.01 : -1;

                // --- ここでMainFormで一度だけ速度計算を実行 ---


                //int fs = 100;
                //double cutoffHz = 3.0;  // まず 6Hz あたり
                //int order = 2;          // ※今の実装は biquad 1段相当。厳密な多段は後述

                //var beforePedalSmoothed = FilterUtils.ButterworthLowpassDouble(beforePedalValues, fs, cutoffHz, order);
                //var afterPedalSmoothed = FilterUtils.ButterworthLowpassDouble(afterPedalValues, fs, cutoffHz, order);

                //var beforePedalVelocity = CalcMethod.ComputeVelocity2(beforePedalSmoothed, 0.01, 2);
                //var afterPedalVelocity = CalcMethod.ComputeVelocity2(afterPedalSmoothed, 0.01, 2);

        



                // 100Hz, dt=0.01s, 窓50→約0.5秒の移動平均（ほぼ無位相：中央平均）
                var beforePedalSmoothed = CalcMethod.Smooth(beforePedalValues, 50);
                var afterPedalSmoothed = CalcMethod.Smooth(afterPedalValues, 50);

                // 速度は“その同じ平滑系列”から中央差分で
                var beforePedalVelocity = CalcMethod.ComputeVelocity2(beforePedalSmoothed, 0.01, 2);
                var afterPedalVelocity = CalcMethod.ComputeVelocity2(afterPedalSmoothed, 0.01, 2);



                // 追加：加速度 → ジャーク（どちらも中央差分）
                var beforeAccel = CalcMethod.ComputeVelocity2(beforePedalVelocity, 0.01, 2);
                var afterAccel = CalcMethod.ComputeVelocity2(afterPedalVelocity, 0.01, 2);
                var beforeJerk = CalcMethod.ComputeVelocity2(beforeAccel, 0.01, 2);
                var afterJerk = CalcMethod.ComputeVelocity2(afterAccel, 0.01, 2);



                // --- 指標計算 ===================================================
                //COP
                double beforeCopStd = 0;
                double afterCopStd = 0;
                double beforeCopRange = 0;
                double afterCopRange = 0;
                if (beforeStartIndex >= 0 && beforeStartIndex < beforeCopPxValues.Count)
                {
                    var filteredBeforeCopPx = beforeCopPxValues.Skip(beforeStartIndex).ToList();

                    // 最後の1秒（100サンプル）を削除
                    if (filteredBeforeCopPx.Count > 100)
                        filteredBeforeCopPx = filteredBeforeCopPx.Take(filteredBeforeCopPx.Count - 300).ToList();
                    beforeCopStd = CalcStandardDeviation(filteredBeforeCopPx); // 標準偏差
                    //beforeCopRange = CalcMaxMinRange(filteredBeforeCopPx); //最大と最小の差
                    //beforeCopRange = MetricsCalculator.CalcMeanAmpRMS(filteredBeforeCopPx);
                    beforeCopRange = MetricsCalculator.CalcMeanAmp(filteredBeforeCopPx, 1600);


                }

                if (afterStartIndex >= 0 && afterStartIndex < afterCopPxValues.Count)
                {
                    var filteredAfterCopPx = afterCopPxValues.Skip(afterStartIndex).ToList();
                    // 最後の1秒（100サンプル）を削除
                    if (filteredAfterCopPx.Count > 100)
                        filteredAfterCopPx = filteredAfterCopPx.Take(filteredAfterCopPx.Count - 100).ToList();
                    afterCopStd = CalcStandardDeviation(filteredAfterCopPx); //標準偏差
                    //afterCopRange = CalcMaxMinRange(filteredAfterCopPx); //最大最小の差
                    //afterCopRange = MetricsCalculator.CalcMeanAmpRMS(filteredAfterCopPx);
                    afterCopRange = MetricsCalculator.CalcMeanAmp(filteredAfterCopPx, 1600);

                }


                // ペダル波形の区間分割を実行 (動的傾きベース分割使用 - スムージング済みデータを使用)
                //var beforeSegments = PedalProcessor.GenerateDynamicSegments(beforePedalSmoothed, beforeStartTime);
                //var afterSegments = PedalProcessor.GenerateDynamicSegments(afterPedalSmoothed, afterStartTime);


                //                List<double> pedalValues,
                //List< double > pedalVelocity,
                //            double risingThreshold,
                //            double fallingThreshold,
                //            double steadyThreshold,
                //            double steadyStdThreshold,
                //            double minDurRisingSec,
                //            double minDurSteadySec,
                //            double minDurFallingSec,
                //            double sampleRate,
                            //int windowSize)
                var beforeSegments = PedalProcessor.SegmentPedalByVelocityV3(
                    beforePedalValues,
                    beforePedalVelocity,
                    0.08,
                    -0.08,
                    0.03,
                    0.1,
                    0.8,
                    0.8,
                    0.8,
                    100.0,
                    21
                );
                var afterSegments = PedalProcessor.SegmentPedalByVelocityV3(
                    afterPedalValues,
                    afterPedalVelocity,
                    0.08,
                    -0.08,
                    0.03,
                    0.1,
                    0.8,
                    0.8,
                    0.8,
                    100.0,
                    21
                );
                beforeSegments = PedalProcessor.CorrectToSteadyRisingPattern(beforeSegments, 100.0);
                afterSegments = PedalProcessor.CorrectToSteadyRisingPattern(afterSegments, 100.0);


                DebugTool.InfoLog("=== Before Segments ===");
                foreach (var seg in beforeSegments)
                {
                    DebugTool.InfoLog(
                        string.Format("Segment: Type={0}, Start={1}, End={2}, Slope={3:F4}",
                            seg.Type, seg.StartIndex, seg.EndIndex, seg.Slope));
                }


                // 各上昇区間を別々に抽出
                var beforeRisingSegments = ExtractSegmentValuesByEachSegment(beforePedalVelocity, beforeSegments, "Rising"); //List<List<double>>
                var beforeFallingSegments = ExtractSegmentValuesByEachSegment(beforePedalVelocity, beforeSegments, "Falling");
                var afterRisingSegments = ExtractSegmentValuesByEachSegment(afterPedalVelocity, afterSegments, "Rising");
                var afterFallingSegments = ExtractSegmentValuesByEachSegment(afterPedalVelocity, afterSegments, "Falling");


                // 共通処理でまとめてログ出力
                var BeforeRisingSurprise = ProcessSegments(beforeRisingSegments, "Before Rising");
                var BeforeFallingSurprise = ProcessSegments(beforeFallingSegments, "Before Falling");
                var AfterRisingSurprise = ProcessSegments(afterRisingSegments, "After Rising");
                var AfterFallingSurprise = ProcessSegments(afterFallingSegments, "After Falling");


                // 上昇区間(Rising)だけのペダル値を抽出して分析
                var beforeRisingValues = ExtractSegmentValues(beforePedalVelocity, beforeSegments, "Rising");
                var afterRisingValues = ExtractSegmentValues(afterPedalVelocity, afterSegments, "Rising");

                DebugTool.InfoLog("=== Before Rising Values ===");
                DebugTool.InfoLog(DebugTool.ArrayToString(beforeRisingValues.ToArray(), ", ", 20));

                var beforePedalStd = CalcStandardDeviation(beforeRisingValues);
                var afterPedalStd = CalcStandardDeviation(afterRisingValues);

                //滑らかさ
                var beforeSmoothness = 1.0 / (beforePedalStd + 0.0001);
                var afterSmoothness = 1.0 / (afterPedalStd + 0.0001);


                // === 3指標の生値を計算 ===
                var dt = 0.01;
                double steadyThreshold = 0.03; // Segmentationで使ってるのに合わせる

                var beforeRaw = ComputeRawMetrics(
                    beforePedalValues, beforePedalVelocity, beforeJerk, beforeSegments, dt, steadyThreshold);
                var afterRaw = ComputeRawMetrics(
                    afterPedalValues, afterPedalVelocity, afterJerk, afterSegments, dt, steadyThreshold);

                // --- min-max 正規化（小さいほど良い => 1に近づける） ---
                (double sJ_before, double sJ_after) = NormLowerBetter(beforeRaw.NJ, afterRaw.NJ);
                (double sS_before, double sS_after) = NormLowerBetter(beforeRaw.StallRatio, afterRaw.StallRatio);
                (double sB_before, double sB_after) = NormLowerBetter(beforeRaw.BoundaryRmsV, afterRaw.BoundaryRmsV);

                // 重み
                const double wJerk = 0.5, wStall = 0.3, wBound = 0.2;

                double beforeTotal = 100.0 * (wJerk * sJ_before + wStall * sS_before + wBound * sB_before);
                double afterTotal = 100.0 * (wJerk * sJ_after + wStall * sS_after + wBound * sB_after);

 



                lblResult.Text = string.Format(
                    "\npedalサプライズ: BeforeRising={0:F4}, BeforeFalling={1:F4},AfterRising={2:F4},AfterFalling={3:F4}",
                    BeforeRisingSurprise, BeforeFallingSurprise,AfterRisingSurprise,AfterFallingSurprise
                );



                // 表示
                lblResult.Text += string.Format(
                    "\n\n[正規化スコア]\n" +
                    "Before: Smooth={0:F2}, Stall={1:F2}, Boundary={2:F2} => Total={3:F1}\n" +
                    "After : Smooth={4:F2}, Stall={5:F2}, Boundary={6:F2} => Total={7:F1}",
                    sJ_before, sS_before, sB_before, beforeTotal,
                    sJ_after, sS_after, sB_after, afterTotal
                );
                // グラフ表示 ============================================================
                // グラフ表示用フォームが未生成または破棄されていれば再生成
                if (stdDevPlotForm == null || stdDevPlotForm.IsDisposed)
                {
                    stdDevPlotForm = new Form_StdDevPlot();
                    stdDevPlotForm.FormClosed += delegate { stdDevPlotForm = null; };
                }

                // PedalとCOPの時系列データを速度込みでグラフに渡す（修正版）
                // 元データとスムージング済みデータの両方を渡す
                stdDevPlotForm.SetTimeSeriesDataWithVelocity("Before", beforePedalValues, beforePedalSmoothed, beforePedalVelocity, beforeCopPxValues, beforeCopPyValues, beforeLabels, beforeStartIndex);
                stdDevPlotForm.SetTimeSeriesDataWithVelocity("After", afterPedalValues, afterPedalSmoothed, afterPedalVelocity, afterCopPxValues, afterCopPyValues, afterLabels, afterStartIndex);

                // グラフに区間データを渡す（追加）
                stdDevPlotForm.SetSegmentData("Before", beforeSegments);
                stdDevPlotForm.SetSegmentData("After", afterSegments);

                // セット（SetJerkData を実装した場合のみ）
                stdDevPlotForm.SetJerkData("Before", beforeJerk);
                stdDevPlotForm.SetJerkData("After", afterJerk);

                // 指標ペアを散布図に渡す
                var dataPoints = new Dictionary<string, StdDevPair>
                {

                    //ver3
                    //{ "Before", new StdDevPair(beforeCopRange,BeforeRisingSurprise) },//A=COP(力X軸),B=pedal(滑らか運転Y軸)
                    //{ "After", new StdDevPair(afterCopRange,AfterRisingSurprise) }
                    
                    { "Before", new StdDevPair(beforeCopRange,BeforeFallingSurprise) },//A=COP(力X軸),B=pedal(滑らか運転Y軸)
                    { "After", new StdDevPair(afterCopRange,AfterFallingSurprise) }

                };

                stdDevPlotForm.PlotDataPoints(dataPoints);// グラフ描画
                stdDevPlotForm.Show();// フォーム表示
                stdDevPlotForm.BringToFront();// 最前面に表示

                UpdateProgress(100, "処理完了");



                // デバッグ出力
                DebugTool.InfoLog("=== Debug Output Start ===");
                DebugTool.InfoLog("Before Segments:");
                foreach (var seg in beforeSegments)
                {
                    DebugTool.InfoLog(
                        string.Format("Type={0}, Start={1}, End={2}, Slope={3:F4}",
                            seg.Type, seg.StartIndex, seg.EndIndex, seg.Slope));
                }

                //DebugTool.InfoLog("Before Rising Values: " + DebugTool.ArrayToString(beforeRisingValues.ToArray(), ", ", 30));
                //DebugTool.InfoLog("After Rising Values: " + DebugTool.ArrayToString(afterRisingValues.ToArray(), ", ", 30));
                // Before Rising Values 全要素出力
                DebugTool.InfoLog("=== Before Rising Values ===");
                DebugTool.InfoLog(DebugTool.ArrayToString(beforeRisingValues.ToArray(), ", ", beforeRisingValues.Count));

                // After Rising Values 全要素出力
                DebugTool.InfoLog("=== After Rising Values ===");
                DebugTool.InfoLog(DebugTool.ArrayToString(afterRisingValues.ToArray(), ", ", afterRisingValues.Count));

                DebugTool.InfoLog(string.Format("beforePedalStd = {0:F6}", beforePedalStd));
                DebugTool.InfoLog(string.Format("afterPedalStd  = {0:F6}", afterPedalStd));


                // 保存ダイアログを出して保存
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "CSVファイル|*.csv",
                    Title = "結果CSVを保存",
                    FileName = "pedal_with_segments.csv"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    // Before, After 両方保存したい場合
                    string beforePath = Path.Combine(
                        Path.GetDirectoryName(saveDialog.FileName),
                        "before_pedal_segments.csv");
                    string afterPath = Path.Combine(
                        Path.GetDirectoryName(saveDialog.FileName),
                        "after_pedal_segments.csv");

                    ExportPedalWithSegments(beforePath, beforePedalValues, beforeSegments, 100.0);
                    ExportPedalWithSegments(afterPath, afterPedalValues, afterSegments, 100.0);

                    MessageBox.Show("CSV保存が完了しました", "保存完了",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }




                DebugTool.InfoLog("=== Debug Output End ===");

            }
            catch (Exception ex)
            {
                ShowError("グラフ表示エラー", ex);
            }
            finally
            {
                // 進捗表示のリセット
                // 2秒後に状態リセット（プログレスバー非表示など）
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 2000;
                timer.Tick += delegate
                {
                    timer.Stop();
                    timer.Dispose();
                    ResetProcessingState();
                };
                timer.Start();
            }
        }

        private void ResetProcessingState()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ResetProcessingState));
                return;
            }

            isProcessing = false;
            btnShowGraph.Enabled = true;

            if (progressBar != null) progressBar.Visible = false;
            if (lblProgress != null) lblProgress.Visible = false;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        //標準偏差
        private double CalcStandardDeviation(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0.0;

            double avg = values.Average();
            double sumSq = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSq / values.Count);
        }


        private void BtnPreviewLVM_Click(object sender, EventArgs e)
        {
            OpenFileDialog lvmDialog = new OpenFileDialog
            {
                Filter = "LVMファイル|*.lvm",
                Title = "プレビューするLVMファイルを選択",
                Multiselect = false
            };

            if (lvmDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = lvmDialog.FileName;

                List<string> previewLines = new List<string>();
                using (StreamReader reader = new StreamReader(filePath))
                {
                    int lineCount = 0;
                    while (!reader.EndOfStream && lineCount < 100)
                    {
                        previewLines.Add(reader.ReadLine());
                        lineCount++;
                    }
                }

                Form viewerForm = new Form();
                viewerForm.Text = "LVMファイルプレビュー: " + Path.GetFileName(filePath);
                viewerForm.Size = new Size(800, 600);

                TextBox txtContent = new TextBox();
                txtContent.Multiline = true;
                txtContent.ReadOnly = true;
                txtContent.ScrollBars = ScrollBars.Both;
                txtContent.Dock = DockStyle.Fill;
                txtContent.Font = new Font("Consolas", 10);
                txtContent.Text = string.Join(Environment.NewLine, previewLines.ToArray());

                viewerForm.Controls.Add(txtContent);
                viewerForm.ShowDialog();
            }
        }

        private void BtnDebugLVM_Click(object sender, EventArgs e)
        {
            OpenFileDialog lvmDialog = new OpenFileDialog
            {
                Filter = "LVMファイル|*.lvm",
                Title = "構造解析するLVMファイルを選択",
                Multiselect = false
            };

            if (lvmDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = lvmDialog.FileName;

                try
                {
                    string analysisResult = UnifiedLvmProcessor.AnalyzeLvmStructure(filePath);

                    Form debugForm = new Form();
                    debugForm.Text = "LVM構造解析結果: " + Path.GetFileName(filePath);
                    debugForm.Size = new Size(900, 700);

                    TextBox txtDebug = new TextBox();
                    txtDebug.Multiline = true;
                    txtDebug.ReadOnly = true;
                    txtDebug.ScrollBars = ScrollBars.Both;
                    txtDebug.Dock = DockStyle.Fill;
                    txtDebug.Font = new Font("Consolas", 10);
                    txtDebug.Text = analysisResult;

                    debugForm.Controls.Add(txtDebug);
                    debugForm.ShowDialog();
                }
                catch (Exception ex)
                {

                    MessageBox.Show(
                        string.Format("構造解析エラー:\n{0}", ex.Message),
                        "エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (stdDevPlotForm != null) stdDevPlotForm.Close();
        }

        /// <summary>
        /// 指定したタイプのセグメントの値を抽出
        /// </summary>
        private List<double> ExtractSegmentValues(List<double> values, List<PedalSegment> segments, string segmentType)
        {
            var result = new List<double>();

            foreach (var segment in segments)
            {
                if (segment.Type == segmentType)
                {
                    int start = Math.Max(0, Math.Min(segment.StartIndex, values.Count - 1));
                    int end = Math.Max(0, Math.Min(segment.EndIndex, values.Count - 1));

                    for (int i = start; i <= end; i++)
                    {
                        result.Add(values[i]);
                    }
                }
            }

            return result;
        }
        /// <summary>
        /// 指定したタイプのセグメントに該当する各区間の値を個別に抽出
        /// 例: "Rising" を指定すると、Risingセグメントごとに List<double> を返す
        /// </summary>
        private List<List<double>> ExtractSegmentValuesByEachSegment(List<double> values, List<PedalSegment> segments, string segmentType)
        {
            var result = new List<List<double>>();

            foreach (var segment in segments)
            {
                if (segment.Type == segmentType)
                {
                    int start = Math.Max(0, Math.Min(segment.StartIndex, values.Count - 1));
                    int end = Math.Max(0, Math.Min(segment.EndIndex, values.Count - 1));

                    var segmentValues = new List<double>();
                    for (int i = start; i <= end; i++)
                    {
                        segmentValues.Add(values[i]);
                    }

                    result.Add(segmentValues);
                }
            }

            return result;
        }


        /// <summary>
        /// セグメント群の logSurprise を計算し、各セグメント平均と全体平均をログ出力する
        /// </summary>
        /// <param name="segments">セグメント群</param>
        /// <param name="label">ラベル (例: "Before Rising")</param>
        /// <returns>全体平均 logSurprise</returns>
        private double ProcessSegments(List<List<double>> segments, string label)
        {
            List<double> segmentMeans = new List<double>();

            for (int i = 0; i < segments.Count; i++)
            {
                string msg = string.Format("{0} {1}: ", label, i + 1)
                            + DebugTool.ArrayToString(segments[i].ToArray(), ", ", 30);
                DebugTool.InfoLog(msg);

                List<double> segment = segments[i];

                // オフセットを加えた配列を作成 (+0.5ならここを調整)
                double[] offsetSegment = segment.Select(x => x + 0.5).ToArray();

                // サプライズ計算
                double[] surprise = SurpriseCalculator.CalculateOperationSurprise(offsetSegment);

                // log(surprise + 1)
                double[] logSurprise = surprise.Select(x => Math.Log(x + 1)).ToArray();

                // セグメント平均
                double meanLogSurprise = logSurprise.Average();


                // セグメントの時間長 [秒]
                double durationSec = segment.Count / 100;
                // 単位時間あたりの平均
                double meanLogSurprise2 = logSurprise.Sum() / durationSec;

                segmentMeans.Add(meanLogSurprise);


                // ログ出力
                DebugTool.InfoLog("Surprise values:");
                for (int j = 0; j < logSurprise.Length; j++)
                {
                    DebugTool.InfoLog(string.Format("  [{0}] {1:F6}", j, logSurprise[j]));
                }
                DebugTool.InfoLog(string.Format("  → 平均 logSurprise: {0:F6}", meanLogSurprise));
            }

            // 全体平均
            double overallMean = (segmentMeans.Count > 0) ? segmentMeans.Average() : 0.0;
            DebugTool.InfoLog(string.Format("=== 全 {0} セグメントの平均 logSurprise: {1:F6} ===", label, overallMean));

            return overallMean;
        }


        private string SelectCsvFile(System.Windows.Forms.Label targetLabel)
        {
            using (OpenFileDialog csvDialog = new OpenFileDialog
            {
                Filter = "CSVファイル|*.csv",
                Title = "CSVファイルを選択",
                Multiselect = false
            })
            {
                if (csvDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = csvDialog.FileName;
                    targetLabel.Text = Path.GetFileName(path);
                    return path;
                }
            }
            return string.Empty;
        }



        private void ExportPedalWithSegments(
    string filePath,
    List<double> pedalValues,
    List<PedalSegment> segments,
    double sampleRate)
        {
            using (var writer = new StreamWriter(filePath))
            {
                // ヘッダー
                writer.WriteLine("time,pedal,segment_label,segment_id");

                int risingSegmentNo = 0;
                int fallingSegmentNo = 0;
                int steadySegmentNo = 0;

                int currentLabel = int.MinValue;  // 前サンプルの状態
                int currentId = 0;                // 前サンプルのセグメント番号

                for (int i = 0; i < pedalValues.Count; i++)
                {
                    double time = i / sampleRate;
                    double pedal = pedalValues[i];

                    // デフォルトは steady
                    int labelValue = 0;

                    foreach (var seg in segments)
                    {
                        if (i >= seg.StartIndex && i <= seg.EndIndex)
                        {
                            if (seg.Type.Equals("Rising", StringComparison.OrdinalIgnoreCase))
                                labelValue = 1;
                            else if (seg.Type.Equals("Falling", StringComparison.OrdinalIgnoreCase))
                                labelValue = -1;
                            else
                                labelValue = 0;
                            break;
                        }
                    }

                    // 新しいセグメントに入ったときはIDを更新
                    if (labelValue != currentLabel)
                    {
                        if (labelValue == 1)
                            risingSegmentNo++;
                        else if (labelValue == -1)
                            fallingSegmentNo++;
                        else
                            steadySegmentNo++;

                        if (labelValue == 1)
                            currentId = risingSegmentNo;
                        else if (labelValue == -1)
                            currentId = fallingSegmentNo;
                        else
                            currentId = steadySegmentNo;

                        currentLabel = labelValue;
                    }

                    writer.WriteLine($"{time:F2},{pedal:F6},{labelValue},{currentId}");
                }
            }
        }

        private (double NJ, double StallRatio, double BoundaryRmsV) ComputeRawMetrics(
    List<double> pedal, List<double> vel, List<double> jerk,
    List<PedalSegment> segments, double dt, double steadyThreshold)
        {
            // 活動区間（Rising/Falling）のインデックス集合
            var activeIdx = new List<int>();
            int minStart = int.MaxValue, maxEnd = -1;

            foreach (var s in segments)
            {
                if (!s.Type.Equals("Steady", StringComparison.OrdinalIgnoreCase))
                {
                    int a = Math.Max(0, s.StartIndex);
                    int b = Math.Min(pedal.Count - 1, s.EndIndex);
                    for (int i = a; i <= b; i++) activeIdx.Add(i);
                    if (a < minStart) minStart = a;
                    if (b > maxEnd) maxEnd = b;
                }
            }
            if (activeIdx.Count == 0) return (0, 0, 0); // ガード

            // T と Δ（活動区間）
            double T = activeIdx.Count * dt;
            double minP = activeIdx.Min(i => pedal[i]);
            double maxP = activeIdx.Max(i => pedal[i]);
            double dAmp = Math.Max(1e-6, maxP - minP);

            // 正規化ジャーク NJ = (T^5 / Δ^2) * ∫ j^2 dt
            double sumJ2dt = activeIdx.Sum(i => jerk[Math.Min(i, jerk.Count - 1)] * jerk[Math.Min(i, jerk.Count - 1)]) * dt;
            double NJ = Math.Pow(T, 5) / (dAmp * dAmp) * sumJ2dt;

            // 停滞割合（活動中に |v| < steadyThreshold）
            int stallCnt = activeIdx.Count(i => Math.Abs(vel[i]) < steadyThreshold);
            double stallRatio = (double)stallCnt / activeIdx.Count;

            // 境界の静止：活動の前後の Steady 区間での速度RMS
            var boundaryIdx = new List<int>();
            foreach (var s in segments)
            {
                if (s.Type.Equals("Steady", StringComparison.OrdinalIgnoreCase))
                {
                    if (s.EndIndex < minStart || s.StartIndex > maxEnd)
                    {
                        int a = Math.Max(0, s.StartIndex);
                        int b = Math.Min(pedal.Count - 1, s.EndIndex);
                        for (int i = a; i <= b; i++) boundaryIdx.Add(i);
                    }
                }
            }
            // もし境界Steadyが見つからなければ、活動前後±0.3sを代替窓に
            if (boundaryIdx.Count == 0)
            {
                int w = (int)Math.Round(0.3 / dt);
                for (int i = Math.Max(0, minStart - w); i < minStart; i++) boundaryIdx.Add(i);
                for (int i = maxEnd + 1; i <= Math.Min(pedal.Count - 1, maxEnd + w); i++) boundaryIdx.Add(i);
            }
            double boundaryRmsV = (boundaryIdx.Count == 0)
                ? 0.0
                : Math.Sqrt(boundaryIdx.Average(i => vel[i] * vel[i]));

            return (NJ, stallRatio, boundaryRmsV);
        }

        // 小さいほど良い値 x を 0–1（1=良い）へ：ペア（Before/After）でmin-max
        private (double, double) NormLowerBetter(double xBefore, double xAfter)
        {
            double xmin = Math.Min(xBefore, xAfter);
            double xmax = Math.Max(xBefore, xAfter);
            if (xmax - xmin < 1e-12) return (1.0, 1.0); // 同値なら満点

            double nb = 1.0 - (xBefore - xmin) / (xmax - xmin);
            double na = 1.0 - (xAfter - xmin) / (xmax - xmin);
            return (Clamp01(nb), Clamp01(na));
        }
        private double Clamp01(double x) => (x < 0) ? 0 : (x > 1 ? 1 : x);







    }
}