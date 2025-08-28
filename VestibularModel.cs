using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoreLinkSys1.Analysis
{
    public class VestibularModel
    {
        private readonly double[,] _A;
        private readonly double[,] _B;
        private readonly double[,] _C;
        private readonly double[,] _D;
        public double[,] _stateX;  // デバッグ用にpublicに変更
        private double[,] _stateY;
        private double[,] _stateZ;

        public VestibularModel(double taud, double taua, double dt)
        {
            // Pythonのdlti(...).to_ss()の出力に合わせて直接初期化
            _A = new double[,] {
                { 1.99851982, -0.99851989 },
                { 1.0,         0.0 }
            };
            _B = new double[,] {
                { 1.0 },
                { 0.0 }
            };
            _C = new double[,] {
                { -0.00147909, 0.00147901 }
            };
            _D = new double[,] {
                { 0.99925993 }
            };

            _stateX = new double[2, 1];
            _stateY = new double[2, 1];
            _stateZ = new double[2, 1];
        }

        public double[] Update(double[] angularVelocity)
        {
            double[] output = new double[3];

            output[0] = UpdateAxis(angularVelocity[0], ref _stateX);
            output[1] = UpdateAxis(angularVelocity[1], ref _stateY);
            output[2] = UpdateAxis(angularVelocity[2], ref _stateZ);

            return output;
        }

        public double UpdateAxis(double input, ref double[,] state)
        {
            // Pythonのcalc_vestibular_output_realtimeの実装に完全に合わせる
            // y = (self.sys_d.C @ state + self.sys_d.D * u).flatten()[0]
            // state = self.sys_d.A @ state + self.sys_d.B * u

            // まず出力を計算（現在の状態を使用）
            double output = _C[0, 0] * state[0, 0] + _C[0, 1] * state[1, 0] + _D[0, 0] * input;

            // 次に状態を更新（Pythonコードと同じ順序）
            double newState0 = _A[0, 0] * state[0, 0] + _A[0, 1] * state[1, 0] + _B[0, 0] * input;
            double newState1 = _A[1, 0] * state[0, 0] + _A[1, 1] * state[1, 0] + _B[1, 0] * input;

            state[0, 0] = newState0;
            state[1, 0] = newState1;

            return output;
        }

        // // デバッグ用：行列の値を確認するメソッド
        // public void PrintMatrices()
        // {
        //     Console.WriteLine("A matrix:");
        //     Console.WriteLine($"[{_A[0, 0]}, {_A[0, 1]}]");
        //     Console.WriteLine($"[{_A[1, 0]}, {_A[1, 1]}]");

        //     Console.WriteLine("B matrix:");
        //     Console.WriteLine($"[{_B[0, 0]}]");
        //     Console.WriteLine($"[{_B[1, 0]}]");

        //     Console.WriteLine("C matrix:");
        //     Console.WriteLine($"[{_C[0, 0]}, {_C[0, 1]}]");

        //     Console.WriteLine("D matrix:");
        //     Console.WriteLine($"[{_D[0, 0]}]");
        // }

        // デバッグ用：伝達関数の係数を確認するメソッド
        public void PrintTransferFunctionCoefficients(double taud, double taua, double dt)
        {
            double tauda = taud * taua;
            double fs = 1.0 / dt;

            double[] num_cont = { tauda, 0, 0 };
            double[] den_cont = { tauda, taud + taua, 1 };

            double a0 = den_cont[0] + 2 * fs * den_cont[1] + 4 * fs * fs * den_cont[2];
            double a1 = 2 * den_cont[0] - 8 * fs * fs * den_cont[2];
            double a2 = den_cont[0] - 2 * fs * den_cont[1] + 4 * fs * fs * den_cont[2];

            double b0 = num_cont[0] + 2 * fs * num_cont[1] + 4 * fs * fs * num_cont[2];
            double b1 = 2 * num_cont[0] - 8 * fs * fs * num_cont[2];
            double b2 = num_cont[0] - 2 * fs * num_cont[1] + 4 * fs * fs * num_cont[2];

            // Console.WriteLine("Transfer Function Coefficients:");
            // Console.WriteLine($"Continuous time: num={num_cont[0]}, den=[{den_cont[0]}, {den_cont[1]}, {den_cont[2]}]");
            // Console.WriteLine($"Discrete time: num=[{b0}, {b1}, {b2}], den=[{a0}, {a1}, {a2}]");
            // Console.WriteLine($"Normalized: num=[{b0 / a0}, {b1 / a0}, {b2 / a0}], den=[1, {a1 / a0}, {a2 / a0}]");
        }

        // テスト用：Pythonコードと同じ入力でテスト
        public double[] TestWithPythonInput(double[] input)
        {
            double[] output = new double[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                double[] gyro_input = { input[i], 0, 0 }; // X軸のみ
                double[] result = Update(gyro_input);
                output[i] = result[0]; // X軸の出力のみ
            }
            return output;
        }

        // テスト用：単一入力での出力を計算
        public double TestSingleInput(double input)
        {
            double[] gyro_input = { input, 0, 0 };
            double[] result = Update(gyro_input);
            return result[0];
        }

        // 状態をリセット
        public void Reset()
        {
            _stateX = new double[2, 1];
            _stateY = new double[2, 1];
            _stateZ = new double[2, 1];
        }

        // デバッグ用：状態と出力の詳細を表示
        // public void DebugStep(double input, int step)
        // {
        //     Console.WriteLine($"Step {step}:");
        //     Console.WriteLine($"  Input: {input}");
        //     Console.WriteLine($"  State X: [{_stateX[0, 0]:F8}, {_stateX[1, 0]:F8}]");
        //     Console.WriteLine($"  State Y: [{_stateY[0, 0]:F8}, {_stateY[1, 0]:F8}]");
        //     Console.WriteLine($"  State Z: [{_stateZ[0, 0]:F8}, {_stateZ[1, 0]:F8}]");

        //     double outputX = _C[0, 0] * _stateX[0, 0] + _C[0, 1] * _stateX[1, 0] + _D[0, 0] * input;
        //     Console.WriteLine($"  Output X: {outputX:F8}");
        //     Console.WriteLine();
        // }

        // Pythonコードとの完全一致テスト
        public double[] TestExactPythonMatch(double[] input)
        {
            Reset(); // 状態をリセット
            double[] output = new double[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                double[] gyro_input = { input[i], 0, 0 }; // X軸のみ
                output[i] = UpdateAxis(input[i], ref _stateX);

                // デバッグ情報（最初の数ステップのみ）
                //if (i < 5)
                //{
                //    DebugStep(input[i], i);
                //}
            }
            return output;
        }

        // Pythonコードとの詳細比較用：各ステップでの計算を詳細に表示
        // public void DebugDetailedStep(double input, int step)
        // {
        //     Console.WriteLine($"=== Step {step} 詳細計算 ===");
        //     Console.WriteLine($"入力: {input}");
        //     Console.WriteLine($"現在の状態: [{_stateX[0, 0]:F12}, {_stateX[1, 0]:F12}]");

        //     // 出力計算の詳細
        //     double output_calc = _C[0, 0] * _stateX[0, 0] + _C[0, 1] * _stateX[1, 0] + _D[0, 0] * input;
        //     Console.WriteLine($"出力計算: {_C[0, 0]:F12} * {_stateX[0, 0]:F12} + {_C[0, 1]:F12} * {_stateX[1, 0]:F12} + {_D[0, 0]:F12} * {input} = {output_calc:F12}");

        //     // 状態更新の詳細
        //     double newState0 = _A[0, 0] * _stateX[0, 0] + _A[0, 1] * _stateX[1, 0] + _B[0, 0] * input;
        //     double newState1 = _A[1, 0] * _stateX[0, 0] + _A[1, 1] * _stateX[1, 0] + _B[1, 0] * input;
        //     Console.WriteLine($"状態更新0: {_A[0, 0]:F12} * {_stateX[0, 0]:F12} + {_A[0, 1]:F12} * {_stateX[1, 0]:F12} + {_B[0, 0]:F12} * {input} = {newState0:F12}");
        //     Console.WriteLine($"状態更新1: {_A[1, 0]:F12} * {_stateX[0, 0]:F12} + {_A[1, 1]:F12} * {_stateX[1, 0]:F12} + {_B[1, 0]:F12} * {input} = {newState1:F12}");
        //     Console.WriteLine();
        // }
    }
}