using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoreLinkSys1.Analysis;
using CoreLinkSys1.Utilities;


namespace CoreLinkSys1.Analysis
{

    public class BaysianSurpriseKalmanFilter
    {
        public static void PrintShape(string name, float[,] mat)
        {
            Console.WriteLine(String.Format("{0}: {1}x{2}", name, mat.GetLength(0), mat.GetLength(1)));
        }

        //定数
        private const int StateDim = 12;

        private const int ObsDim = 6;

        private float Gravity;

        //ベクトル・行列
        private float[] _x;
        private float[] _x_;  // Pythonコードのx_に対応
        private float[,] _F;
        private float[,] _H;
        private float[,] _M;
        private float[,] _E;
        private float[,] _Q;
        private float[,] _R;
        private float[,] _P;
        private float[,] _P_prev;

        private float[,] _M_in;

        private float[] _x_prev;

        private readonly float _dt;
        private readonly float _k1;
        private readonly float _k2;
        private float _sigmaOmega = 0.7f;
        private float _sigmaA;
        private float _sigmaAz;
        private float _sigmaG;
        private float _sigmaV = 0.175f;
        private float _sigmaF;

        //履歴
        private readonly List<float> _surpriseHistory = new List<float>();
        private readonly List<float[]> _stateHistory = new List<float[]>();

        // 前庭モデル関係
        private float _taud = 7.0f;
        private float _taua = 190.0f;
        private float _tc = 4.0f;
        private VestibularModel _vestibularModel;

        private readonly int _meanRange;

        public BaysianSurpriseKalmanFilter(float dt, int meanRange)
        {
            _dt = dt;
            _meanRange = meanRange;
            _k1 = _tc / (_dt + _tc);
            _k2 = _dt / (_dt + _tc);
            _vestibularModel = new VestibularModel(_taud, _taua, _dt);
        }

