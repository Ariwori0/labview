using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoreLinkSys1.Analysis
{
    /// <summary>
    /// 行列演算クラス
    /// (きーた参考)https://qiita.com/sekky0816/items/8c73a7ec32fd9b040127
    /// </summary>
    public static class MatrixUtils
    {
        public static void AssertShape(float[,] a, float[,] b)
        {
            if (a.GetLength(1) != b.GetLength(0))
            {
                throw new ArgumentException(String.Format("行列サイズ不一致: a({0}x{1}), b({2}x{3})", a.GetLength(0), a.GetLength(1), b.GetLength(0), b.GetLength(1)));
            }
        }


        //改良版行列同士の積
        public static float[,] Multiply(float[,] a, float[,] b)
        {

            MatrixUtils.AssertShape(a, b);

            int rowsA = a.GetLength(0);
            int colsA = a.GetLength(1);
            int rowsB = b.GetLength(0);
            int colsB = b.GetLength(1);

            float[,] result = new float[rowsA, colsB];

            // 結果行列を0で初期化
            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    result[i, j] = 0.0f;
                }
            }

            for (int i = 0; i < rowsA; i++)
            {
                for (int k = 0; k < colsA; k++)
                {
                    float a_ik = a[i, k];
                    for (int j = 0; j < colsB; j++)
                    {
                        result[i, j] += a_ik * b[k, j];
                    }
                }
            }
            return result;
        }


        // 行列の加算
        public static float[,] Add(float[,] a, float[,] b)
        {
            int rows = a.GetLength(0);
            int cols = a.GetLength(1);
            float[,] result = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = a[i, j] + b[i, j];
                }
            }
            return result;
        }

        // 行列の減算
        public static float[,] Subtract(float[,] a, float[,] b)
        {
            int rows = a.GetLength(0);
            int cols = a.GetLength(1);
            float[,] result = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = a[i, j] - b[i, j];
                }
            }
            return result;
        }

        // 転置行列
        public static float[,] Transpose(float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            float[,] result = new float[cols, rows];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }
            return result;
        }

        //ガウス–ジョルダン法による一般の正方行列の逆行列
        public static float[,] Inverse(float[,] A)
        {
            int n = A.GetLength(0);
            if (n != A.GetLength(1)) throw new InvalidOperationException("Not a square matrix.");

            float[,] a = (float[,])A.Clone();
            float[,] inv = Identity(n);

            for (int k = 0; k < n; k++)
            {
                int max = k;
                for (int j = k + 1; j < n; j++)
                {
                    if (Math.Abs(a[j, k]) > Math.Abs(a[max, k]))
                        max = j;
                }

                // Swap rows
                for (int i = 0; i < n; i++)
                {
                    float tmp = a[max, i];
                    a[max, i] = a[k, i];
                    a[k, i] = tmp;
                    tmp = inv[max, i];
                    inv[max, i] = inv[k, i];
                    inv[k, i] = tmp;
                }

                float pivot = a[k, k];
                if (Math.Abs(pivot) < 1e-12f) throw new InvalidOperationException("Matrix is singular.");

                for (int i = 0; i < n; i++)
                {
                    a[k, i] /= pivot;
                    inv[k, i] /= pivot;
                }

                for (int j = 0; j < n; j++)
                {
                    if (j == k) continue;
                    float factor = a[j, k];
                    for (int i = 0; i < n; i++)
                    {
                        a[j, i] -= a[k, i] * factor;
                        inv[j, i] -= inv[k, i] * factor;
                    }
                }
            }

            return inv;
        }


        // 逆行列 (3x3専用)
        public static float[,] Inverse3x3(float[,] matrix)
        {
            float a = matrix[0, 0], b = matrix[0, 1], c = matrix[0, 2];
            float d = matrix[1, 0], e = matrix[1, 1], f = matrix[1, 2];
            float g = matrix[2, 0], h = matrix[2, 1], i = matrix[2, 2];

            float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
            if (Math.Abs(det) < 1e-12)
                throw new InvalidOperationException("Matrix is singular");

            float invDet = 1f / det;

            return new float[,] {
                { (e*i - f*h) * invDet, (c*h - b*i) * invDet, (b*f - c*e) * invDet },
                { (f*g - d*i) * invDet, (a*i - c*g) * invDet, (c*d - a*f) * invDet },
                { (d*h - e*g) * invDet, (b*g - a*h) * invDet, (a*e - b*d) * invDet }
            };
        }

        // 擬似逆行列 (3x3専用、特異行列でも安全)
        public static float[,] PseudoInverse3x3(float[,] matrix)
        {
            float a = matrix[0, 0], b = matrix[0, 1], c = matrix[0, 2];
            float d = matrix[1, 0], e = matrix[1, 1], f = matrix[1, 2];
            float g = matrix[2, 0], h = matrix[2, 1], i = matrix[2, 2];

            float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

            // 行列式が非常に小さい場合は単位行列を返す
            if (Math.Abs(det) < 1e-12f)
            {
                return Identity(3);
            }

            float invDet = 1f / det;

            return new float[,] {
                { (e*i - f*h) * invDet, (c*h - b*i) * invDet, (b*f - c*e) * invDet },
                { (f*g - d*i) * invDet, (a*i - c*g) * invDet, (c*d - a*f) * invDet },
                { (d*h - e*g) * invDet, (b*g - a*h) * invDet, (a*e - b*d) * invDet }
            };
        }

        // 行列式計算(3x3専用) LU不要の閉形式で
        public static float Determinant3x3(float[,] matrix)
        {
            return matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) -
                   matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0]) +
                   matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);
        }

        // トレース
        public static float Trace(float[,] matrix)
        {
            float trace = 0;
            int n = Math.Min(matrix.GetLength(0), matrix.GetLength(1));
            for (int i = 0; i < n; i++)
            {
                trace += matrix[i, i];
            }
            return trace;
        }

        // 単位行列の生成
        public static float[,] Identity(int size)
        {
            float[,] result = new float[size, size];
            for (int i = 0; i < size; i++)
            {
                result[i, i] = 1f;
            }
            return result;
        }

        // ベクトルと行列の乗算 (ベクトルを1行N列の行列として扱う)
        public static float[] VectorMatrixMultiply(float[] vector, float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            // ベクトルの長さと行列の行数が一致することを確認
            if (vector.Length != rows)
            {
                throw new ArgumentException(String.Format("ベクトル長({0})と行列行数({1})が一致しません", vector.Length, rows));
            }

            float[] result = new float[cols];

            for (int j = 0; j < cols; j++)
            {
                float sum = 0;
                for (int k = 0; k < rows; k++)  // vector.Lengthではなくrowsを使用
                {
                    sum += vector[k] * matrix[k, j];
                }
                result[j] = sum;
            }
            return result;
        }

        // 行列とベクトルの乗算 (行列×ベクトル)
        public static float[] MatrixVectorMultiply(float[,] matrix, float[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            // 行列の列数とベクトルの長さが一致することを確認
            if (vector.Length != cols)
            {
                throw new ArgumentException(String.Format("行列列数({0})とベクトル長({1})が一致しません", cols, vector.Length));
            }

            float[] result = new float[rows];

            for (int i = 0; i < rows; i++)
            {
                float sum = 0;
                for (int j = 0; j < cols; j++)
                {
                    sum += matrix[i, j] * vector[j];
                }
                result[i] = sum;
            }
            return result;
        }

        public static void Multiply12x12(float[,] a, float[,] b, float[,] result)
        {
            // b を事前転置しておくことでキャッシュ効率化
            float[,] bT = Transpose(b);

            for (int i = 0; i < 12; i++)
            {
                for (int j = 0; j < 12; j++)
                {
                    float sum = 0f;
                    // アンローリング (4段)
                    sum += a[i, 0] * bT[j, 0];
                    sum += a[i, 1] * bT[j, 1];
                    sum += a[i, 2] * bT[j, 2];
                    sum += a[i, 3] * bT[j, 3];
                    sum += a[i, 4] * bT[j, 4];
                    sum += a[i, 5] * bT[j, 5];
                    sum += a[i, 6] * bT[j, 6];
                    sum += a[i, 7] * bT[j, 7];
                    sum += a[i, 8] * bT[j, 8];
                    sum += a[i, 9] * bT[j, 9];
                    sum += a[i, 10] * bT[j, 10];
                    sum += a[i, 11] * bT[j, 11];
                    result[i, j] = sum;
                }
            }
        }

        /// <summary>
        /// キャッシュブロッキング＋ループアンローリング（4段）による高速行列積（N×N正方行列専用）
        /// BLOCK=16固定、アンローリング4段
        /// </summary>
        public static void MultiplyBlockedUnrolled(float[,] a, float[,] b, float[,] c, int N)
        {
            const int BLOCK = 16;
            // cを0で初期化
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    c[i, j] = 0f;

            for (int ii = 0; ii < N; ii += BLOCK)
            {
                for (int kk = 0; kk < N; kk += BLOCK)
                {
                    for (int jj = 0; jj < N; jj += BLOCK)
                    {
                        int iMax = Math.Min(ii + BLOCK, N);
                        int kMax = Math.Min(kk + BLOCK, N);
                        int jMax = Math.Min(jj + BLOCK, N);
                        for (int i = ii; i < iMax; i += 4)
                        {
                            int i1 = i + 1, i2 = i + 2, i3 = i + 3;
                            for (int k = kk; k < kMax; k += 4)
                            {
                                int k1 = k + 1, k2 = k + 2, k3 = k + 3;
                                for (int j = jj; j < jMax; j++)
                                {
                                    // i
                                    if (i < N) {
                                        if (k < N)   c[i, j] += a[i, k]   * b[k, j];
                                        if (k1 < N)  c[i, j] += a[i, k1]  * b[k1, j];
                                        if (k2 < N)  c[i, j] += a[i, k2]  * b[k2, j];
                                        if (k3 < N)  c[i, j] += a[i, k3]  * b[k3, j];
                                    }
                                    // i1
                                    if (i1 < N) {
                                        if (k < N)   c[i1, j] += a[i1, k]   * b[k, j];
                                        if (k1 < N)  c[i1, j] += a[i1, k1]  * b[k1, j];
                                        if (k2 < N)  c[i1, j] += a[i1, k2]  * b[k2, j];
                                        if (k3 < N)  c[i1, j] += a[i1, k3]  * b[k3, j];
                                    }
                                    // i2
                                    if (i2 < N) {
                                        if (k < N)   c[i2, j] += a[i2, k]   * b[k, j];
                                        if (k1 < N)  c[i2, j] += a[i2, k1]  * b[k1, j];
                                        if (k2 < N)  c[i2, j] += a[i2, k2]  * b[k2, j];
                                        if (k3 < N)  c[i2, j] += a[i2, k3]  * b[k3, j];
                                    }
                                    // i3
                                    if (i3 < N) {
                                        if (k < N)   c[i3, j] += a[i3, k]   * b[k, j];
                                        if (k1 < N)  c[i3, j] += a[i3, k1]  * b[k1, j];
                                        if (k2 < N)  c[i3, j] += a[i3, k2]  * b[k2, j];
                                        if (k3 < N)  c[i3, j] += a[i3, k3]  * b[k3, j];
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 12x12行列専用の完全アンローリング＋転置最適化による高速行列積
        /// a, b, resultはすべて12x12のfloat[,]配列
        /// </summary>
        public static void Multiply12x12Unrolled(float[,] a, float[,] b, float[,] result)
        {
            // bを転置してキャッシュ効率を上げる
            float[,] bT = new float[12, 12];
            for (int i = 0; i < 12; i++)
                for (int j = 0; j < 12; j++)
                    bT[j, i] = b[i, j];

            // resultを0で初期化
            for (int i = 0; i < 12; i++)
                for (int j = 0; j < 12; j++)
                    result[i, j] = 0f;

            // 完全アンローリング
            for (int i = 0; i < 12; i++)
            {
                for (int j = 0; j < 12; j++)
                {
                    float sum = 0f;
                    sum += a[i, 0] * bT[j, 0];
                    sum += a[i, 1] * bT[j, 1];
                    sum += a[i, 2] * bT[j, 2];
                    sum += a[i, 3] * bT[j, 3];
                    sum += a[i, 4] * bT[j, 4];
                    sum += a[i, 5] * bT[j, 5];
                    sum += a[i, 6] * bT[j, 6];
                    sum += a[i, 7] * bT[j, 7];
                    sum += a[i, 8] * bT[j, 8];
                    sum += a[i, 9] * bT[j, 9];
                    sum += a[i, 10] * bT[j, 10];
                    sum += a[i, 11] * bT[j, 11];
                    result[i, j] = sum;
                }
            }
        }

        /// <summary>
        /// 6x6実正方行列のQR分解（グラム・シュミット法）
        /// 入力: a[6,6]
        /// 出力: q[6,6], r[6,6]（q:直交行列, r:上三角）
        //         float[,] A = new float[6,6]; // 6x6行列をセット
        // // ... Aに値をセット ...
        // float[,] Q, R;
        // MatrixUtils.QRDecomposition6x6(A, out Q, out R);
        // Q, Rが得られる
        /// </summary>
        public static void QRDecomposition6x6(float[,] a, out float[,] q, out float[,] r)
        {
            int N = 6;
            q = new float[N, N];
            r = new float[N, N];
            float[,] v = new float[N, N];

            // v = aの列ベクトルコピー
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                    v[i, j] = a[i, j];

            for (int j = 0; j < N; j++)
            {
                // r[k, j] = q_k^T * a_j
                for (int k = 0; k < j; k++)
                {
                    float dot = 0f;
                    for (int i = 0; i < N; i++)
                        dot += q[i, k] * a[i, j];
                    r[k, j] = dot;
                    for (int i = 0; i < N; i++)
                        v[i, j] -= r[k, j] * q[i, k];
                }
                // r[j, j] = ||v_j||
                float norm = 0f;
                for (int i = 0; i < N; i++)
                    norm += v[i, j] * v[i, j];
                norm = (float)Math.Sqrt(norm);
                r[j, j] = norm;
                if (norm < 1e-8f)
                    throw new InvalidOperationException("線形従属な列が含まれています");
                // q_j = v_j / ||v_j||
                for (int i = 0; i < N; i++)
                    q[i, j] = v[i, j] / norm;
            }
        }

        // Q^T * y を計算
        public static float[] MultiplyQtVector(float[,] q, float[] y)
        {
            int N = 6;
            float[] z = new float[N];
            for (int i = 0; i < N; i++)
            {
                float sum = 0f;
                for (int j = 0; j < N; j++)
                    sum += q[j, i] * y[j];
                z[i] = sum;
            }
            return z;
        }

        // 上三角行列Rで後退代入
        public static float[] BackSubstitution(float[,] r, float[] z)
        {
            int N = 6;
            float[] x = new float[N];
            for (int i = N - 1; i >= 0; i--)
            {
                float sum = z[i];
                for (int j = i + 1; j < N; j++)
                    sum -= r[i, j] * x[j];
                x[i] = sum / r[i, i];
            }
            return x;
        }

    }
}