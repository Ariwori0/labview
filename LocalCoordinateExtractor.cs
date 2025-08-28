using System;
using System.Collections.Generic;
using CoreLinkSys1.AHRS;

namespace CoreLinkSys1.Analysis
{
    /// <summary>
    /// Local座標系から Global水平加速度と推進力を抽出するクラス
    /// Python版 LocalCoordinateExtractor の C# 移植
    /// </summary>
    public class LocalCoordinateExtractor
    {
        private float sampleRate;
        private float beta;
        private MadgwickAHRS ahrs;

        // フィルタ設定
        private int lpfOrder = 4;
        private int hpfOrder = 4;
        private int bpfOrder = 4;

        private double lpfCutoff = 20.0;   // Hz
        private double hpfCutoff = 0.1;    // Hz
        private double bandLow = 0.2;      // Hz
        private double bandHigh = 8.0;     // Hz

        private float[] fixedForward = null;

        public LocalCoordinateExtractor(float sampleRate, float beta)
        {
            this.sampleRate = sampleRate;
            this.beta = beta;
            this.ahrs = new MadgwickAHRS(1f / sampleRate, beta);
        }
        /// <summary>
        /// 外部のAHRSインスタンスを受け取るコンストラクタ
        /// </summary>
        public LocalCoordinateExtractor(float sampleRate, float beta, MadgwickAHRS externalAhrs)
        {
            this.sampleRate = sampleRate;
            this.beta = beta;
            this.ahrs = externalAhrs; // 外部から受け取ったインスタンスを使う
        }

        /// <summary>
        /// サンプル処理の結果を保持するクラス
        /// </summary>
        public class ProcessResult
        {
            public float[] LinearAcc;     // 重力除去後の線形加速度
            public float[] GlobalAcc;     // Global座標系での加速度
            public float[] GlobalHoriz;   // Global座標系での水平加速度ベクトル
            public float ForwardAcc;      // 推進方向成分
            public float[] Quaternion;    // 最新のクォータニオン
        }

        /// <summary>
        /// 1サンプル処理: 重力除去 → Global変換 → 水平成分 → 推進力
        /// </summary>
        public ProcessResult ProcessSample(float gx, float gy, float gz,
                                           float ax, float ay, float az)
        {
            // 1. 姿勢更新
            ahrs.Update(gx, gy, gz, ax, ay, az);

            // 2. 線形加速度（重力除去）
            float[] linearAcc = MadgwickAHRS.CalculateLinearAcceleration(
                ax, ay, az,
                ahrs.Quaternion[0], ahrs.Quaternion[1],
                ahrs.Quaternion[2], ahrs.Quaternion[3]);

            // 3. Global変換
            float[] globalAcc = MadgwickAHRS.ConvertToGlobalFrame(
                linearAcc[0], linearAcc[1], linearAcc[2],
                ahrs.Quaternion[0], ahrs.Quaternion[1],
                ahrs.Quaternion[2], ahrs.Quaternion[3]);

            // 4. 水平成分 (Z=0)
            float[] globalHoriz = new float[3];
            globalHoriz[0] = globalAcc[0];
            globalHoriz[1] = globalAcc[1];
            globalHoriz[2] = globalAcc[2];

            // 5. Euler角（yawを取得）
            float roll, pitch, yaw;
            QuaternionToEuler(out roll, out pitch, out yaw,
                ahrs.Quaternion[0], ahrs.Quaternion[1],
                ahrs.Quaternion[2], ahrs.Quaternion[3]);


            // 6. 推進方向ベクトル（yawから計算）
            float[] forward;
            if (fixedForward != null)
            {
                forward = fixedForward;  // 固定ベクトルを使用
            }
            else
            {
                // 従来通り yaw から毎サンプル算出
                forward = new float[] {
                    (float)Math.Cos(yaw),
                    (float)Math.Sin(yaw),
                    0f
                };
            }

            // 7. 内積で forward 成分（推進力）を抽出
            float forwardAcc = globalHoriz[0] * forward[0] + globalHoriz[1] * forward[1];

            // 8. 結果を返す
            return new ProcessResult
            {
                LinearAcc = linearAcc,
                GlobalAcc = globalAcc,
                GlobalHoriz = globalHoriz,
                ForwardAcc = forwardAcc,
                Quaternion = new float[] {
                    ahrs.Quaternion[0], ahrs.Quaternion[1],
                    ahrs.Quaternion[2], ahrs.Quaternion[3]
                }

            };
        }

        /// <summary>
        /// Pythonの preprocess_data 相当:
        /// 加速度・ジャイロをフィルタリング
        /// </summary>
        public void PreprocessData(ref float[] accX, ref float[] accY, ref float[] accZ,
                                   ref float[] gyroX, ref float[] gyroY, ref float[] gyroZ)
        {
            // LPF（20Hz以下の動きを残す）
            accX = FilterUtils.ButterworthLowpass(accX, (int)sampleRate, lpfCutoff, lpfOrder);
            accY = FilterUtils.ButterworthLowpass(accY, (int)sampleRate, lpfCutoff, lpfOrder);
            accZ = FilterUtils.ButterworthLowpass(accZ, (int)sampleRate, lpfCutoff, lpfOrder);

            gyroX = FilterUtils.ButterworthLowpass(gyroX, (int)sampleRate, lpfCutoff, lpfOrder);
            gyroY = FilterUtils.ButterworthLowpass(gyroY, (int)sampleRate, lpfCutoff, lpfOrder);
            gyroZ = FilterUtils.ButterworthLowpass(gyroZ, (int)sampleRate, lpfCutoff, lpfOrder);

            // HPF（0.1Hz以下のドリフトを除去）
            accX = FilterUtils.ButterworthHighpass(accX, (int)sampleRate, hpfCutoff, hpfOrder);
            accY = FilterUtils.ButterworthHighpass(accY, (int)sampleRate, hpfCutoff, hpfOrder);
            accZ = FilterUtils.ButterworthHighpass(accZ, (int)sampleRate, hpfCutoff, hpfOrder);

            // BPF（歩行の周波数帯 0.2–8 Hz）
            accX = FilterUtils.ButterworthBandpass(accX, (int)sampleRate, bandLow, bandHigh, bpfOrder);
            accY = FilterUtils.ButterworthBandpass(accY, (int)sampleRate, bandLow, bandHigh, bpfOrder);
            accZ = FilterUtils.ButterworthBandpass(accZ, (int)sampleRate, bandLow, bandHigh, bpfOrder);
        }

        /// <summary>
        /// クォータニオンをオイラー角 (rad) に変換
        /// </summary>
        private void QuaternionToEuler(out float roll, out float pitch, out float yaw,
            float q0, float q1, float q2, float q3)
        {
            roll = (float)Math.Atan2(2f * (q0 * q1 + q2 * q3),
                                      1f - 2f * (q1 * q1 + q2 * q2));
            pitch = (float)Math.Asin(2f * (q0 * q2 - q3 * q1));
            yaw = (float)Math.Atan2(2f * (q0 * q3 + q1 * q2),
                                     1f - 2f * (q2 * q2 + q3 * q3));
        }

        public void SetFixedForward(float dx, float dy)
        {
            float norm = (float)Math.Sqrt(dx * dx + dy * dy);
            fixedForward = new float[] { dx / norm, dy / norm, 0f };
        }
    }
}