        public void Initialize(float[,] signal)
        {
            Console.WriteLine("Initialize started");

            // 姿勢角推定(初期化時)
            // sensor_posture_data = np.mean(Signal[mean_range_, 3:6], axis=0)
            float[] sensor_posture_data = new float[3];
            for (int i = 0; i < _meanRange && i < signal.GetLength(0); i++)
            {
                sensor_posture_data[0] += signal[i, 3]; // ax
                sensor_posture_data[1] += signal[i, 4]; // ay
                sensor_posture_data[2] += signal[i, 5]; // az
            }
            int count = Math.Min(_meanRange, signal.GetLength(0));
            sensor_posture_data[0] /= count;
            sensor_posture_data[1] /= count;
            sensor_posture_data[2] /= count;

            // 姿勢角推定
            float roll_pre = (float)Math.Atan2(sensor_posture_data[1], sensor_posture_data[2]);
            float pitch_pre = (float)Math.Atan2(-sensor_posture_data[0],
                (float)Math.Sqrt(sensor_posture_data[1] * sensor_posture_data[1] + sensor_posture_data[2] * sensor_posture_data[2]));

            // Pythonコードと同じく回転行列を使用した重力成分計算
            float cs_p = (float)Math.Cos(pitch_pre);
            float sn_p = (float)Math.Sin(pitch_pre);
            float cs_r = (float)Math.Cos(roll_pre);
            float sn_r = (float)Math.Sin(roll_pre);

            // 回転行列の計算（Pythonコードと同じ）
            // rotM = [[cs_p, 0, sn_p], [0, 1, 0], [-sn_p, 0, cs_p]] @ [[1, 0, 0], [0, cs_r, -sn_r], [0, sn_r, cs_r]]
            float[,] rotM_pitch = new float[3, 3] {
                {cs_p, 0, sn_p},
                {0, 1, 0},
                {-sn_p, 0, cs_p}
            };
            float[,] rotM_roll = new float[3, 3] {
                {1, 0, 0},
                {0, cs_r, -sn_r},
                {0, sn_r, cs_r}
            };

            // 回転行列の積を計算
            float[,] rotM = MatrixUtils.Multiply(rotM_pitch, rotM_roll);

            // 重力方向ベクトルの計算 G = rotM @ sensor_posture_data
            float[] G = MatrixUtils.MatrixVectorMultiply(rotM, sensor_posture_data);

            // 重力加速度 g = G[2]
            float g = G[2];
            Gravity = g;

            // ノイズパラメータもここで初期化
            _sigmaA = 0.3f * Gravity;
            _sigmaAz = 0.3f * Gravity;
            _sigmaG = 1e-4f * Gravity;
            _sigmaF = 0.002f * Gravity;

            // 初期状態ベクトル（Pythonコードと同じ計算）
            _x = new float[StateDim];
            _x[6] = g * (float)Math.Sin(-pitch_pre);  // X(7,1) = g*sin(theta)
            _x[7] = g * (float)Math.Sin(roll_pre);    // X(8,1) = g*sin(theta2)
            _x[8] = g * (float)Math.Cos(-pitch_pre);  // X(9,1) = g*cos(theta)
            _x_ = new float[StateDim];  // x_の初期化
            _x_[6] = _x[6]; _x_[7] = _x[7]; _x_[8] = _x[8];  // 重力成分をコピー
            _x_prev = (float[])_x.Clone();  // x_prevの初期化
            //DebugTool.DebugLog(String.Format("(初期)状態ベクトル_x {0}", DebugTool.ArrayToString(_x, ",", 12)));

            // 状態遷移行列 F
            _F = MatrixUtils.Identity(StateDim);
            for (int i = 3; i < 6; i++) _F[i, i] = _k1;
            for (int i = 0; i < 3; i++) _F[i, i] = 0;
            for (int i = 9; i < 12; i++) _F[i, i] = 0;
            //DebugTool.InfoLog($"F行列のk1値: {_k1}");
            //DebugTool.DebugLog(String.Format("(初期)状態遷移行列_F {0}", DebugTool.MatrixToString(_F, 12, 12)));

            // 観測行列 H
            _H = new float[ObsDim, StateDim];
            for (int i = 0; i < 3; i++)
            {
                _H[i, i] = 1;       // 角速度
                _H[i, i + 3] = -1;    // バイアス
                _H[i + 3, i + 6] = 1;   // 重力
                _H[i + 3, i + 9] = 1;   // 加速度
            }
            //DebugTool.DebugLog(String.Format("(初期)観測行列_H {0}", DebugTool.MatrixToString(_H, 6, 12)));

            // 制御入力行列 M
            _M = new float[StateDim, 7];
            for (int i = 0; i < 3; i++)
            {
                _M[i, i] = 1;
                _M[i + 3, i] = _k2;
            }
            _M[9, 3] = 1; _M[10, 4] = 1; _M[11, 5] = 1;  // 加速度部分
            UpdateM();  // 初期化時にM行列を更新
            _M_in = (float[,])_M.Clone();
            //DebugTool.InfoLog($"M行列のk2値: {_k2}");
            //DebugTool.DebugLog(String.Format("(初期)制御入力行列_M {0}", DebugTool.MatrixToString(_M, 12, 7)));

            // プロセスノイズ共分散 Q
            float[,] Q_tmp = new float[7, 7];
            for (int i = 0; i < 3; i++) Q_tmp[i, i] = _sigmaOmega * _sigmaOmega;
            for (int i = 3; i < 5; i++) Q_tmp[i, i] = _sigmaA * _sigmaA;
            Q_tmp[5, 5] = _sigmaAz * _sigmaAz;
            Q_tmp[6, 6] = _sigmaG * _sigmaG;

            //DebugTool.InfoLog(string.Format("ノイズパラメータ: sigmaOmega={0}, sigmaA={1}, sigmaAz={2}, sigmaG={3}", _sigmaOmega, _sigmaA, _sigmaAz, _sigmaG));
            //DebugTool.DebugLog(String.Format("(初期)ノイズ共分散Q_tmp {0}", DebugTool.MatrixToString(Q_tmp, 7, 7)));

            _E = (float[,])_M.Clone();
            _E[8, 6] = 1;
            //DebugTool.InfoLog($"E行列の(8,6)要素: {_E[8, 6]}");
            //Console.WriteLine("Multiply 1");
            //PrintShape("_E", _E);
            //PrintShape("_Q", Q_tmp);
            //DebugTool.StartPerformanceMeasurement("_Q = _E * Q_tmp * _E.T");
            _Q = MatrixUtils.Multiply(MatrixUtils.Multiply(_E, Q_tmp), MatrixUtils.Transpose(_E));
            //DebugTool.DebugLog(String.Format("(初期)プロセスノイズ共分散_Q {0}", DebugTool.MatrixToString(_Q, 12, 12)));
            // _Q = _E * Q_tmp * _E.T; 
            //DebugTool.EndPerformanceMeasurement(" _Q = _E * Q_tmp * _E.T");

            // 観測ノイズ共分散 R
            _R = new float[ObsDim, ObsDim];
            for (int i = 0; i < 3; i++) _R[i, i] = _sigmaV * _sigmaV;
            for (int i = 3; i < 6; i++) _R[i, i] = _sigmaF * _sigmaF;
            //DebugTool.DebugLog(String.Format("(初期)観測ノイズ共分散_R {0}", DebugTool.MatrixToString(_R, 6, 6)));

            // 初期共分散
            _P = InitializeCovariance();
            //DebugTool.DebugLog(String.Format("(初期)誤差共分散_P {0}", DebugTool.MatrixToString(_P, 12, 12)));
            _P_prev = (float[,])_P.Clone();

            //Console.WriteLine("Initialize completed");
        }

