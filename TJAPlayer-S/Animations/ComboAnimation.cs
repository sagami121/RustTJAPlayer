using System;
using System.Drawing;

namespace TjaPlayer.Animations
{
    /// <summary>
    /// 太鼓の達人風のコンボバウンドアニメーションを管理するクラス。
    /// バネ物理モデルを使用して、キレのある動きと戻り動作を再現します。
    /// </summary>
    public class ComboAnimation
    {
        // アニメーション定数
        private const float SpringConstant = 120.0f; // バネの強さ
        private const float Damping = 0.70f;        // 減衰率
        private const float DefaultScale = 1.0f;

        // 状態変数
        public int Combo { get; private set; }
        public float Scale { get; private set; }
        public float Velocity { get; private set; }

        public ComboAnimation()
        {
            Scale = DefaultScale;
            Velocity = 0.0f;
            Combo = 0;
        }

        /// <summary>
        /// コンボを加算します。
        /// </summary>
        public void AddCombo()
        {
            Combo++;
            
            // 加算時に速度にインパルスを与える（バウンド開始）
            // 拡大率 1.25倍程度になるようなインパルス
            float impulse = 0.5f; 
            Velocity += impulse;
        }

        public void ResetCombo()
        {
            Combo = 0;
            Scale = DefaultScale;
            Velocity = 0.0f;
        }

        /// <summary>
        /// 物理演算の更新 (60FPS前提)
        /// </summary>
        public void Update()
        {
            // バネの計算 (Hooke's Law + Damping)
            float force = (DefaultScale - Scale) * SpringConstant;
            Velocity += force * 0.016f; // 60FPS固定のdeltaTime
            Velocity *= Damping;
            Scale += Velocity;
        }
    }
}
