using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoreLinkSys1.Utilities;

namespace CoreLinkSys1.Analysis
{
    /// <summary>
    /// ペダル動作区間
    /// </summary>
    public class PedalSegment
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string Type { get; set; } // "Rising", "Falling", "Steady"
        public double Slope { get; set; }
        public double Duration { get; set; } // 区間の持続時間（秒）
    }

    /// <summary>
    /// 汎用計算メソッド群
    /// </summary>
    public static class CalcMethod
    {
        // --- 速度計算 ---
        public static List<double> ComputeVelocity(List<double> data, double dt)
        {
            return ComputeVelocity(data, dt, 2);
        }
        public static List<double> ComputeVelocity(List<double> data, double dt, int step)
        {
            var velocity = new List<double>();
            for (int i = 0; i < data.Count - step; i++)
                velocity.Add((data[i + step] - data[i]) / (dt * step));
            velocity.Add(0);
            return velocity;
        }



        public static List<double> ComputeVelocity2(List<double> data, double dt)
        {
            return ComputeVelocity2(data, dt, 2);
        }

        public static List<double> ComputeVelocity2(List<double> data, double dt, int step)
        {
            if (data == null || data.Count == 0) return new List<double>();

            var velocity = new List<double>();
            for (int i = step; i < data.Count - step; i++)
                velocity.Add((data[i + step] - data[i - step]) / (2 * dt * step));

            double firstVal = velocity.Count > 0 ? velocity[0] : 0.0;
            for (int i = 0; i < step; i++) velocity.Insert(0, firstVal);

            double lastVal = velocity.Count > 0 ? velocity[velocity.Count - 1] : 0.0;
            while (velocity.Count < data.Count) velocity.Add(lastVal);

            return velocity;
        }

        // --- 平滑化 ---
        public static List<double> Smooth(List<double> data, int window)
        {
            var smoothed = new List<double>();
            for (int i = 0; i < data.Count; i++)
            {
                int start = Math.Max(0, i - window / 2);
                int end = Math.Min(data.Count - 1, i + window / 2);
                double avg = 0;
                for (int j = start; j <= end; j++) avg += data[j];
                smoothed.Add(avg / (end - start + 1));
            }
            return smoothed;
        }

        public static List<double> SmoothExcludeLargeJumps(List<double> data, int window, double diffThreshold)
        {
            var smoothed = new List<double>();
            for (int i = 0; i < data.Count; i++)
            {
                int start = Math.Max(0, i - window / 2);
                int end = Math.Min(data.Count - 1, i + window / 2);

                // ウィンドウ内のデータ抽出
                var windowData = new List<double> { data[start] };
                for (int j = start + 1; j <= end; j++)
                {
                    double diff = Math.Abs(data[j] - data[j - 1]);
                    if (diff <= diffThreshold) // 変化量が小さい場合のみ採用
                    {
                        windowData.Add(data[j]);
                    }
                }

                // 平均を追加（ウィンドウ内がすべて外れ値なら元データを使う）
                smoothed.Add(windowData.Count > 0 ? windowData.Average() : data[i]);
            }
            return smoothed;
        }

        public static List<double> SmoothMedian(List<double> data, int window)
        {
            var smoothed = new List<double>();
            for (int i = 0; i < data.Count; i++)
            {
                int start = Math.Max(0, i - window / 2);
                int end = Math.Min(data.Count - 1, i + window / 2);
                var windowData = data.GetRange(start, end - start + 1);
                windowData.Sort();
                smoothed.Add(windowData[windowData.Count / 2]); // 中央値
            }
            return smoothed;
        }

        // 平滑化してから速度計算（引数指定あり）
        public static List<double> ComputeVelocitySmoothed(List<double> data, double dt, int smoothWindow, int step)
        {
            if (data == null || data.Count == 0) return new List<double>();

            // ① 平滑化
            var smoothed = Smooth(data, smoothWindow);

            // ② 速度計算（中央差分法）
            return ComputeVelocity2(smoothed, dt, step);
        }

        // 平滑化してから速度計算（デフォルト値を使う版）
        public static List<double> ComputeVelocitySmoothed(List<double> data, double dt)
        {
            return ComputeVelocitySmoothed(data, dt, 5, 2); // デフォルト: 窓幅5, 差分ステップ2
        }

        public static List<double> Smooth(List<double> data)
        {
            return Smooth(data, 5);
        }

        // --- COP移動距離 ---
        public static double CalcPathLength(List<double> copYValues)
        {
            return CalcPathLength(copYValues, 0.5);
        }
        public static double CalcPathLength(List<double> copYValues, double threshold)
        {
            if (copYValues == null || copYValues.Count < 2) return 0.0;

            double pathLength = 0.0;
            for (int i = 1; i < copYValues.Count; i++)
            {
                if (copYValues[i] < -15 || copYValues[i] > 15 ||
                    copYValues[i - 1] < -15 || copYValues[i - 1] > 15)
                    continue;

                double diff = Math.Abs(copYValues[i] - copYValues[i - 1]);

                // ここで差分をログ出力
                //DebugTool.DebugLog(string.Format("COP diff[{0}-{1}] = {2:F6}", i - 1, i, diff));

                if (diff <= threshold) pathLength += diff;
            }
            return pathLength;
        }

        // --- ペダルラベリング ---
        public static List<string> LabelPedalPhases(List<double> pedalData, double dt)
        {
            return LabelPedalPhases(pedalData, dt, 0.02);
        }
        public static List<string> LabelPedalPhases(List<double> pedalData, double dt, double velocityThreshold)
        {
            var labels = new List<string>();
            if (pedalData == null || pedalData.Count < 2) return labels;

            var velocity = Smooth(ComputeVelocity(pedalData, dt), 7);

            foreach (var v in velocity)
            {
                if (v > velocityThreshold) labels.Add("Rising");
                else if (v < -velocityThreshold) labels.Add("Falling");
                else labels.Add("Flat");
            }
            return labels;
        }

        // --- ペダル開始インデックス ---
        public static int FindPedalStartIndex(List<double> pedalValues)
        {
            for (int i = 0; i < pedalValues.Count; i++)
                if (pedalValues[i] >= 0.01) return i;
            return -1;
        }

        // --- 動的ペダル波形セグメント（新しい実装） ---
        /// <summary>
        /// 傾きベースでヒステリシスと最小持続時間を考慮したペダル区間分割
        /// </summary>
        /// <param name="values">ペダル値のリスト</param>
        /// <param name="windowSize">傾き計算用の窓サイズ</param>
        /// <param name="risingThreshold">上昇判定の閾値</param>
        /// <param name="fallingThreshold">下降判定の閾値</param>
        /// <param name="steadyThreshold">停止判定の閾値（ヒステリシス用）</param>
        /// <param name="minDurationSec">最小持続時間（秒）</param>
        /// <param name="sampleRate">サンプリングレート（Hz）</param>
        /// <returns>区間分割されたセグメントのリスト</returns>
        public static List<PedalSegment> SegmentPedalWaveformDynamic(
            List<double> values,
            int windowSize,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold,
            double minDurationSec,
            double sampleRate)
        {
            var segments = new List<PedalSegment>();
            if (values == null || values.Count < windowSize) return segments;

            int minDurationSamples = (int)(minDurationSec * sampleRate);

            // 各点での傾きを計算
            var slopes = new List<double>();
            for (int i = 0; i <= values.Count - windowSize; i++)
            {
                List<double> window = values.GetRange(i, windowSize);
                double slope = CalculateSlope(window);
                slopes.Add(slope);
            }

            // 初期状態を決定
            string currentState = DetermineInitialState(slopes[0], risingThreshold, fallingThreshold, steadyThreshold);
            int segmentStart = 0;

            for (int i = 1; i < slopes.Count; i++)
            {
                double slope = slopes[i];
                string newState = DetermineNewState(currentState, slope,
                    risingThreshold, fallingThreshold, steadyThreshold);

                // 状態が変化した場合
                if (newState != currentState)
                {
                    int segmentEnd = i + windowSize - 1;
                    double duration = (segmentEnd - segmentStart) / sampleRate;

                    // 最小持続時間チェック
                    if (duration >= minDurationSec || segments.Count == 0)
                    {
                        segments.Add(new PedalSegment
                        {
                            StartIndex = segmentStart,
                            EndIndex = segmentEnd,
                            Type = currentState,
                            Slope = CalculateAverageSlope(slopes, Math.Max(0, segmentStart), Math.Min(slopes.Count - 1, i)),
                            Duration = duration
                        });

                        segmentStart = i;
                        currentState = newState;
                    }
                }
            }

            // 最後のセグメント追加
            if (segmentStart < slopes.Count)
            {
                int finalEnd = slopes.Count + windowSize - 1;
                double finalDuration = (finalEnd - segmentStart) / sampleRate;

                segments.Add(new PedalSegment
                {
                    StartIndex = segmentStart,
                    EndIndex = Math.Min(finalEnd, values.Count - 1),
                    Type = currentState,
                    Slope = CalculateAverageSlope(slopes, segmentStart, slopes.Count - 1),
                    Duration = finalDuration
                });
            }

            return segments;
        }

        // ★デフォルト引数用のオーバーロード
        public static List<PedalSegment> SegmentPedalWaveformDynamic(List<double> values)
        {
            return SegmentPedalWaveformDynamic(values, 20, 0.05, -0.05, 0.02, 0.5, 100.0);
        }

        // 6引数オーバーロード（sampleRate省略時は100Hz固定）
        public static List<PedalSegment> SegmentPedalWaveformDynamic(
            List<double> values,
            int windowSize,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold,
            double minDurationSec)
        {
            return SegmentPedalWaveformDynamic(values, windowSize, risingThreshold, fallingThreshold, steadyThreshold, minDurationSec, 100.0);
        }

        public static List<PedalSegment> SegmentPedalWaveformDynamic(
            List<double> values,
            int windowSize,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold)
        {
            // minDurationSec = 0.5, sampleRate = 100.0 を仮定
            return SegmentPedalWaveformDynamic(values, windowSize, risingThreshold, fallingThreshold, steadyThreshold, 0.5, 100.0);
        }


        /// <summary>
        /// 初期状態を傾きから決定
        /// </summary>
        private static string DetermineInitialState(double slope, double risingThreshold, double fallingThreshold, double steadyThreshold)
        {
            if (slope > risingThreshold) return "Rising";
            if (slope < fallingThreshold) return "Falling";
            return "Steady";
        }

        /// <summary>
        /// ヒステリシスを考慮した新しい状態の決定
        /// </summary>
        private static string DetermineNewState(string currentState, double slope,
            double risingThreshold, double fallingThreshold, double steadyThreshold)
        {
            switch (currentState)
            {
                case "Rising":
                    // 上昇中は、停止閾値を下回ったら停止、下降閾値を下回ったら下降
                    if (slope < steadyThreshold && slope > fallingThreshold) return "Steady";
                    if (slope < fallingThreshold) return "Falling";
                    return "Rising";

                case "Falling":
                    // 下降中は、停止閾値を上回ったら停止、上昇閾値を上回ったら上昇
                    if (slope > -steadyThreshold && slope < risingThreshold) return "Steady";
                    if (slope > risingThreshold) return "Rising";
                    return "Falling";

                case "Steady":
                default:
                    // 停止中は、上昇/下降閾値を超えたら状態変更
                    if (slope > risingThreshold) return "Rising";
                    if (slope < fallingThreshold) return "Falling";
                    return "Steady";
            }
        }

        /// <summary>
        /// 指定範囲の平均傾きを計算
        /// </summary>
        private static double CalculateAverageSlope(List<double> slopes, int startIndex, int endIndex)
        {
            if (startIndex >= endIndex || startIndex < 0 || endIndex >= slopes.Count)
                return 0.0;

            double sum = 0.0;
            int count = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                sum += slopes[i];
                count++;
            }
            return count > 0 ? sum / count : 0.0;
        }

        // --- 既存のペダル波形セグメント（互換性のため保持） ---
        public static List<PedalSegment> SegmentPedalWaveform(List<double> values, int windowSize, double slopeThreshold)
        {
            // 新しい動的セグメント化を使用（デフォルトパラメータで）
            return SegmentPedalWaveformDynamic(values, windowSize, slopeThreshold, -slopeThreshold, slopeThreshold * 0.4);
        }

        private static double CalculateSlope(List<double> values)
        {
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            int n = values.Count;
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXY += i * values[i];
                sumXX += i * i;
            }
            return (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        }
    }

    /// <summary>
    /// 動的ペダル区間分割専用クラス
    /// </summary>
    public class PedalProcessor
    {
        /// <summary>
        /// 動的な傾きベース区間分割（推奨メソッド）
        /// </summary>
        /// <param name="pedalValues">ペダル値のリスト</param>
        /// <param name="startTime">開始時刻（秒）</param>
        /// <param name="sampleRate">サンプリングレート（Hz）</param>
        /// <returns>動的に分割されたセグメントのリスト</returns>
        public static List<PedalSegment> GenerateDynamicSegments(
        List<double> pedalValues,
        double startTime,
        double sampleRate)
        {
            if (pedalValues == null || pedalValues.Count == 0 || startTime < 0)
                return new List<PedalSegment>();

            // 開始インデックスを計算
            int startIndex = (int)(startTime * sampleRate);
            if (startIndex >= pedalValues.Count)
                return new List<PedalSegment>();

            // 開始点以降のデータを抽出
            var relevantData = pedalValues.Skip(startIndex).ToList();

            // 動的セグメント化を実行
            var segments = CalcMethod.SegmentPedalWaveformDynamic(
                relevantData,
                20,      // windowSize: 0.2秒の窓
                0.08,    // risingThreshold: 上昇判定閾値
                -0.08,   // fallingThreshold: 下降判定閾値
                0.03,    // steadyThreshold: 停止判定閾値（ヒステリシス）
                0.8,     // minDurationSec: 最小持続時間
                sampleRate // sampleRate: サンプリングレート
            );

            // インデックスを元のデータに合わせて調整
            foreach (var segment in segments)
            {
                segment.StartIndex += startIndex;
                segment.EndIndex += startIndex;
            }

            return segments;
        }

        // ★ デフォルト値用オーバーロードを追加
        public static List<PedalSegment> GenerateDynamicSegments(
            List<double> pedalValues,
            double startTime)
        {
            return GenerateDynamicSegments(pedalValues, startTime, 100.0);
        }



        // === 既存の固定時間パターン（下位互換性のため保持、但し非推奨） ===
        public struct Interval { public double Duration; public string Type; public Interval(double d, string t) { Duration = d; Type = t; } }

        [Obsolete("固定時間パターンは非推奨です。GenerateDynamicSegments()を使用してください。")]
        public static List<PedalSegment> GenerateFixedTimeSegments(double startTime)
        {
            return GenerateFixedTimeSegments(startTime, 100.0);
        }

        [Obsolete("固定時間パターンは非推奨です。GenerateDynamicSegments()を使用してください。")]
        public static List<PedalSegment> GenerateFixedTimeSegments(double startTime, double sampleRate)
        {
            var segments = new List<PedalSegment>();
            if (startTime < 0) return segments;

            List<Interval> intervalPattern = new List<Interval>
            {
                new Interval(8.0, "Rising"), new Interval(4.0, "Steady"),
                new Interval(8.0, "Falling"), new Interval(8.0, "Steady"),
                new Interval(4.0, "Rising"), new Interval(4.0, "Steady"),
                new Interval(4.0, "Falling"), new Interval(8.0, "Steady"),
                new Interval(8.0, "Rising"), new Interval(4.0, "Steady"),
                new Interval(2.0, "Falling"), new Interval(4.0, "Steady"),

                new Interval(4.0, "Rising"), new Interval(4.0, "Steady"),
                new Interval(4.0, "Falling"), new Interval(8.0, "Steady"),
                new Interval(8.0, "Rising"), new Interval(4.0, "Steady"),
                new Interval(8.0, "Falling"), new Interval(8.0, "Steady"),
                new Interval(4.0, "Rising"), new Interval(4.0, "Steady"),
                new Interval(4.0, "Falling")
            };

            int currentIndex = (int)(startTime * sampleRate);
            foreach (var interval in intervalPattern)
            {
                int startIndex = currentIndex;
                int endIndex = startIndex + (int)(interval.Duration * sampleRate) - 1;
                segments.Add(new PedalSegment
                {
                    StartIndex = startIndex,
                    EndIndex = endIndex,
                    Type = interval.Type,
                    Slope = GetDefaultSlopeForType(interval.Type),
                    Duration = interval.Duration
                });
                currentIndex = endIndex + 1;
            }
            return segments;
        }

        private static double GetDefaultSlopeForType(string type)
        {
            if (type == "Rising") return 0.5;
            if (type == "Falling") return -0.5;
            return 0.0;
        }

        /// <summary>
        /// ペダル速度に基づいて波形を「上昇(Rising)」「下降(Falling)」「停止(Steady)」に区間分割する関数。
        /// ヒステリシス（2段階閾値）と最小持続時間により、誤判定を抑制して安定した区間切り分けを行う。
        /// </summary>
        /// <param name="pedalValues">ペダル角度や位置の時系列データ（生波形）</param>
        /// <param name="pedalVelocity">ペダル速度（pedalValuesから計算した微分値）</param>
        /// <param name="risingThreshold">上昇と判定する速度の下限値（例: +0.08）</param>
        /// <param name="fallingThreshold">下降と判定する速度の上限値（例: -0.08）</param>
        /// <param name="steadyThreshold">停止と判定する速度の絶対値上限（例: ±0.03）rising/falling より狭い設定が望ましい。</param>
        /// <param name="minDurationSec">1区間の最小持続時間（秒）これ未満の短い区間は無視される（ノイズ除去）。</param> 
        /// <param name="sampleRate">サンプリング周波数（Hz）</param>
        /// <returns>区間リスト（各区間は開始・終了インデックス、区間種別、平均速度、持続時間を含む）</returns>
        public static List<PedalSegment> SegmentPedalByVelocity(
        List<double> pedalValues,
        List<double> pedalVelocity,
        double risingThreshold,
        double fallingThreshold,
        double steadyThreshold,
        double minDurationSec,
        double sampleRate)
        {
            var segments = new List<PedalSegment>();
            if (pedalValues == null || pedalVelocity == null || pedalValues.Count != pedalVelocity.Count)
                return segments;

            int minDurationSamples = (int)(minDurationSec * sampleRate);
            if (pedalValues.Count == 0) return segments;

            // --- 初期状態決定 ---
            // 最初の速度値から、初期状態（Rising / Falling / Steady）を判定
            string currentState = DetermineInitialStateByVelocity(pedalVelocity[0], risingThreshold, fallingThreshold, steadyThreshold);
            int segmentStart = 0;// 現在の区間の開始インデックス

            // --- メインループ：速度に基づく状態遷移 ---
            for (int i = 1; i < pedalVelocity.Count; i++)
            {
                // 現在状態と最新の速度値から、新しい状態を判定
                string newState = DetermineNewStateByVelocity(currentState, pedalVelocity[i],
                    risingThreshold, fallingThreshold, steadyThreshold);

                // 状態が変わった場合 → 区間終了
                if (newState != currentState)
                {
                    int segmentEnd = i - 1;
                    double duration = (segmentEnd - segmentStart + 1) / sampleRate;

                    // 区間が最小持続時間以上なら確定（または最初の区間なら強制確定）
                    if (duration >= minDurationSec || segments.Count == 0)
                    {
                        segments.Add(new PedalSegment
                        {
                            StartIndex = segmentStart,
                            EndIndex = segmentEnd,
                            Type = currentState,// Rising / Falling / Steady
                            Slope = CalculateAverageVelocity(pedalVelocity, segmentStart, segmentEnd),
                            Duration = duration
                        });

                        // 新しい区間を開始
                        segmentStart = i;
                        currentState = newState;
                    }
                }
            }

            // --- 最後の区間を追加 ---
            if (segmentStart < pedalValues.Count)
            {
                segments.Add(new PedalSegment
                {
                    StartIndex = segmentStart,
                    EndIndex = pedalValues.Count - 1,
                    Type = currentState,
                    Slope = CalculateAverageVelocity(pedalVelocity, segmentStart, pedalValues.Count - 1),
                    Duration = (pedalValues.Count - segmentStart) / sampleRate
                });
            }

            return segments;
        }

        public static List<PedalSegment> SegmentPedalByVelocityV2(
            List<double> pedalValues,
            List<double> pedalVelocity,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold,
            double minDurRisingSec,   // 例: 6.0
            double minDurSteadySec,   // 例: 2.0
            double minDurFallingSec,  // 例: 6.0
            double sampleRate)
        {
            var segments = new List<PedalSegment>();
            if (pedalValues == null || pedalVelocity == null || pedalValues.Count != pedalVelocity.Count)
                return segments;
            if (pedalValues.Count == 0) return segments;

            string currentState = DetermineInitialStateByVelocity(
                pedalVelocity[0], risingThreshold, fallingThreshold, steadyThreshold);
            int segmentStart = 0;

            for (int i = 1; i < pedalVelocity.Count; i++)
            {
                string newState = DetermineNewStateByVelocity(
                    currentState, pedalVelocity[i], risingThreshold, fallingThreshold, steadyThreshold);

                if (newState != currentState)
                {
                    int segmentEnd = i - 1;
                    int lenSamples = segmentEnd - segmentStart + 1;

                    // 最初の区間は強制確定。それ以外は状態別の最小長さを満たすか判定
                    bool ok = (segments.Count == 0) ||
                            (lenSamples >= GetMinSamplesForState(
                                currentState,
                                minDurRisingSec, minDurSteadySec, minDurFallingSec,
                                sampleRate));

                    if (ok)
                    {
                        double durationSec = lenSamples / sampleRate;
                        segments.Add(new PedalSegment
                        {
                            StartIndex = segmentStart,
                            EndIndex = segmentEnd,
                            Type = currentState,
                            Slope = CalculateAverageVelocity(pedalVelocity, segmentStart, segmentEnd),
                            Duration = durationSec
                        });

                        segmentStart = i;
                        currentState = newState;
                    }
                    // 満たさない場合は遷移を無視して現状態を継続
                }
            }

            if (segmentStart < pedalValues.Count)
            {
                int segmentEnd = pedalValues.Count - 1;
                segments.Add(new PedalSegment
                {
                    StartIndex = segmentStart,
                    EndIndex = segmentEnd,
                    Type = currentState,
                    Slope = CalculateAverageVelocity(pedalVelocity, segmentStart, segmentEnd),
                    Duration = (segmentEnd - segmentStart + 1) / sampleRate
                });
            }

            return segments;
        }

        // ★ C#2008対応：クラス内のヘルパーとして外出し
        private static int GetMinSamplesForState(
            string state,
            double minDurRisingSec,
            double minDurSteadySec,
            double minDurFallingSec,
            double sampleRate)
        {
            if (state == "Rising") return (int)Math.Round(minDurRisingSec * sampleRate);
            if (state == "Falling") return (int)Math.Round(minDurFallingSec * sampleRate);
            return (int)Math.Round(minDurSteadySec * sampleRate); // "Steady"
        }

        // デフォルト糖衣（R=6s, S=2s, F=6s, fs=100Hz）
        public static List<PedalSegment> SegmentPedalByVelocityV2(
            List<double> pedalValues,
            List<double> pedalVelocity,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold)
        {
            return SegmentPedalByVelocityV2(
                pedalValues, pedalVelocity,
                risingThreshold, fallingThreshold, steadyThreshold,
                6.0, 2.0, 6.0, 100.0);
        }

        private static string DetermineInitialStateByVelocity(double velocity,
            double risingThreshold, double fallingThreshold, double steadyThreshold)
        {
            if (velocity > risingThreshold) return "Rising";
            if (velocity < fallingThreshold) return "Falling";
            return "Steady";
        }

        public static List<PedalSegment> SegmentPedalByVelocityV3(
            List<double> pedalValues,
            List<double> pedalVelocity,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold,
            double steadyStdThreshold,
            double minDurRisingSec,
            double minDurSteadySec,
            double minDurFallingSec,
            double sampleRate,
            int windowSize)
        {
            var segments = new List<PedalSegment>();
            if (pedalValues == null || pedalVelocity == null || pedalValues.Count != pedalVelocity.Count)
                return segments;
            if (pedalValues.Count == 0) return segments;

            string currentState = DetermineState(pedalVelocity, 0, risingThreshold, fallingThreshold, steadyThreshold, steadyStdThreshold, windowSize);
            int segmentStart = 0;

            for (int i = 1; i < pedalVelocity.Count; i++)
            {
                string newState = DetermineState(pedalVelocity, i, risingThreshold, fallingThreshold, steadyThreshold, steadyStdThreshold, windowSize);

                if (newState != currentState)
                {
                    int segmentEnd = i - 1;
                    int lenSamples = segmentEnd - segmentStart + 1;

                    bool ok = (segments.Count == 0) || (lenSamples >= GetMinSamplesForState(currentState, minDurRisingSec, minDurSteadySec, minDurFallingSec, sampleRate));

                    if (ok)
                    {
                        double durationSec = lenSamples / sampleRate;
                        segments.Add(new PedalSegment
                        {
                            StartIndex = segmentStart,
                            EndIndex = segmentEnd,
                            Type = currentState,
                            Slope = CalculateAverageVelocity(pedalVelocity, segmentStart, segmentEnd),
                            Duration = durationSec
                        });

                        segmentStart = i;
                        currentState = newState;
                    }
                    // 遷移条件を満たさない場合は継続
                }
            }

            // 最後の区間
            if (segmentStart < pedalVelocity.Count)
            {
                int segmentEnd = pedalVelocity.Count - 1;
                segments.Add(new PedalSegment
                {
                    StartIndex = segmentStart,
                    EndIndex = segmentEnd,
                    Type = currentState,
                    Slope = CalculateAverageVelocity(pedalVelocity, segmentStart, segmentEnd),
                    Duration = (segmentEnd - segmentStart + 1) / sampleRate
                });
            }

            return segments;
        }


        private static string DetermineState(
            List<double> velocities,
            int centerIndex,
            double risingThreshold,
            double fallingThreshold,
            double steadyThreshold,
            double steadyStdThreshold,
            int windowSize)
        {
            List<double> window = GetWindow(velocities, centerIndex, windowSize);
            double mean = window.Average();
            double stdDev = Math.Sqrt(window.Average(v => Math.Pow(v - mean, 2)));

            // 上昇・下降の判定は、中心点の速度値で従来通り行う
            double velocity = velocities[centerIndex];
            if (velocity > risingThreshold)
            {
                return "Rising";
            }
            else if (velocity < fallingThreshold)
            {
                return "Falling";
            }
            else
            {
                // if ((stdDev < steadyStdThreshold) && (Math.Abs(velocity) < steadyThreshold))
                //     return "Steady";
                // else
                //     return "Transition";
                return "Steady";
            }

        }


        private static List<double> GetWindow(List<double> data, int center, int size)
        {
            int half = size / 2;
            int start = Math.Max(0, center - half);
            int end = Math.Min(data.Count - 1, center + half);
            return data.GetRange(start, end - start + 1);
        }











        private static string DetermineNewStateByVelocity(string currentState, double velocity,
            double risingThreshold, double fallingThreshold, double steadyThreshold)
        {
            switch (currentState)
            {
                case "Rising":
                    // 上昇中は、停止閾値を下回ったら停止、下降閾値を下回ったら下降
                    if (velocity < steadyThreshold && velocity > fallingThreshold) return "Steady";
                    if (velocity < fallingThreshold) return "Falling";
                    return "Rising";

                case "Falling":
                    // 下降中は、停止閾値を上回ったら停止、上昇閾値を上回ったら上昇
                    if (velocity > -steadyThreshold && velocity < risingThreshold) return "Steady";
                    if (velocity > risingThreshold) return "Rising";
                    return "Falling";

                case "Steady":
                default:
                    // 停止中は、上昇/下降閾値を超えたら状態変更
                    if (velocity > risingThreshold) return "Rising";
                    if (velocity < fallingThreshold) return "Falling";
                    return "Steady";
            }
        }

        private static double CalculateAverageVelocity(List<double> velocities, int startIndex, int endIndex)
        {
            if (startIndex >= endIndex || startIndex < 0 || endIndex >= velocities.Count)
                return 0.0;

            double sum = 0.0;
            int count = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                sum += velocities[i];
                count++;
            }
            return count > 0 ? sum / count : 0.0;
        }

        // CoreLinkSys1.Analysis.CalcMethod に追記
        public static double CalcStdDev(List<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double avg = values.Average();
            double sumSq = 0.0;
            for (int i = 0; i < values.Count; i++)
            {
                double d = values[i] - avg;
                sumSq += d * d;
            }
            return Math.Sqrt(sumSq / values.Count);
        }

        public static double CalcMaxMinRange(List<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double max = values.Max();
            double min = values.Min();
            return Math.Abs(max - min);
        }



        private static bool IsValidTransition(string prev, string next)
        {
            if (prev == "Steady" && (next == "Rising" || next == "Falling")) return true;
            if ((prev == "Rising" || prev == "Falling") && next == "Steady") return true;
            return false;
        }


        public static List<PedalSegment> CorrectToSteadyRisingPattern(List<PedalSegment> segments, double sampleRate)
        {
            if (segments == null || segments.Count < 3) return segments;

            var corrected = new List<PedalSegment>(segments);

            for (int i = 2; i < corrected.Count; i++)
            {
                var prev2 = corrected[i - 2];
                var prev1 = corrected[i - 1];
                var current = corrected[i];

                // 上昇→停止→上昇 パターン検出
                if (prev2.Type == "Rising" && prev1.Type == "Steady" && current.Type == "Rising")
                {
                    current.Type = "Steady"; // 上昇②を停止に変換
                    current.Slope = 0.0;
                }
                // 下降→停止→下降 パターン検出
                else if (prev2.Type == "Falling" && prev1.Type == "Steady" && current.Type == "Falling")
                {
                    current.Type = "Steady"; // 下降②を停止に変換
                    current.Slope = 0.0;
                }
            }

            // 同じタイプの連続セグメントを結合
            return MergeConsecutiveSegments(corrected, sampleRate);
        }

        private static List<PedalSegment> MergeConsecutiveSegments(List<PedalSegment> segments, double sampleRate)
        {
            if (segments.Count == 0) return segments;

            var merged = new List<PedalSegment> { segments[0] };

            for (int i = 1; i < segments.Count; i++)
            {
                var last = merged.Last();
                var current = segments[i];

                if (last.Type == current.Type)
                {
                    // 同じタイプのセグメントを結合
                    last.EndIndex = current.EndIndex;
                    last.Duration += current.Duration;
                    last.Slope = (last.Slope * last.Duration + current.Slope * current.Duration)
                                / (last.Duration + current.Duration);
                }
                else
                {
                    merged.Add(current);
                }
            }

            return merged;
        }

    }
}