        // 誤差共分散の初期化
        private float[,] InitializeCovariance()
        {
            DebugTool.InfoLog("=== InitializeCovariance開始 ===");
            //DebugTool.InfoLog(string.Format("初期Q行列: {0}", DebugTool.MatrixToString(_Q, ",", 12, 12)));

            float[,] L = (float[,])_Q.Clone();
            for (int iter = 0; iter < 100; iter++)
            {
                // if (iter < 3) // 最初の3回の反復をログ出力
                // {
                //     DebugTool.InfoLog($"反復 {iter}: L行列: {DebugTool.MatrixToString(L, ",", 12, 12)}");
                // }

                float[,] L_pri = MatrixUtils.Add(
                    MatrixUtils.Multiply(MatrixUtils.Multiply(_F, L), MatrixUtils.Transpose(_F)),
                     _Q); // L_pri = self.F @ L @ self.F.T + self.Q

                // if (iter < 3)
                // {
                //     DebugTool.InfoLog($"反復 {iter}: L_pri行列: {DebugTool.MatrixToString(L_pri, ",", 12, 12)}");

                // }

                float[,] K = MatrixUtils.Multiply(
                    MatrixUtils.Multiply(L_pri, MatrixUtils.Transpose(_H)),
                    MatrixUtils.Inverse(MatrixUtils.Add(
                        MatrixUtils.Multiply(MatrixUtils.Multiply(_H, L_pri), MatrixUtils.Transpose(_H)),
                        _R))); //K = (L_pri @ self.H.T) @ inv(((self.H @ L_pri) @ self.H.T) + self.R)

                // if (iter < 3)
                // {
                //     DebugTool.InfoLog($"反復 {iter}: K行列: {DebugTool.MatrixToString(K, ",", 12, 6)}");
                // }

                float[,] I = MatrixUtils.Identity(StateDim);
                L = MatrixUtils.Multiply(
                    MatrixUtils.Subtract(I, MatrixUtils.Multiply(K, _H)),
                    L_pri); //L = (np.eye(self.state_dim) - K @ self.H) @ L_pri

                // if (iter < 3)
                // {
                //     DebugTool.InfoLog($"反復 {iter}: 更新後L行列: {DebugTool.MatrixToString(L, ",", 12, 12)}");
                // }
            }

            DebugTool.InfoLog(string.Format("最終L行列: {0}", DebugTool.MatrixToString(L, ",", 12, 12)));
            DebugTool.InfoLog("=== InitializeCovariance終了 ===");
            return L;
        }

