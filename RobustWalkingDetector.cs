using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoreLinkSys1.Analysis
{
    public class RobustWalkingDetector
    {
        private int fs;


        // デフォルト値を持たないコンストラクタ
        public RobustWalkingDetector(int sampleRate)
        {
            this.fs = sampleRate;
        }

        // デフォルト用オーバーロード (100Hz)
        public RobustWalkingDetector() : this(100)
        {
        }

        public void DetectWalking(List<ProcessedRecord> records)
        {
            int n = records.Count;
            if (n < fs) return;

            float[] accX = records.Select(r => r.RawAccX).ToArray();
            float[] accY = records.Select(r => r.RawAccY).ToArray();
            float[] accZ = records.Select(r => r.RawAccZ).ToArray();
            float[] gyroX = records.Select(r => r.RawGyroX).ToArray();
            float[] gyroY = records.Select(r => r.RawGyroY).ToArray();
            float[] gyroZ = records.Select(r => r.RawGyroZ).ToArray();
            float[] time = records.Select(r => r.Time).ToArray();

            // --- 1. フィルタリング ---
            float[] accXf = FilterUtils.ButterworthLowpass(accX, fs, 5, 4);
            float[] accYf = FilterUtils.ButterworthLowpass(accY, fs, 5, 4);
            float[] accZf = FilterUtils.ButterworthLowpass(accZ, fs, 5, 4);

            // --- 2. 特徴量計算 ---
            float[] accMag = new float[n];
            for (int i = 0; i < n; i++)
                accMag[i] = (float)Math.Sqrt(accXf[i] * accXf[i] + accYf[i] * accYf[i] + accZf[i] * accZf[i]);

            float[] accDev = RollingDeviation(accMag, fs);

            // 垂直方向バンドパス
            float[] accZbp = FilterUtils.ButterworthBandpass(accZf, fs, 0.5, 3.0, 4);
            float[] accZvar = RollingVariance(accZbp, fs / 2);

            // 水平方向変動
            float[] accH = new float[n];
            for (int i = 0; i < n; i++)
                accH[i] = (float)Math.Sqrt(accXf[i] * accXf[i] + accYf[i] * accYf[i]);
            float[] accHvar = RollingVariance(accH, fs / 2);

            // ジャイロ変動
            float[] gyroMag = new float[n];
            for (int i = 0; i < n; i++)
                gyroMag[i] = (float)Math.Sqrt(gyroX[i] * gyroX[i] + gyroY[i] * gyroY[i] + gyroZ[i] * gyroZ[i]);
            float[] gyroVar = RollingVariance(gyroMag, fs / 2);

            // 包絡線（ピーク検出＋補間）
            float[] envelope = ComputeEnvelope(accYf, accZf, time);

            // FFT 周波数パワー
            float[] stepPower = ComputeStepFrequencyPower(accZbp, fs, 0.5f, 3.0f);

            // --- 3. 複数特徴量統合 ---
            float[] score = MultiFeatureScore(envelope, accZvar, accHvar, gyroVar, stepPower);

            // --- 4. 二値化＆ID割当 ---
            int[] binary = Binarize(score, 0.4f);
            int[] walkId = AssignWalkIds(binary);

            // --- 5. ProcessedRecordに反映 ---
            for (int i = 0; i < n; i++)
            {
                var r = records[i];
                r.WalkingScore = score[i];
                r.WalkingBinary = binary[i];
                r.WalkId = walkId[i];
                records[i] = r;
            }
        }

        // --- ユーティリティ群 ---

        private float[] RollingDeviation(float[] data, int window)
        {
            int n = data.Length;
            float[] result = new float[n];
            for (int i = 0; i < n; i++)
            {
                int s = Math.Max(0, i - window / 2);
                int e = Math.Min(n, i + window / 2);
                float mean = data.Skip(s).Take(e - s).Average();
                result[i] = Math.Abs(data[i] - mean);
            }
            return result;
        }

        private float[] RollingVariance(float[] data, int window)
        {
            int n = data.Length;
            float[] result = new float[n];
            for (int i = 0; i < n; i++)
            {
                int s = Math.Max(0, i - window / 2);
                int e = Math.Min(n, i + window / 2);
                var seg = data.Skip(s).Take(e - s);
                float m = seg.Average();
                float v = seg.Select(x => (x - m) * (x - m)).Average();
                result[i] = v;
            }
            return result;
        }

        private float[] ComputeEnvelope(float[] accY, float[] accZ, float[] time)
        {
            int n = accY.Length;
            float[] accMag2 = new float[n];
            for (int i = 0; i < n; i++)
                accMag2[i] = Math.Abs((float)Math.Sqrt(accY[i] * accY[i] + accZ[i] * accZ[i]) - 1.0f);

            // ピーク検出（簡易版）
            List<int> peaks = new List<int>();
            for (int i = 1; i < n - 1; i++)
            {
                if (accMag2[i] > accMag2[i - 1] && accMag2[i] > accMag2[i + 1] && accMag2[i] > 0.05f)
                    peaks.Add(i);
            }

            float[] env = new float[n];
            if (peaks.Count > 1)
            {
                // 線形補間
                for (int k = 0; k < peaks.Count - 1; k++)
                {
                    int i0 = peaks[k];
                    int i1 = peaks[k + 1];
                    for (int i = i0; i <= i1; i++)
                    {
                        float t = (time[i] - time[i0]) / (time[i1] - time[i0]);
                        env[i] = accMag2[i0] + t * (accMag2[i1] - accMag2[i0]);
                    }
                }
            }
            else
            {
                Array.Copy(accMag2, env, n);
            }
            return env;
        }

        private float[] ComputeStepFrequencyPower(float[] signal, int fs, float fLow, float fHigh)
        {
            int n = signal.Length;
            float[] power = new float[n];
            int win = fs * 2; // 2秒窓

            for (int i = 0; i < n; i++)
            {
                int s = Math.Max(0, i - win / 2);
                int e = Math.Min(n, i + win / 2);
                int len = e - s;
                if (len < win / 2) { power[i] = 0; continue; }

                float[] seg = new float[len];
                Array.Copy(signal, s, seg, 0, len);

                // FFT
                Complex[] fft = FFTUtils.FFT(seg);
                float df = fs / (float)len;

                float p = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    float freq = k * df;
                    if (freq >= fLow && freq <= fHigh)
                        p += fft[k].Magnitude * fft[k].Magnitude;
                }
                power[i] = p;
            }
            return power;
        }

        private float[] MultiFeatureScore(float[] env, float[] varZ, float[] varH, float[] gyroVar, float[] stepPower)
        {
            int n = env.Length;
            float[] score = new float[n];
            for (int i = 0; i < n; i++)
            {
                score[i] = 0.3f * (env[i] > 0.15f ? 1 : 0)
                         + 0.25f * (varZ[i] > 0.1f ? 1 : 0)
                         + 0.15f * (varH[i] > 0.05f ? 1 : 0)
                         + 0.1f * (gyroVar[i] > 0.1f ? 1 : 0)
                         + 0.2f * (stepPower[i] > 0.2f ? 1 : 0);
            }
            // 平滑化
            return RollingMean(score, fs);
        }

        private float[] RollingMean(float[] data, int window)
        {
            int n = data.Length;
            float[] result = new float[n];
            for (int i = 0; i < n; i++)
            {
                int s = Math.Max(0, i - window / 2);
                int e = Math.Min(n, i + window / 2);
                result[i] = data.Skip(s).Take(e - s).Average();
            }
            return result;
        }

        private int[] Binarize(float[] score, float th)
        {
            return score.Select(s => s > th ? 1 : 0).ToArray();
        }

        private int[] AssignWalkIds(int[] binary)
        {
            int n = binary.Length;
            int[] id = new int[n];
            int currentId = 0;
            for (int i = 1; i < n; i++)
            {
                if (binary[i] == 1 && binary[i - 1] == 0)
                    currentId++;
                id[i] = (binary[i] == 1) ? currentId : 0;
            }
            return id;
        }
    }

    // === 簡易 Complex 構造体 ===
    public struct Complex
    {
        public float Re, Im;
        public Complex(float r, float i) { Re = r; Im = i; }
        public float Magnitude { get { return (float)Math.Sqrt(Re * Re + Im * Im); } }
        public static Complex operator +(Complex a, Complex b) { return new Complex(a.Re + b.Re, a.Im + b.Im); }
        public static Complex operator -(Complex a, Complex b) { return new Complex(a.Re - b.Re, a.Im - b.Im); }
        public static Complex operator *(Complex a, Complex b) { return new Complex(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re); }
    }

    // === FFT ユーティリティ ===
    public static class FFTUtils
    {
        public static Complex[] FFT(float[] data)
        {
            int n = 1;
            while (n < data.Length) n <<= 1; // 2の累乗に拡張
            Complex[] a = new Complex[n];
            for (int i = 0; i < data.Length; i++) a[i] = new Complex(data[i], 0);
            for (int i = data.Length; i < n; i++) a[i] = new Complex(0, 0);
            FFTRecursive(a);
            return a;
        }

        private static void FFTRecursive(Complex[] a)
        {
            int n = a.Length;
            if (n <= 1) return;
            Complex[] even = new Complex[n / 2];
            Complex[] odd = new Complex[n / 2];
            for (int i = 0; i < n / 2; i++)
            {
                even[i] = a[2 * i];
                odd[i] = a[2 * i + 1];
            }
            FFTRecursive(even);
            FFTRecursive(odd);
            for (int k = 0; k < n / 2; k++)
            {
                double angle = -2.0 * Math.PI * k / n;
                Complex wk = new Complex((float)Math.Cos(angle), (float)Math.Sin(angle));
                a[k] = even[k] + wk * odd[k];
                a[k + n / 2] = even[k] - wk * odd[k];
            }
        }
    }
}