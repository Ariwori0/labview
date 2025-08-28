using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoreLinkSys1.Analysis
{
    public class CycleRange
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        public CycleRange(int start, int end)
        {
            StartIndex = start;
            EndIndex = end;
        }
    }

    public class EnsembleResult
    {
        public List<string> FinalLabels { get; set; }
        public List<double> Confidence { get; set; }

        public EnsembleResult()
        {
            FinalLabels = new List<string>();
            Confidence = new List<double>();
        }
    }


    public class RobustGaitCycleDetector
    {
        private int fs;
        private double minCycleDuration = 0.8;
        private double maxCycleDuration = 2.5;
        private double peakDistanceFactor = 0.35;

        public RobustGaitCycleDetector(int sampleRate)
        {
            this.fs = sampleRate;
        }

        public RobustGaitCycleDetector() : this(100) { }

        /// <summary>
        /// 適応ローパスフィルタ
        /// </summary>
        public float[] AdaptiveLowpassFilter(float[] data, double baseCutoffHz, int order)
        {
            // TODO: FFT で支配周波数を求めて cutoff を調整
            return FilterUtils.ButterworthLowpass(data, fs, baseCutoffHz, order);
        }


        /// <summary>
        /// ピーク検出
        /// </summary>
        public List<int> DetectPeaks(float[] data, double minProminence, int minDistanceSamples)
        {
            List<int> peaks = new List<int>();
            for (int i = 1; i < data.Length - 1; i++)
            {
                if (data[i] > data[i - 1] && data[i] > data[i + 1])
                {
                    if (peaks.Count == 0 || i - peaks[peaks.Count - 1] > minDistanceSamples)
                    {
                        if (data[i] > minProminence) peaks.Add(i);
                    }
                }
            }
            return peaks;
        }


        /// <summary>
        /// 谷検出 (負のピークを探す)
        /// </summary>
        public List<int> DetectValleys(float[] data, double minProminence, int minDistanceSamples)
        {
            List<int> valleys = new List<int>();
            for (int i = 1; i < data.Length - 1; i++)
            {
                if (data[i] < data[i - 1] && data[i] < data[i + 1])
                {
                    if (valleys.Count == 0 || i - valleys[valleys.Count - 1] > minDistanceSamples)
                    {
                        if (data[i] < -minProminence) valleys.Add(i);
                    }
                }
            }
            return valleys;
        }

        /// <summary>
        /// 歩行サイクル妥当性検証 (valley → valley 間の時間で判定)
        /// </summary>
        /// 
        public List<CycleRange> ValidateCycles(List<int> valleys, float[] timeArray)
        {
            List<CycleRange> validCycles = new List<CycleRange>();

            for (int i = 0; i < valleys.Count - 2; i += 2)
            {
                int startIdx = valleys[i];
                int endIdx = valleys[i + 2];

                double duration = timeArray[endIdx] - timeArray[startIdx];
                if (duration >= minCycleDuration && duration <= maxCycleDuration)
                {
                    validCycles.Add(new CycleRange(startIdx, endIdx));
                }
            }
            return validCycles;
        }

        // ---------------- ステップラベリング ----------------
        // public List<string> LabelByGyroZ(float[] gyroZ, List<int> peaks, int windowMs)
        // {
        //     List<string> labels = new List<string>();
        //     int halfWindow = (int)(windowMs / 1000.0 * fs);

        //     foreach (int idx in peaks)
        //     {
        //         int start = Math.Max(0, idx - halfWindow);
        //         int end = Math.Min(gyroZ.Length, idx + halfWindow);

        //         double meanVal = 0;
        //         for (int i = start; i < end; i++) meanVal += gyroZ[i];
        //         meanVal /= Math.Max(1, end - start);

        //         if (meanVal > 0) labels.Add("L");
        //         else if (meanVal < 0) labels.Add("R");
        //         else labels.Add("U");
        //     }
        //     return labels;
        // }

        // public List<string> LabelByGyroVector(float[] gx, float[] gy, float[] gz, List<int> peaks, int windowMs)
        // {
        //     List<string> labels = new List<string>();
        //     int halfWindow = (int)(windowMs / 1000.0 * fs);

        //     foreach (int idx in peaks)
        //     {
        //         int start = Math.Max(0, idx - halfWindow);
        //         int end = Math.Min(gz.Length, idx + halfWindow);

        //         double sumZ = 0, sumMag = 0;
        //         for (int i = start; i < end; i++)
        //         {
        //             double mag = Math.Sqrt(gx[i] * gx[i] + gy[i] * gy[i] + gz[i] * gz[i]);
        //             sumZ += Math.Abs(gz[i]);
        //             sumMag += mag;
        //         }

        //         double dominance = sumZ / (sumMag + 1e-6);
        //         double meanGz = 0;
        //         for (int i = start; i < end; i++) meanGz += gz[i];
        //         meanGz /= Math.Max(1, end - start);

        //         if (dominance > 0.3)
        //         {
        //             labels.Add(meanGz > 0 ? "L" : "R");
        //         }
        //         else labels.Add("U");
        //     }
        //     return labels;
        // }

        /// <summary>
        /// アンサンブル投票
        /// </summary>
        public List<string> EnsembleVoting(List<List<string>> methodResults)
        {
            int n = methodResults[0].Count;
            List<string> finalLabels = new List<string>();

            for (int i = 0; i < n; i++)
            {
                int votesL = 0, votesR = 0, votesU = 0;
                foreach (var method in methodResults)
                {
                    if (i < method.Count)
                    {
                        string label = method[i];
                        if (label == "L") votesL++;
                        else if (label == "R") votesR++;
                        else votesU++;
                    }
                }

                if (votesL > votesR && votesL > votesU) finalLabels.Add("L");
                else if (votesR > votesL && votesR > votesU) finalLabels.Add("R");
                else finalLabels.Add("U");
            }

            return finalLabels;
        }

        /// <summary>
        /// 交互パターン補正
        /// </summary>
        public List<string> EnforceAlternatingPattern(List<string> labels)
        {
            List<string> corrected = new List<string>(labels);
            if (corrected.Count == 0) return corrected;

            // 最初の有効ラベルを探す
            int firstValidIdx = -1;
            string firstLabel = "R";
            for (int i = 0; i < corrected.Count; i++)
            {
                if (corrected[i] == "L" || corrected[i] == "R")
                {
                    firstValidIdx = i;
                    firstLabel = corrected[i];
                    break;
                }
            }

            if (firstValidIdx == -1)
            {
                // 全部不明 → R-L-R-L で埋める
                corrected.Clear();
                for (int i = 0; i < labels.Count; i++)
                {
                    corrected.Add(i % 2 == 0 ? "R" : "L");
                }
                return corrected;
            }

            // 前方向
            string current = firstLabel;
            for (int i = firstValidIdx + 1; i < corrected.Count; i++)
            {
                current = (current == "R") ? "L" : "R";
                corrected[i] = current;
            }

            // 後方向
            current = firstLabel;
            for (int i = firstValidIdx - 1; i >= 0; i--)
            {
                current = (current == "R") ? "L" : "R";
                corrected[i] = current;
            }

            return corrected;
        }

        /// <summary>
        /// 外れ値検出 (IQR or Z-score)
        /// </summary>
        private bool[] DetectOutliers(double[] intervals, string method, double factor)
        {
            int n = intervals.Length;
            bool[] outliers = new bool[n];

            if (n == 0) return outliers;

            if (method == "iqr")
            {
                double[] sorted = (double[])intervals.Clone();
                Array.Sort(sorted);
                double q1 = Percentile(sorted, 25);
                double q3 = Percentile(sorted, 75);
                double iqr = q3 - q1;
                double lower = q1 - factor * iqr;
                double upper = q3 + factor * iqr;

                for (int i = 0; i < n; i++)
                    outliers[i] = (intervals[i] < lower || intervals[i] > upper);
            }
            else if (method == "zscore")
            {
                double mean = Mean(intervals);
                double std = Std(intervals, mean);

                for (int i = 0; i < n; i++)
                {
                    double z = Math.Abs((intervals[i] - mean) / (std + 1e-6));
                    outliers[i] = z > factor;
                }
            }
            return outliers;
        }

        /// <summary>
        /// ロバストなピーク検出
        /// </summary>
        public List<int> RobustPeakDetection(float[] data, float[] time)
        {
            // 1. フィルタリング
            float[] filtered = AdaptiveLowpassFilter(data, 3, 2);

            // 2. スムージング (簡易Savitzky–Golay → 移動平均で代用可)
            float[] smoothed = MovingAverage(filtered, 5);

            // 3. 閾値計算
            double mean = Mean(smoothed);
            double std = Std(smoothed, mean);

            // 複数distanceパラメータで探索
            int[] distances = {
                (int)(fs * peakDistanceFactor * 0.8),
                (int)(fs * peakDistanceFactor * 1.0),
                (int)(fs * peakDistanceFactor * 1.2)
            };

            HashSet<int> allPeaks = new HashSet<int>();
            foreach (int dist in distances)
            {
                List<int> peaks = FindPeaks(smoothed, mean + 0.1 * std, dist);
                foreach (int p in peaks) allPeaks.Add(p);
            }

            List<int> sortedPeaks = new List<int>(allPeaks);
            sortedPeaks.Sort();

            // 4. 外れ値除去
            if (sortedPeaks.Count > 2)
            {
                List<double> intervals = new List<double>();
                for (int i = 1; i < sortedPeaks.Count; i++)
                {
                    double dt = time[sortedPeaks[i]] - time[sortedPeaks[i - 1]];
                    intervals.Add(dt);
                }

                bool[] outliers = DetectOutliers(intervals.ToArray(), "iqr", 1.5);

                List<int> validPeaks = new List<int>();
                validPeaks.Add(sortedPeaks[0]); // 最初のピークは残す
                for (int i = 0; i < outliers.Length; i++)
                {
                    if (!outliers[i])
                        validPeaks.Add(sortedPeaks[i + 1]);
                }
                sortedPeaks = validPeaks;
            }

            return sortedPeaks;
        }

        /// <summary>
        /// ロバストな谷検出
        /// </summary>
        public List<int> RobustValleyDetection(float[] data, float[] time)
        {
            float[] filtered = AdaptiveLowpassFilter(data, 3, 2);
            float[] smoothed = MovingAverage(filtered, 5);

            // 負のピーク検出
            List<int> valleys = FindValleys(smoothed, 0.02, (int)(fs * peakDistanceFactor));

            // 外れ値除去
            if (valleys.Count > 2)
            {
                List<double> intervals = new List<double>();
                for (int i = 1; i < valleys.Count; i++)
                {
                    double dt = time[valleys[i]] - time[valleys[i - 1]];
                    intervals.Add(dt);
                }

                bool[] outliers = DetectOutliers(intervals.ToArray(), "iqr", 1.5);

                List<int> validValleys = new List<int>();
                validValleys.Add(valleys[0]);
                for (int i = 0; i < outliers.Length; i++)
                {
                    if (!outliers[i])
                        validValleys.Add(valleys[i + 1]);
                }
                valleys = validValleys;
            }

            return valleys;
        }

        // ===== Utility =====
        private static List<int> FindPeaks(float[] data, double threshold, int minDist)
        {
            List<int> peaks = new List<int>();
            for (int i = 1; i < data.Length - 1; i++)
            {
                if (data[i] > data[i - 1] && data[i] > data[i + 1] && data[i] > threshold)
                {
                    if (peaks.Count == 0 || i - peaks[peaks.Count - 1] > minDist)
                        peaks.Add(i);
                }
            }
            return peaks;
        }

        private static List<int> FindValleys(float[] data, double prominence, int minDist)
        {
            List<int> valleys = new List<int>();
            for (int i = 1; i < data.Length - 1; i++)
            {
                if (data[i] < data[i - 1] && data[i] < data[i + 1] && data[i] < -prominence)
                {
                    if (valleys.Count == 0 || i - valleys[valleys.Count - 1] > minDist)
                        valleys.Add(i);
                }
            }
            return valleys;
        }

        private static float[] MovingAverage(float[] data, int window)
        {
            int n = data.Length;
            float[] smoothed = new float[n];
            for (int i = 0; i < n; i++)
            {
                int start = Math.Max(0, i - window / 2);
                int end = Math.Min(n - 1, i + window / 2);
                double sum = 0;
                for (int j = start; j <= end; j++) sum += data[j];
                smoothed[i] = (float)(sum / (end - start + 1));
            }
            return smoothed;
        }

        private static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 0) return 0;
            double rank = (p / 100.0) * (sorted.Length - 1);
            int low = (int)Math.Floor(rank);
            int high = (int)Math.Ceiling(rank);
            if (low == high) return sorted[low];
            return sorted[low] + (rank - low) * (sorted[high] - sorted[low]);
        }

        private static double Mean(float[] arr)
        {
            double s = 0; foreach (var v in arr) s += v;
            return s / arr.Length;
        }

        private static double Mean(double[] arr)
        {
            double s = 0; foreach (var v in arr) s += v;
            return s / arr.Length;
        }

        private static double Std(float[] arr, double mean)
        {
            double s = 0; foreach (var v in arr) s += (v - mean) * (v - mean);
            return Math.Sqrt(s / arr.Length);
        }

        private static double Std(double[] arr, double mean)
        {
            double s = 0; foreach (var v in arr) s += (v - mean) * (v - mean);
            return Math.Sqrt(s / arr.Length);
        }

        /// <summary>
        /// GyroZベースのラベル付け
        /// </summary>
        public List<string> LabelByGyroZ(float[] gyroZ, List<int> peaks, int windowMs)
        {
            List<string> labels = new List<string>();
            int halfWindow = (int)(windowMs / 1000.0 * fs);

            // ピークごとに周辺の統計を計算
            List<double> medians = new List<double>();
            List<Dictionary<string, double>> statsList = new List<Dictionary<string, double>>();

            foreach (int idx in peaks)
            {
                int start = Math.Max(0, idx - halfWindow);
                int end = Math.Min(gyroZ.Length, idx + halfWindow);

                var segment = gyroZ.Skip(start).Take(end - start).ToArray();
                double median = Median(segment.Select(x => (double)x));
                double mean = segment.Average();
                double range = segment.Max() - segment.Min();

                var stats = new Dictionary<string, double>
                {
                    {"median", median},
                    {"mean", mean},
                    {"range", range}
                };
                statsList.Add(stats);
                medians.Add(median);
            }

            // 適応的閾値 (MADベース)
            double mad = Median(medians.Select(x => Math.Abs(x - Median(medians))).ToArray());
            double threshold = Math.Max(mad * 1.4826, Std(medians.ToArray(), medians.Average()) * 0.5);

            foreach (var stats in statsList)
            {
                if (stats["median"] > threshold) labels.Add("L");
                else if (stats["median"] < -threshold) labels.Add("R");
                else labels.Add("U");
            }
            return labels;
        }

        /// <summary>
        /// ジャイロ3軸ベクトル解析
        /// </summary>
        public List<string> LabelByGyroVector(float[] gx, float[] gy, float[] gz, List<int> peaks, int windowMs)
        {
            List<string> labels = new List<string>();
            int halfWindow = (int)(windowMs / 1000.0 * fs);

            foreach (int idx in peaks)
            {
                int start = Math.Max(0, idx - halfWindow);
                int end = Math.Min(gz.Length, idx + halfWindow);

                var gxSeg = gx.Skip(start).Take(end - start).ToArray();
                var gySeg = gy.Skip(start).Take(end - start).ToArray();
                var gzSeg = gz.Skip(start).Take(end - start).ToArray();

                double sumMag = 0, sumZ = 0;
                for (int i = 0; i < gxSeg.Length; i++)
                {
                    double mag = Math.Sqrt(gxSeg[i] * gxSeg[i] + gySeg[i] * gySeg[i] + gzSeg[i] * gzSeg[i]);
                    sumMag += mag;
                    sumZ += Math.Abs(gzSeg[i]);
                }

                double dominance = sumZ / (sumMag + 1e-6);
                double meanGz = gzSeg.Average();

                if (dominance > 0.3)
                    labels.Add(meanGz > 0 ? "L" : "R");
                else
                    labels.Add("U");
            }
            return labels;
        }

        /// <summary>
        /// 加速度パターンベース
        /// </summary>
        public List<string> LabelByAccPattern(float[] ax, float[] ay, List<int> peaks, int windowMs)
        {
            List<string> labels = new List<string>();
            int halfWindow = (int)(windowMs / 1000.0 * fs);

            foreach (int idx in peaks)
            {
                int start = Math.Max(0, idx - halfWindow);
                int end = Math.Min(ax.Length, idx + halfWindow);

                var axSeg = ax.Skip(start).Take(end - start).ToArray();
                var aySeg = ay.Skip(start).Take(end - start).ToArray();

                double meanX = axSeg.Average();
                double corr = Correlation(axSeg, aySeg);

                if (Math.Abs(meanX) > 0.05)
                {
                    labels.Add(meanX > 0 ? "R" : "L");
                }
                else
                {
                    labels.Add("U");
                }
            }
            return labels;
        }

        /// <summary>
        /// テンプレートマッチング (GyroZパターン相関)
        /// </summary>
        public List<string> LabelByTemplate(float[] gyroZ, List<int> peaks, int windowMs)
        {
            List<string> labels = new List<string>();
            int halfWindow = (int)(windowMs / 1000.0 * fs);

            if (peaks.Count < 4)
                return Enumerable.Repeat("U", peaks.Count).ToList();

            // 最初の数ステップでテンプレート作成
            Dictionary<string, List<float[]>> templates = new Dictionary<string, List<float[]>>
            {
                {"L", new List<float[]>()},
                {"R", new List<float[]>()}
            };

            var initialLabels = LabelByGyroZ(gyroZ, peaks.Take(6).ToList(), windowMs);
            for (int i = 0; i < Math.Min(6, peaks.Count); i++)
            {
                if (initialLabels[i] == "L" || initialLabels[i] == "R")
                {
                    int start = Math.Max(0, peaks[i] - halfWindow);
                    int end = Math.Min(gyroZ.Length, peaks[i] + halfWindow);
                    float[] pattern = gyroZ.Skip(start).Take(end - start).ToArray();
                    if (pattern.Length == 2 * halfWindow)
                        templates[initialLabels[i]].Add(pattern);
                }
            }

            // 平均テンプレート
            Dictionary<string, float[]> avgTemplates = new Dictionary<string, float[]>();
            foreach (var kv in templates)
            {
                if (kv.Value.Count > 0)
                {
                    int len = kv.Value[0].Length;
                    float[] avg = new float[len];
                    foreach (var arr in kv.Value)
                        for (int j = 0; j < len; j++) avg[j] += arr[j];
                    for (int j = 0; j < len; j++) avg[j] /= kv.Value.Count;
                    avgTemplates[kv.Key] = avg;
                }
            }

            // 各ピークをテンプレートと相関比較
            foreach (int idx in peaks)
            {
                int start = Math.Max(0, idx - halfWindow);
                int end = Math.Min(gyroZ.Length, idx + halfWindow);
                float[] pattern = gyroZ.Skip(start).Take(end - start).ToArray();

                string best = "U";
                double bestCorr = 0;
                foreach (var kv in avgTemplates)
                {
                    if (pattern.Length == kv.Value.Length)
                    {
                        double corr = Correlation(pattern, kv.Value);
                        if (corr > bestCorr)
                        {
                            bestCorr = corr;
                            best = kv.Key;
                        }
                    }
                }

                labels.Add(bestCorr > 0.3 ? best : "U");
            }
            return labels;
        }

        /// <summary>
        /// アンサンブル投票 + 信頼度
        /// </summary>
        public EnsembleResult EnsembleLabeling(List<List<string>> methodResults, int nPeaks)
        {
            EnsembleResult result = new EnsembleResult();

            for (int i = 0; i < nPeaks; i++)
            {
                int votesL = 0, votesR = 0, votesU = 0;
                foreach (var method in methodResults)
                {
                    if (i < method.Count)
                    {
                        string label = method[i];
                        if (label == "L") votesL++;
                        else if (label == "R") votesR++;
                        else votesU++;
                    }
                }

                string final;
                double conf;
                int totalVotes = votesL + votesR + votesU;

                if (votesL > votesR && votesL > votesU)
                {
                    final = "L";
                    conf = (double)votesL / totalVotes;
                }
                else if (votesR > votesL && votesR > votesU)
                {
                    final = "R";
                    conf = (double)votesR / totalVotes;
                }
                else
                {
                    final = "U";
                    conf = 0.0;
                }

                result.FinalLabels.Add(final);
                result.Confidence.Add(conf);
            }

            return result;
        }


        // ========== Utility ==========
        private static double Median(IEnumerable<double> seq)
        {
            var sorted = seq.OrderBy(x => x).ToArray();
            int n = sorted.Length;
            if (n == 0) return 0;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        private static double Correlation(IList<float> a, IList<float> b)
        {
            if (a.Count != b.Count || a.Count == 0) return 0;
            double meanA = a.Average();
            double meanB = b.Average();
            double num = 0, denA = 0, denB = 0;
            for (int i = 0; i < a.Count; i++)
            {
                double da = a[i] - meanA;
                double db = b[i] - meanB;
                num += da * db;
                denA += da * da;
                denB += db * db;
            }
            return num / (Math.Sqrt(denA * denB) + 1e-6);
        }

        private static double Correlation(float[] a, float[] b)
        {
            return Correlation(a.ToList(), b.ToList());
        }



    }
}