        private void UpdateM()
        {
            // Pythonコードのnextstepメソッドに合わせて、M行列のコピーを作成してから更新
            float[,] M_copy = (float[,])_M.Clone();

            // 動的に更新されるM行列の要素（x_を使用）
            M_copy[6, 1] = -_x_[8] * _dt;
            M_copy[6, 2] = _x_[7] * _dt;
            M_copy[7, 0] = _x_[8] * _dt;
            M_copy[7, 2] = -_x_[6] * _dt;
            M_copy[8, 0] = -_x_[7] * _dt;
            M_copy[8, 1] = _x_[6] * _dt;

            _M = M_copy;
        }

        //main
        public float ProcessSample(float time, float gx, float gy, float gz, float ax, float ay, float az)
        {
            DebugTool.DebugLog(String.Format("gyroacc {0}", DebugTool.ArrayToString(new float[] { gx, gy, gz, ax, ay, az })));

            // 更新前の状態を保存（ベイジアンサプライズ計算用）
            _x_prev = (float[])_x.Clone();

            // 前庭モデル出力の計算
            DebugTool.StartPerformanceMeasurement("前庭モデル出力");
            double[] vestibularOutput = _vestibularModel.Update(new double[] { gx, gy, gz });
            DebugTool.EndPerformanceMeasurement("前庭モデル出力");
            //DebugTool.DebugLog(String.Format("前庭モデル {0}", DebugTool.ArrayToString(vestibularOutput)));

            // 観測ベクトル
            DebugTool.InfoLog(string.Format("前庭モデル出力: {0}", DebugTool.ArrayToString(vestibularOutput, ",", 12)));

            float[] z = new float[ObsDim];
            for (int i = 0; i < 3; i++)
            {
                z[i] = (float)vestibularOutput[i];
            }
            z[3] = ax; z[4] = ay; z[5] = az;

            // X_Eの作成（Pythonコードに合わせる）
            float[] XE = new float[7];
            XE[0] = gx; XE[1] = gy; XE[2] = gz;
            XE[3] = ax; XE[4] = ay; XE[5] = az;
            XE[6] = 0.0f;

            //DebugTool.DebugLog(String.Format("XE {0}", DebugTool.ArrayToString(XE)));
            //DebugTool.DebugLog(String.Format("XS {0}", DebugTool.ArrayToString(z)));

            // 予測前の状態を記録
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 予測前の状態ベクトル: {1}", time, DebugTool.ArrayToString(_x, ",", 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 予測前の共分散行列: {1}", time, DebugTool.MatrixToString(_P, ",", 12, 12)));

            // 予測ステップ
            DebugTool.StartPerformanceMeasurement("予測ステップ");
            Predict(XE);
            DebugTool.EndPerformanceMeasurement("予測ステップ");

