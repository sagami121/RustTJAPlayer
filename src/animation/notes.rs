use std::time::{Instant, Duration};
use egui::{Pos2, Color32, Ui};

/// ゲージへ飛んでいくノーツの魂エフェクト
pub struct SoulParticle {
    pub start_pos: Pos2,
    pub target_pos: Pos2,
    pub color: Color32,
    pub start_time: Instant,
    pub duration: Duration,
}

pub struct NoteAnimationManager {
    pub soul_particles: Vec<SoulParticle>,
}

impl NoteAnimationManager {
    pub fn new() -> Self {
        Self {
            soul_particles: Vec::new(),
        }
    }

    /// ノーツが叩かれた時にパーティクルを生成
    pub fn spawn_soul(&mut self, is_don: bool, start_pos: Pos2, target_pos: Pos2) {
        let color = if is_don {
            Color32::from_rgb(245, 70, 50)  // ドンの赤
        } else {
            Color32::from_rgb(70, 180, 245) // カッの青
        };

        self.soul_particles.push(SoulParticle {
            start_pos,
            target_pos,
            color,
            start_time: Instant::now(),
            duration: Duration::from_millis(350),
        });
    }

    /// 毎フレームの更新と描画。寿命が切れたものは自動削除
    pub fn update_and_draw(&mut self, ui: &mut Ui) {
        let now = Instant::now();
        let painter = ui.painter();

        // フェードアウト用の時間を定義 (0.2秒)
        let fade_out_duration = Duration::from_millis(200);

        self.soul_particles.retain(|p| {
            let elapsed = now.saturating_duration_since(p.start_time);
            let total_duration = p.duration + fade_out_duration;
            
            if elapsed >= total_duration {
                return false; // 完全に終了したので削除
            }

            // 進行度の計算 (0.0 ~ 1.0 が移動、1.0 ~ は滞留/フェード)
            let move_progress = (elapsed.as_secs_f32() / p.duration.as_secs_f32()).min(1.0);
            let is_fading = elapsed > p.duration;
            let fade_progress = if is_fading {
                (elapsed.saturating_sub(p.duration).as_secs_f32() / fade_out_duration.as_secs_f32()).min(1.0)
            } else {
                0.0
            };

            // 軌道の計算
            let lx = p.start_pos.x + (p.target_pos.x - p.start_pos.x) * move_progress;
            let ly = p.start_pos.y + (p.target_pos.y - p.start_pos.y) * move_progress;
            
            // 放物線（arc_y）の計算: move_progress が 0.5 の時に最大
            let arc_y_factor = -60.0; 
            let arc_y = arc_y_factor * (4.0 * move_progress * (1.0 - move_progress));
            let current_pos = egui::pos2(lx, ly + arc_y);

            // サイズと透明度
            let base_size = 12.0 * (1.0 - move_progress * 0.3);
            let size = if is_fading {
                base_size * (1.0 - fade_progress) // フェードアウト中に小さくする
            } else {
                base_size
            };
            
            let alpha = if is_fading {
                (1.0 - fade_progress).powf(2.0) // 滑らかに消える
            } else {
                1.0
            };

            let color_with_alpha = p.color.linear_multiply(alpha);
            let white_with_alpha = egui::Color32::WHITE.linear_multiply(alpha);

            // 描画：外側の白い光輪 + 内側の赤/青
            painter.circle_filled(current_pos, size + 2.0, white_with_alpha);
            painter.circle_filled(current_pos, size, color_with_alpha);

            // 残像エフェクト（移動中のみ）
            if !is_fading && move_progress > 0.1 {
                let trail_progress = move_progress - 0.08;
                let t_lx = p.start_pos.x + (p.target_pos.x - p.start_pos.x) * trail_progress;
                let t_ly = p.start_pos.y + (p.target_pos.y - p.start_pos.y) * trail_progress;
                let t_arc_y = (arc_y_factor * 0.5) * (4.0 * trail_progress * (1.0 - trail_progress));
                painter.circle_filled(
                    egui::pos2(t_lx, t_ly + t_arc_y),
                    size * 0.6,
                    p.color.linear_multiply(0.4 * alpha)
                );
            }

            true
        });

        // アニメーション中は継続して再描画を要求
        if !self.soul_particles.is_empty() {
            ui.ctx().request_repaint();
        }
    }
}