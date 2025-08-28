using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoreLinkSys1.Analysis
{
    class DynamicAcc
    {
        // グローバル座標系での水平面の加速度を計算するメソッド
        public static float[] CalculateGlobalAcceleration(float[] localAccel, float[] rotationMatrix)
        {
            // 回転行列を使用して加速度を変換
            float[] globalAccel = new float[3];

            // 回転行列と加速度の積を計算
            globalAccel[0] = rotationMatrix[0] * localAccel[0] + rotationMatrix[1] * localAccel[1] + rotationMatrix[2] * localAccel[2];
            globalAccel[1] = rotationMatrix[3] * localAccel[0] + rotationMatrix[4] * localAccel[1] + rotationMatrix[5] * localAccel[2];
            globalAccel[2] = rotationMatrix[6] * localAccel[0] + rotationMatrix[7] * localAccel[1] + rotationMatrix[8] * localAccel[2];

            // 重力加速度を考慮（Z軸方向の重力を除去）
            globalAccel[2] -= 1.0f; // 1Gの重力加速度を除去

            return globalAccel;
        }

        public static float[] CalculateDynamicAcceleration(float[] localAccel, float[] rotationMatrix)
        {
            // グローバル重力ベクトル（Z方向1G）
            float[] gravity = new float[] { 0f, 0f, 0.98f };

            // 重力ベクトルを回転行列で変換（グローバル→ローカルへの変換に対応）
            float[] rotatedGravity = new float[3];
            for (int i = 0; i < 3; i++)
            {
                rotatedGravity[i] =
                    rotationMatrix[i * 3 + 0] * gravity[0] +
                    rotationMatrix[i * 3 + 1] * gravity[1] +
                    rotationMatrix[i * 3 + 2] * gravity[2];
            }

            // 加速度から重力成分を除去
            float[] dynamicAccel = new float[3];
            for (int i = 0; i < 3; i++)
            {
                dynamicAccel[i] = localAccel[i] - rotatedGravity[i];
            }

            return dynamicAccel;
        }


    }
}