            // 予測後の状態を記録
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 予測後の状態ベクトル: {1}", time, DebugTool.ArrayToString(_x, ",", 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 予測後の共分散行列: {1}", time, DebugTool.MatrixToString(_P, ",", 12, 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 予測後のx_ベクトル: {1}", time, DebugTool.ArrayToString(_x_, ",", 12)));

            // 更新ステップ
            DebugTool.StartPerformanceMeasurement("更新ステップ");
            Update(z);
            DebugTool.EndPerformanceMeasurement("更新ステップ");

            // 更新後の状態を記録
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 更新後の状態ベクトル: {1}", time, DebugTool.ArrayToString(_x, ",", 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] 更新後の共分散行列: {1}", time, DebugTool.MatrixToString(_P, ",", 12, 12)));

            DebugTool.StartPerformanceMeasurement("ベイジアンサプライズ計算");
            //DebugTool.InfoLog(string.Format("[{0:F3}s] (BS)更新前の状態ベクトル: {1}", time, DebugTool.ArrayToString(_x_prev, ",", 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] (BS)更新前の共分散行列: {1}", time, DebugTool.MatrixToString(_P_prev, ",", 12, 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] (BS)更新後の状態ベクトル: {1}", time, DebugTool.ArrayToString(_x, ",", 12)));
            //DebugTool.InfoLog(string.Format("[{0:F3}s] (BS)更新後の共分散行列: {1}", time, DebugTool.MatrixToString(_P, ",", 12, 12)));


            float surprise = ComputeBayesianSurprise();
            DebugTool.EndPerformanceMeasurement("ベイジアンサプライズ計算");

            DebugTool.DebugLog("前庭モデル出力: " + DebugTool.GetPerformanceStats("前庭モデル出力"));
            DebugTool.DebugLog("予測ステップ: " + DebugTool.GetPerformanceStats("予測ステップ"));
            DebugTool.DebugLog("更新ステップ: " + DebugTool.GetPerformanceStats("更新ステップ"));
            DebugTool.DebugLog("ベイジアンサプライズ計算: " + DebugTool.GetPerformanceStats("ベイジアンサプライズ計算"));

            _surpriseHistory.Add(surprise);

            return surprise;
        }

        private void Predict(float[] XE)
        {
            //DebugTool.DebugLog(String.Format("予測前状態 : {0}", DebugTool.ArrayToString(XE)));

            // Pythonコードに合わせて予測ステップを修正
            // self.x_ = self.F @ self.x + self.M @ np.zeros(7) + self.E @ self.X_E
            float[] zeros = new float[7];
            float[] Fx = MatrixUtils.VectorMatrixMultiply(_x, _F);
            float[] Mu = MatrixUtils.MatrixVectorMultiply(_M, zeros);
            float[] EXE = MatrixUtils.MatrixVectorMultiply(_E, XE);

            //DebugTool.InfoLog(string.Format("予測計算 - Fx: {0}", DebugTool.ArrayToString(Fx, ",", 12)));
            //DebugTool.InfoLog(string.Format("予測計算 - Mu: {0}", DebugTool.ArrayToString(Mu, ",", 12)));
            //DebugTool.InfoLog(string.Format("予測計算 - EXE: {0}", DebugTool.ArrayToString(EXE, ",", 12)));


            // x_の計算（Pythonコードのx_に対応）
            float[] x_ = new float[Fx.Length];
            for (int i = 0; i < x_.Length; i++)
            {
                x_[i] = Fx[i] + Mu[i] + EXE[i];
            }
            _x_ = x_;  // x_を保存

            // self.x = self.F @ self.x + self.M_in @ np.zeros(7)
            float[] M_in_zeros = MatrixUtils.MatrixVectorMultiply(_M_in, zeros);
            for (int i = 0; i < _x.Length; i++)
            {
                _x[i] = Fx[i] + M_in_zeros[i];
            }

            //DebugTool.InfoLog(string.Format("予測計算 - M_in_zeros: {0}", DebugTool.ArrayToString(M_in_zeros, ",", 12)));


            // 共分散予測 (self.P = self.F @ self.P @ self.F.T + self.Q)
            _P_prev = (float[,])_P.Clone();
            _P = MatrixUtils.Add(
                MatrixUtils.Multiply(MatrixUtils.Multiply(_F, _P), MatrixUtils.Transpose(_F)),
                _Q);

            //高速化
            //_P = MatrixUtils.Add(
            //    MatrixUtils.Multiply(MatrixUtils.Multiply12x12(_F, _P), MatrixUtils.Transpose(_F)),
            //    _Q);

            //Debug用書き下し
            //float[,] FP = MatrixUtils.Multiply(_F, _P);
            //float[,] FPFt = MatrixUtils.Multiply(FP, MatrixUtils.Transpose(_F));
            //_P = MatrixUtils.Add(FPFt, _Q);
            //DebugTool.InfoLog(string.Format("予測計算 - FP: {0}", DebugTool.MatrixToString(FP, ",", 12, 12)));
            //DebugTool.InfoLog(string.Format("予測計算 - FPFt: {0}", DebugTool.MatrixToString(FPFt, ",", 12, 12)));
            //DebugTool.InfoLog(string.Format("予測計算 - Q: {0}", DebugTool.MatrixToString(_Q, ",", 12, 12)));

            //DebugTool.DebugLog(String.Format("Predictの_P {0}", DebugTool.MatrixToString(_P, 12, 12)));
        }

        private void Update(float[] z)
        {
            //DebugTool.DebugLog(String.Format("観測ベクトルz : {0}", DebugTool.ArrayToString(z)));

            // カルマンゲイン計算
            float[,] K = MatrixUtils.Multiply(
                MatrixUtils.Multiply(_P, MatrixUtils.Transpose(_H)),
                MatrixUtils.Inverse(MatrixUtils.Add(
                    MatrixUtils.Multiply(MatrixUtils.Multiply(_H, _P), MatrixUtils.Transpose(_H)),
                    _R)));

            // Debug用書き下し
            //float[,] Ht = MatrixUtils.Transpose(_H); 
            //float[,] HP = MatrixUtils.Multiply(_H, _P);
            //float[,] HPHt = MatrixUtils.Multiply(HP, Ht);
            //float[,] HPHt_plus_R = MatrixUtils.Add(HPHt, _R);
            //float[,] HPHt_plus_R_inv = MatrixUtils.Inverse(HPHt_plus_R);
            //float[,] PHt = MatrixUtils.Multiply(_P, Ht);
            //float[,] K = MatrixUtils.Multiply(PHt, HPHt_plus_R_inv);

            //DebugTool.InfoLog(string.Format("更新計算 - Ht: {0}", DebugTool.MatrixToString(Ht, ",", 12, 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - HP: {0}", DebugTool.MatrixToString(HP, ",", 6, 12)));
            //DebugTool.InfoLog(string.Format("更新計算 - HPHt: {0}", DebugTool.MatrixToString(HPHt, ",", 6, 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - R: {0}", DebugTool.MatrixToString(_R, ",", 6, 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - HPHt_plus_R: {0}", DebugTool.MatrixToString(HPHt_plus_R, ",", 6, 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - HPHt_plus_R_inv: {0}", DebugTool.MatrixToString(HPHt_plus_R_inv, ",", 6, 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - PHt: {0}", DebugTool.MatrixToString(PHt, ",", 12, 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - カルマンゲインK: {0}", DebugTool.MatrixToString(K, ",", 12, 6)));
            //DebugTool.DebugLog(string.Format("UpdateのK {0}", DebugTool.MatrixToString(K, 12, 6)));

            // 状態更新 error = self.Z - self.H @ self.x , self.x = self.x + K @ error
            float[] Hx = MatrixUtils.MatrixVectorMultiply(_H, _x);  // H[6,12] × x[12] → Hx[6]
            float[] error = new float[z.Length];
            for (int i = 0; i < z.Length; i++)
                error[i] = z[i] - Hx[i];                             // z - Hx
            float[] correction = MatrixUtils.VectorMatrixMultiply(error, MatrixUtils.Transpose(K));  // K @ error

            //DebugTool.InfoLog(string.Format("更新計算 - Z: {0}", DebugTool.ArrayToString(z, ",", 12)));
            //DebugTool.InfoLog(string.Format("更新計算 - x: {0}", DebugTool.ArrayToString(_x, ",", 12)));
            //DebugTool.InfoLog(string.Format("更新計算 - H: {0}", DebugTool.MatrixToString(_H, ",", 6, 12)));
            //DebugTool.InfoLog(string.Format("更新計算 - Hx: {0}", DebugTool.ArrayToString(Hx, ",", 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - error (z - Hx): {0}", DebugTool.ArrayToString(error, ",", 6)));
            //DebugTool.InfoLog(string.Format("更新計算 - correction (K @ error): {0}", DebugTool.ArrayToString(correction, ",", 12)));

            for (int i = 0; i < _x.Length; i++)
                _x[i] += correction[i];                             // x = x + correction

            // 共分散更新
            _P = MatrixUtils.Multiply(MatrixUtils.Subtract(MatrixUtils.Identity(StateDim), MatrixUtils.Multiply(K, _H)), _P);
            // Debug用書き下し
            //float[,] I = MatrixUtils.Identity(StateDim);
            //float[,] KH = MatrixUtils.Multiply(K, _H);
            //float[,] I_minus_KH = MatrixUtils.Subtract(I, KH);
            //_P = MatrixUtils.Multiply(I_minus_KH, _P);
            //DebugTool.InfoLog(string.Format("更新計算 - I: {0}", DebugTool.MatrixToString(I, ",", 12, 12)));
            //DebugTool.InfoLog(string.Format("更新計算 - KH: {0}", DebugTool.MatrixToString(KH, ",", 12, 12)));
            //DebugTool.InfoLog(string.Format("更新計算 - I_minus_KH: {0}", DebugTool.MatrixToString(I_minus_KH, ",", 12, 12)));

            //DebugTool.DebugLog(String.Format("Updateの_P {0}", DebugTool.MatrixToString(_P, 12, 12)));

            // M行列の更新（Pythonコードに合わせてここで更新）
            // self.M = self.nextstep(self.M,self.x_)
            // self.M_in = self.nextstep(self.M_in,self.x_prev)
            UpdateM();
            UpdateM_in();

            // E行列の更新
            _E = (float[,])_M.Clone();
            _E[8, 6] = 1;
        }

        private void UpdateM_in()
        {
            // Pythonコードのnextstepメソッドに合わせて、M_in行列のコピーを作成してから更新
            float[,] M_in_copy = (float[,])_M_in.Clone();

            // M_in行列の動的更新（x_prevを使用）
            M_in_copy[6, 1] = -_x_prev[8] * _dt;
            M_in_copy[6, 2] = _x_prev[7] * _dt;
            M_in_copy[7, 0] = _x_prev[8] * _dt;
            M_in_copy[7, 2] = -_x_prev[6] * _dt;
            M_in_copy[8, 0] = -_x_prev[7] * _dt;
            M_in_copy[8, 1] = _x_prev[6] * _dt;

            _M_in = M_in_copy;
        }

        //ベイジアンサプライズメソッド
        private float ComputeBayesianSurprise()
        {
            // 重力成分のインデックス (6-8: Gx, Gy, Gz)
            int[] idx = { 6, 7, 8 };

            // 差分ベクトル
            float[] x_diff = new float[3];
            for (int i = 0; i < 3; i++)
                x_diff[i] = _x_prev[idx[i]] - _x[idx[i]];

            // 部分行列の抽出
            float[,] P_prev_sub = new float[3, 3];
            float[,] P_sub = new float[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    P_prev_sub[i, j] = _P_prev[idx[i], idx[j]];
                    P_sub[i, j] = _P[idx[i], idx[j]];
                }
            }

            // 行列式と逆行列
            float detP = MatrixUtils.Determinant3x3(P_sub);
            float detP_prev = MatrixUtils.Determinant3x3(P_prev_sub);
            float logTerm = (float)Math.Log(detP / detP_prev);

            float[,] P_inv = MatrixUtils.PseudoInverse3x3(P_sub);  // 擬似逆行列を使用
            //Console.WriteLine("Multiply 10");
            float traceTerm = MatrixUtils.Trace(MatrixUtils.Multiply(P_inv, P_prev_sub));

            // 二次形式計算
            float quadTerm = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    quadTerm += x_diff[i] * P_inv[i, j] * x_diff[j];
                }
            }

            return 0.5f * (logTerm - 3 + traceTerm + quadTerm);
        }

        // 状態ベクトル取得
        // public float[] GetState() => _x;
        // 共分散行列取得
        //public float[,] GetCovariance() => _P;
        // サプライズ履歴取得
        // public List<float> GetSurpriseHistory() => _surpriseHistory;

        // 各種行列の取得
        // public float[,] GetF() => _F;
        // public float[,] GetH() => _H;
        // public float[,] GetM() => _M;
        // public float[,] GetE() => _E;
        // public float[,] GetQ() => _Q;
        //public float[,] GetR() => _R;
        //public float[,] GetPPrev() => _P_prev;
        // public float[,] GetMIn() => _M_in;
    }

}