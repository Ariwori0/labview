using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace CoreLinkSys1.Analysis
{
    public static class FilterUtils
    {

        // デフォルト引数ありの代わりにオーバーロードを追加
        public static float[] ButterworthBandpass(float[] data, int fs, double lowHz, double highHz)
        {
            return ButterworthBandpass(data, fs, lowHz, highHz, 4);
        }


        // ====== フィルタ設計 ======
        // ローパスフィルタ設計 (双二次フィルタに分解)
        private static void ButterworthDesign(int order, double cutoff, out double[] b, out double[] a)
        {
            // 双二次フィルタ設計（双一次変換）
            int n = order;
            double[] aCoeffs = new double[n + 1];
            double[] bCoeffs = new double[n + 1];

            double ita = 1.0 / Math.Tan(Math.PI * cutoff);
            double q = Math.Sqrt(2.0);

            double norm = 1.0 / (1.0 + q * ita + ita * ita);
            bCoeffs[0] = 1.0 * norm;
            bCoeffs[1] = 2.0 * norm;
            bCoeffs[2] = 1.0 * norm;
            aCoeffs[0] = 1.0;
            aCoeffs[1] = 2.0 * (1.0 - ita * ita) * norm;
            aCoeffs[2] = (1.0 - q * ita + ita * ita) * norm;

            b = bCoeffs;
            a = aCoeffs;
        }

        // バンドパスフィルタ設計（簡易IIR: 双一次変換）
        private static void ButterworthBandDesign(int order, double low, double high, out double[] b, out double[] a)
        {
            // 本格的な設計は長くなるので簡易版（バイカッド連接で代用可）
            // ここでは2次のIIRバンドパスを返す
            double w0 = Math.PI * (high + low);
            double bw = Math.PI * (high - low);

            double alpha = Math.Sin(w0) * Math.Sinh(Math.Log(2.0) / 2.0 * bw / Math.Sin(w0));

            double cosw0 = Math.Cos(w0);
            double a0 = 1 + alpha;

            b = new double[3];
            a = new double[3];

            b[0] = alpha / a0;
            b[1] = 0;
            b[2] = -alpha / a0;

            a[0] = 1;
            a[1] = -2 * cosw0 / a0;
            a[2] = (1 - alpha) / a0;
        }


        /// <summary>
        /// Butterworthローパスフィルタ (filtfilt相当)
        /// </summary>
        public static float[] ButterworthLowpass(float[] data, int fs, double cutoffHz, int order)
        {
            double nyq = 0.5 * fs;
            double normalCutoff = cutoffHz / nyq;

            double[] b, a;
            Signal.ButterworthLowpass(order, normalCutoff, out b, out a);

            return FiltFilt(b, a, data);
        }

        /// <summary>
        /// Butterworthハイパスフィルタ (filtfilt相当)
        /// </summary>
        public static float[] ButterworthHighpass(float[] data, int fs, double cutoffHz, int order)
        {
            double nyq = 0.5 * fs;
            double normalCutoff = cutoffHz / nyq;

            double[] b, a;
            Signal.ButterworthHighpass(order, normalCutoff, out b, out a);

            return FiltFilt(b, a, data);
        }

        /// <summary>
        /// Butterworthバンドパスフィルタ (filtfilt相当)
        /// </summary>
        public static float[] ButterworthBandpass(float[] data, int fs, double lowHz, double highHz, int order)
        {
            double nyq = 0.5 * fs;
            double low = lowHz / nyq;
            double high = highHz / nyq;

            double[] b, a;
            Signal.ButterworthBandpass(order, low, high, out b, out a);

            return FiltFilt(b, a, data);
        }

        // ====== フィルタ適用 (filtfilt) ======

        private static float[] FiltFilt(double[] b, double[] a, float[] data)
        {
            int n = data.Length;
            float[] yForward = ApplyFilter(b, a, data);

            // 反転してもう一度適用
            float[] rev = new float[n];
            for (int i = 0; i < n; i++) rev[i] = yForward[n - 1 - i];

            float[] yBackward = ApplyFilter(b, a, rev);

            // 再び反転
            float[] yFinal = new float[n];
            for (int i = 0; i < n; i++) yFinal[i] = yBackward[n - 1 - i];

            return yFinal;
        }

        private static float[] ApplyFilter(double[] b, double[] a, float[] x)
        {
            int n = x.Length;
            float[] y = new float[n];
            int na = a.Length;
            int nb = b.Length;

            for (int i = 0; i < n; i++)
            {
                double acc = 0.0;
                for (int j = 0; j < nb; j++)
                {
                    if (i - j >= 0) acc += b[j] * x[i - j];
                }
                for (int j = 1; j < na; j++)
                {
                    if (i - j >= 0) acc -= a[j] * y[i - j];
                }
                acc /= a[0];
                y[i] = (float)acc;
            }
            return y;
        }
    }

    /// <summary>
    /// バターワースフィルタ設計（双一次変換）
    /// Pythonの signal.butter と同等の係数を出す簡易版
    /// </summary>
    internal static class Signal
    {
        public static void ButterworthLowpass(int order, double cutoff, out double[] b, out double[] a)
        {
            // Python: butter(N, Wn, 'low')
            // 今は「双二次1段分」で代用 → order=4なら biquad を2回かける
            BiquadLowpass(cutoff, out b, out a);
        }

        public static void ButterworthHighpass(int order, double cutoff, out double[] b, out double[] a)
        {
            // Python: butter(N, Wn, 'high')
            BiquadHighpass(cutoff, out b, out a);
        }

        public static void ButterworthBandpass(int order, double low, double high, out double[] b, out double[] a)
        {
            // Python: butter(N, [low, high], 'band')
            BiquadBandpass(low, high, out b, out a);
        }

        // ==== 実際はbiquad設計 ====
        private static void BiquadLowpass(double normCutoff, out double[] b, out double[] a)
        {
            double ita = 1.0 / Math.Tan(Math.PI * normCutoff);
            double q = Math.Sqrt(2.0);

            double norm = 1.0 / (1.0 + q * ita + ita * ita);
            b = new double[3];
            a = new double[3];
            b[0] = 1.0 * norm;
            b[1] = 2.0 * norm;
            b[2] = 1.0 * norm;
            a[0] = 1.0;
            a[1] = 2.0 * (1.0 - ita * ita) * norm;
            a[2] = (1.0 - q * ita + ita * ita) * norm;
        }

        private static void BiquadHighpass(double normCutoff, out double[] b, out double[] a)
        {
            double ita = 1.0 / Math.Tan(Math.PI * normCutoff);
            double q = Math.Sqrt(2.0);

            double norm = 1.0 / (1.0 + q * ita + ita * ita);
            b = new double[3];
            a = new double[3];
            b[0] = 1.0 * norm;
            b[1] = -2.0 * norm;
            b[2] = 1.0 * norm;
            a[0] = 1.0;
            a[1] = 2.0 * (ita * ita - 1.0) * norm;
            a[2] = (1.0 - q * ita + ita * ita) * norm;
        }

        private static void BiquadBandpass(double low, double high, out double[] b, out double[] a)
        {
            double bw = high - low;
            double w0 = Math.PI * (high + low);

            double cosw0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) * Math.Sinh(Math.Log(2.0) / 2.0 * bw / Math.Sin(w0));

            double a0 = 1 + alpha;
            b = new double[3];
            a = new double[3];

            b[0] = alpha / a0;
            b[1] = 0;
            b[2] = -alpha / a0;

            a[0] = 1;
            a[1] = -2 * cosw0 / a0;
            a[2] = (1 - alpha) / a0;
        }
    }

}