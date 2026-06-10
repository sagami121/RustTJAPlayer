use crate::audio::AudioSystem;
use crate::input::{GameAction, is_action_pressed};
use crate::judge::{Judgment, judge_note};
use crate::models::{NoteType, Note, ActiveRoll};
use crate::song_loader::SongInfo;
use crate::utils::resolve_path;
use crate::measure_jump::MeasureJumpManager;
use crate::animation::notes::NoteAnimationManager;
use eframe::egui;
use std::time::{Duration, Instant};
use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};
use std::io::BufReader;
use std::fs::File;
use rodio::{Decoder, source::Buffered};

const START_DELAY_MS: f64 = 2000.0;

#[derive(PartialEq, Eq)]
pub enum PlayState {
    Ready,
    Playing,
}

/// Main application state for the TJA player GUI.
pub struct RustTJAPlayerApp {
    song_info: SongInfo,
    selected_course: usize,
    audio_system: AudioSystem,
    bgm_sink: Option<rodio::Sink>,
    bgm_samples: Option<Arc<AtomicU64>>,
    bgm_sample_rate: u32,
    bgm_channels: u16,
    pub state: PlayState,
    music_started: bool,
    wall_start: Instant,
    combo: u32,
    score: u32,
    last_judgment: Option<Judgment>,
    next_note_idx: usize,
    scroll_speed: f32,
    judgment_line_x: f32,
    dong_source: Option<Buffered<Decoder<BufReader<File>>>>,
    ka_source: Option<Buffered<Decoder<BufReader<File>>>>,
    is_autoplay: bool,
    chart_create_mode: bool,
    pub exit_requested: bool,
    pub active_roll: Option<ActiveRoll>,
    pub last_roll_hit_time_ms: f64,
    pub measure_jump: MeasureJumpManager,
    pub note_animation_manager: NoteAnimationManager,
    pub soul_value: f32,
    pub max_soul_value: f32,
}

impl RustTJAPlayerApp {
    pub fn new(song_info: SongInfo, selected_course: usize) -> Self {
        let mut audio_system = AudioSystem::new().expect("Failed to initialize audio system");
        audio_system.set_se_volume(
            song_info.chart.global_custom_metadata.get("sevol")
                .and_then(|s| s.parse::<u32>().ok())
                .unwrap_or(100)
        );

        let dong_path = resolve_path("theme/default/sound/dong.wav");
        let ka_path = resolve_path("theme/default/sound/ka.wav");

        let dong_source = dong_path.and_then(|p| audio_system.load_cached_sound(&p));
        let ka_source = ka_path.and_then(|p| audio_system.load_cached_sound(&p));

        let config = crate::config::load_config();
        let is_autoplay = config.auto_play;
        let chart_create_mode = config.chart_create_mode;
        
        let course = song_info.chart.course_metadata.get(selected_course);
        let total_measures = course.map(|c| c.bar_lines.len()).unwrap_or(1);
        let note_count = course.map(|c| c.notes.iter().filter(|n| matches!(n.note_type, NoteType::Don | NoteType::Ka | NoteType::DonBig | NoteType::KaBig)).count()).unwrap_or(1);

        Self {
            song_info,
            selected_course,
            audio_system,
            bgm_sink: None,
            bgm_samples: None,
            bgm_sample_rate: 44100,
            bgm_channels: 2,
            state: PlayState::Ready,
            music_started: false,
            wall_start: Instant::now(),
            combo: 0,
            score: 0,
            last_judgment: None,
            next_note_idx: 0,
            scroll_speed: 1.0,
            judgment_line_x: 200.0,
            dong_source,
            ka_source,
            is_autoplay,
            chart_create_mode,
            exit_requested: false,
            active_roll: None,
            last_roll_hit_time_ms: 0.0,
            measure_jump: MeasureJumpManager::new(total_measures),
            note_animation_manager: NoteAnimationManager::new(),
            soul_value: 0.0,
            max_soul_value: note_count as f32,
        }
    }
    fn selected_course(&self) -> Option<&crate::tja::QueryableCourseMetadata> {
        self.song_info.chart.course_metadata.get(self.selected_course)
    }

    fn get_time_at_measure(&self, measure: f32) -> f64 {
        if let Some(course) = self.selected_course() {
            let m1 = measure.floor() as usize;
            let m2 = measure.ceil() as usize;
            
            let t1 = course.bar_lines.get(m1).map(|b| b.time_ms).unwrap_or(0.0);
            if m1 == m2 { return t1; }
            let t2 = course.bar_lines.get(m2).map(|b| b.time_ms).unwrap_or(t1);
            
            t1 + (t2 - t1) * (measure - m1 as f32) as f64
        } else {
            0.0
        }
    }

    fn jump_to_measure(&mut self, measure: usize) {
        let params = if let Some(course) = self.selected_course() {
            if let Some(bar) = course.bar_lines.get(measure) {
                let target_time = bar.time_ms;
                let measure_length = course.bar_lines.get(measure + 1).map(|b| b.time_ms - target_time).unwrap_or(2000.0);
                let next_note_idx = course.notes.iter().position(|n| n.time_ms >= target_time - measure_length).unwrap_or(course.notes.len());
                Some((target_time, measure_length, next_note_idx))
            } else { None }
        } else { None };

        if let Some((target_time, measure_length, next_note_idx)) = params {
            self.wall_start = Instant::now() - Duration::from_millis((target_time - measure_length).max(0.0) as u64);
            self.next_note_idx = next_note_idx;
            
            if let Some(ref sink) = self.bgm_sink { sink.stop(); }
            self.bgm_sink = None;
            self.music_started = false;
            self.active_roll = None;
        }
    }

    fn current_time_ms(&self) -> f64 {
        if self.state == PlayState::Ready {
            return -START_DELAY_MS;
        }

        if let Some(counter) = &self.bgm_samples {
            let samples = counter.load(Ordering::SeqCst) as f64;
            let total_channels = self.bgm_channels as f64;
            let rate = self.bgm_sample_rate as f64;
            if total_channels > 0.0 && rate > 0.0 {
                return (samples / total_channels) / rate * 1000.0;
            }
        }
        
        let elapsed = self.wall_start.elapsed().as_secs_f64() * 1000.0;
        elapsed - START_DELAY_MS
    }

    fn maybe_start_playback(&mut self) {
        if self.state == PlayState::Playing && !self.music_started {
            let current_ms = self.current_time_ms();
            let measure = self.measure_jump.current_measure().round() as usize;
            
            let target_time = self.song_info.chart.course_metadata.get(self.selected_course)
                .and_then(|c| c.bar_lines.get(measure)).map(|b| b.time_ms);

            if let Some(target_time) = target_time {
                if current_ms >= target_time {
                    let audio_path = self.song_info.audio_path.clone();
                    if let Some(audio_path) = audio_path {
                        if let Some((sink, counter, rate, channels)) = self.audio_system.play_tracked_file(&audio_path) {
                            self.bgm_sink = Some(sink);
                            self.bgm_samples = Some(counter);
                            self.bgm_sample_rate = rate;
                            self.bgm_channels = channels;
                            if let Some(ref s) = self.bgm_sink {
                                s.set_volume(self.song_info.chart.bgm_vol as f32 / 100.0);
                                s.try_seek(Duration::from_millis(target_time.max(0.0) as u64)).ok();
                            }
                        }
                    }
                    self.music_started = true;
                }
            }
        }
    }

    fn process_hit(&mut self, action: GameAction, judgment: Judgment) {
        match action {
            GameAction::HitLeft => {
                if let Some(ref src) = self.dong_source {
                    self.audio_system.play_cached(src);
                }
            }
            GameAction::HitRight => {
                if let Some(ref src) = self.ka_source {
                    self.audio_system.play_cached(src);
                }
            }
        }

        if judgment.is_hit() {
            self.combo += 1;
            self.score += judgment.score();
            
            // 魂ゲージの加算
            let increment = match judgment {
                Judgment::Perfect => 1.0,
                Judgment::Good => 0.5,
                _ => 0.0,
            };
            self.soul_value = (self.soul_value + increment).min(self.max_soul_value);
        } else {
            self.combo = 0;
            // 魂ゲージの減算 (ミス)
            self.soul_value = (self.soul_value - 1.0).max(0.0);
        }
        self.last_judgment = Some(judgment);
        self.next_note_idx += 1;
    }
}

impl eframe::App for RustTJAPlayerApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        let screen_rect = ctx.screen_rect();
        let lane_y = screen_rect.center().y;
        
        // 魂ゲージの位置設定 (画面上部、判定線より右側)
        let gauge_rect = egui::Rect::from_min_max(
            screen_rect.left_top() + egui::vec2(self.judgment_line_x + 50.0, 80.0),
            screen_rect.left_top() + egui::vec2(screen_rect.width() - 50.0, 105.0)
        );
        let soul_progress = if self.max_soul_value > 0.0 { (self.soul_value / self.max_soul_value).clamp(0.0, 1.0) } else { 0.0 };
        let soul_tip_pos = egui::pos2(
            gauge_rect.left() + gauge_rect.width() * soul_progress,
            gauge_rect.center().y
        );

        if ctx.input(|i| i.key_pressed(egui::Key::Escape)) {
            if let Some(sink) = &self.bgm_sink {
                sink.stop();
            }
            crate::config::save_config(self.is_autoplay, self.chart_create_mode, 1280, 720);
            self.exit_requested = true;
            return;
        }

        if ctx.input(|i| i.key_pressed(egui::Key::Q)) {
            if let Some(sink) = &self.bgm_sink {
                sink.stop();
            }
            self.bgm_sink = None;
            self.music_started = false;
            self.state = PlayState::Ready;
            self.combo = 0;
            self.score = 0;
            self.soul_value = 0.0;
            self.last_judgment = None;
            self.active_roll = None;
            self.next_note_idx = 0;
        }

        if self.state == PlayState::Ready && self.chart_create_mode {
            if ctx.input(|i| i.key_pressed(egui::Key::Space)) {
                let measure = self.measure_jump.current_measure().round() as usize;
                self.jump_to_measure(measure);
                self.state = PlayState::Playing;
            }
        }

        self.maybe_start_playback();

        if ctx.input(|i| i.key_pressed(egui::Key::F1)) {
            self.is_autoplay = !self.is_autoplay;
            crate::config::save_config(self.is_autoplay, self.chart_create_mode, 1280, 720);
        }

        if ctx.input(|i| i.key_pressed(egui::Key::PageUp)) {
            self.measure_jump.move_relative(-1);
            self.jump_to_measure(self.measure_jump.target_measure());
        }
        if ctx.input(|i| i.key_pressed(egui::Key::PageDown)) {
            self.measure_jump.move_relative(1);
            self.jump_to_measure(self.measure_jump.target_measure());
        }
        if ctx.input(|i| i.key_pressed(egui::Key::Home)) {
            self.measure_jump.jump_to(0);
            self.jump_to_measure(self.measure_jump.target_measure());
        }
        if ctx.input(|i| i.key_pressed(egui::Key::End)) {
            self.measure_jump.jump_to(self.measure_jump.total_measures() - 1);
            self.jump_to_measure(self.measure_jump.target_measure());
        }

        self.measure_jump.update(ctx.input(|i| i.stable_dt));

        let current_ms = if self.state == PlayState::Playing {
            self.current_time_ms()
        } else {
            self.get_time_at_measure(self.measure_jump.current_measure())
        };

        if self.state == PlayState::Playing {
            if let Some(ref active_roll) = self.active_roll {
                if current_ms > active_roll.end_time_ms {
                    self.active_roll = None;
                }
            }

            if let Some(ref mut active_roll) = self.active_roll {
                if self.is_autoplay {
                    if current_ms - self.last_roll_hit_time_ms > 60.0 {
                        active_roll.count += 1;
                        if let Some(ref src) = self.dong_source { self.audio_system.play_cached(src); }
                        self.score += 10;
                        self.last_roll_hit_time_ms = current_ms;
                    }
                } else {
                    let don = is_action_pressed(ctx, GameAction::HitLeft);
                    let ka = is_action_pressed(ctx, GameAction::HitRight);
                    
                    let mut hit = false;
                    if don {
                        hit = true;
                        if let Some(ref src) = self.dong_source { self.audio_system.play_cached(src); }
                    } else if ka && matches!(active_roll.note_type, NoteType::Roll | NoteType::RollBig) {
                        hit = true;
                        if let Some(ref src) = self.ka_source { self.audio_system.play_cached(src); }
                    }
                    
                    if hit {
                        active_roll.count += 1;
                        self.score += 10;
                    }
                }
            } else {
                let mut loop_count = 0;
                loop {
                    loop_count += 1;
                    if loop_count > 100 { break; }

                    let note_info: Option<Note> = if let Some(course) = self.selected_course() {
                        course.notes.get(self.next_note_idx).cloned()
                    } else {
                        None
                    };

                    if let Some(note) = note_info {
                        if matches!(note.note_type, NoteType::Roll | NoteType::RollBig | NoteType::Balloon) {
                            if current_ms >= note.time_ms {
                                if let Some(end_time_ms) = note.end_time_ms {
                                    self.active_roll = Some(ActiveRoll {
                                        note_type: note.note_type,
                                        start_time_ms: note.time_ms,
                                        end_time_ms,
                                        count: 0,
                                    });
                                }
                                self.next_note_idx += 1;
                                continue;
                            }
                        }

                        if self.is_autoplay {
                            if current_ms >= note.time_ms {
                                let action = match note.note_type {
                                    NoteType::Don | NoteType::DonBig => Some(GameAction::HitLeft),
                                    NoteType::Ka | NoteType::KaBig => Some(GameAction::HitRight),
                                    _ => None,
                                };
                                if let Some(act) = action {
                                    let is_don = matches!(note.note_type, NoteType::Don | NoteType::DonBig);
                                    let start_pos = egui::Pos2::new(self.judgment_line_x, lane_y);
                                    self.note_animation_manager.spawn_soul(is_don, start_pos, soul_tip_pos);
                                    self.process_hit(act, Judgment::Perfect);
                                } else {
                                    self.next_note_idx += 1;
                                }
                                continue;
                            }
                        } else {
                            if current_ms > note.time_ms + 150.0 {
                                self.combo = 0;
                                self.last_judgment = Some(Judgment::Miss);
                                self.soul_value = (self.soul_value - 1.0).max(0.0);
                                self.next_note_idx += 1;
                                continue;
                            } else {
                                let mut hit_action = None;
                                if is_action_pressed(ctx, GameAction::HitLeft) { hit_action = Some(GameAction::HitLeft); }
                                else if is_action_pressed(ctx, GameAction::HitRight) { hit_action = Some(GameAction::HitRight); }

                                if let Some(action) = hit_action {
                                    let is_don = matches!(note.note_type, NoteType::Don | NoteType::DonBig);
                                    let is_ka = matches!(note.note_type, NoteType::Ka | NoteType::KaBig);
                                    
                                    let valid_hit = match action {
                                        GameAction::HitLeft => is_don,
                                        GameAction::HitRight => is_ka,
                                    };

                                    if valid_hit {
                                        let start_pos = egui::Pos2::new(self.judgment_line_x, lane_y);
                                        self.note_animation_manager.spawn_soul(is_don, start_pos, soul_tip_pos);
                                        let judgment = judge_note(current_ms, note.time_ms, 150.0);
                                        if judgment != Judgment::Miss || current_ms > note.time_ms {
                                            self.process_hit(action, judgment);
                                        }
                                    } else {
                                        match action {
                                            GameAction::HitLeft => if let Some(ref src) = self.dong_source { self.audio_system.play_cached(src); },
                                            GameAction::HitRight => if let Some(ref src) = self.ka_source { self.audio_system.play_cached(src); },
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }
        }

        egui::TopBottomPanel::top("info_panel").show(ctx, |ui| {
            ui.horizontal(|ui| {
                let title = if self.song_info.chart.title.is_empty() { "Unknown" } else { &self.song_info.chart.title };
                ui.label(egui::RichText::new(title).size(24.0).strong());
                ui.separator();
                ui.label(egui::RichText::new(format!(
                    "MEASURE: {:03}/{:03}",
                    self.measure_jump.current_measure().round() as usize,
                    self.measure_jump.total_measures()
                )).size(20.0));
                ui.separator();
                ui.label(egui::RichText::new(format!("Combo: {}", self.combo)).size(20.0).color(egui::Color32::YELLOW));
                ui.separator();
                ui.label(egui::RichText::new(format!("Score: {}", self.score)).size(20.0).color(egui::Color32::GREEN));
                ui.separator();
                if let Some(jud) = self.last_judgment {
                    ui.label(egui::RichText::new(jud.to_str()).size(24.0).strong().color(jud.color()));
                }

                if self.is_autoplay {
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        ui.label(egui::RichText::new("AUTO PLAY").size(24.0).strong().color(egui::Color32::RED));
                    });
                }
            });
        });

        egui::CentralPanel::default().show(ctx, |ui| {
            let (rect, _) = ui.allocate_exact_size(ui.available_size(), egui::Sense::hover());
            let painter = ui.painter_at(rect);

            // 魂ゲージの描画
            painter.rect_filled(gauge_rect, 5.0, egui::Color32::from_gray(50));
            let current_gauge_rect = egui::Rect::from_min_max(
                gauge_rect.left_top(),
                egui::pos2(soul_tip_pos.x, gauge_rect.bottom())
            );
            let gauge_color = if soul_progress >= 1.0 { egui::Color32::GOLD } else { egui::Color32::from_rgb(255, 100, 100) };
            painter.rect_filled(current_gauge_rect, 5.0, gauge_color);
            painter.rect_stroke(gauge_rect, 5.0, (2.0, egui::Color32::WHITE));

            let lane_height = 80.0;
            painter.rect_filled(
                egui::Rect::from_x_y_ranges(rect.x_range(), (lane_y - lane_height/2.0)..=(lane_y + lane_height/2.0)),
                0.0,
                egui::Color32::from_gray(30),
            );

            let line_x = self.judgment_line_x;
            painter.line_segment(
                [egui::Pos2::new(line_x, lane_y - lane_height/2.0), egui::Pos2::new(line_x, lane_y + lane_height/2.0)],
                (2.0, egui::Color32::WHITE),
            );

            if let Some(course) = self.selected_course() {
                let start_draw_idx = if self.next_note_idx > 10 { self.next_note_idx - 10 } else { 0 };

                for bar in &course.bar_lines {
                    let dt = bar.time_ms - current_ms;
                    let diff_sec = dt / 1000.0;
                    if diff_sec < -0.5 || diff_sec > 2.5 { continue; }

                    const BASE_PPS: f64 = 400.0;
                    let x = line_x + (diff_sec * BASE_PPS * self.scroll_speed as f64 * bar.scroll_factor) as f32;
                    painter.line_segment(
                        [egui::Pos2::new(x, lane_y - lane_height / 2.0), egui::Pos2::new(x, lane_y + lane_height / 2.0)],
                        egui::Stroke::new(1.0, egui::Color32::from_gray(80)),
                    );
                }
                
                for i in start_draw_idx..course.notes.len() {
                    let note = &course.notes[i];
                    let dt = note.time_ms - current_ms;
                    let diff_sec = dt / 1000.0;

                    if diff_sec < -0.5 || diff_sec > 2.5 { continue; }
                    
                    const BASE_PIXELS_PER_SECOND: f64 = 400.0;
                    let scroll_factor = self.scroll_speed as f64 * note.scroll_factor as f64;
                    let x = line_x + ((diff_sec * BASE_PIXELS_PER_SECOND * scroll_factor) as f32);
                    
                    let mut end_x = x;
                    if let Some(end_time_ms) = note.end_time_ms {
                        let end_dt = end_time_ms - current_ms;
                        let end_diff_sec = end_dt / 1000.0;
                        end_x = line_x + ((end_diff_sec * BASE_PIXELS_PER_SECOND * scroll_factor) as f32);
                    }

                    if matches!(note.note_type, NoteType::Roll | NoteType::RollBig | NoteType::Balloon) {
                        if note.end_time_ms.is_some() {
                            let band_height = if matches!(note.note_type, NoteType::RollBig) { 50.0 } else { 30.0 };
                            let band_rect = egui::Rect::from_min_max(
                                egui::Pos2::new(x, lane_y - band_height / 2.0),
                                egui::Pos2::new(end_x, lane_y + band_height / 2.0),
                            );
                            let color = if note.note_type == NoteType::Balloon {
                                egui::Color32::from_rgb(255, 100, 100)
                            } else {
                                egui::Color32::from_rgb(255, 200, 50)
                            };
                            painter.rect_filled(band_rect, 15.0, color);
                            painter.rect_stroke(band_rect, 15.0, (2.0, egui::Color32::BLACK));
                        }
                    }

                    let (color, radius) = match note.note_type {
                        NoteType::Don => (egui::Color32::RED, 15.0),
                        NoteType::Ka => (egui::Color32::BLUE, 15.0),
                        NoteType::DonBig => (egui::Color32::RED, 25.0),
                        NoteType::KaBig => (egui::Color32::BLUE, 25.0),
                        NoteType::Roll => (egui::Color32::YELLOW, 15.0),
                        NoteType::RollBig => (egui::Color32::YELLOW, 25.0),
                        NoteType::Balloon => (egui::Color32::from_rgb(255, 100, 100), 15.0),
                        _ => (egui::Color32::GRAY, 10.0),
                    };
                    
                    if note.note_type != NoteType::End {
                        painter.circle_filled(egui::Pos2::new(x, lane_y), radius, color);
                        painter.circle_stroke(egui::Pos2::new(x, lane_y), radius, (1.0, egui::Color32::WHITE));
                    }
                }
            }

            if self.state == PlayState::Ready && self.chart_create_mode {
                painter.text(
                    rect.center(),
                    egui::Align2::CENTER_CENTER,
                    "PRESS SPACE KEY",
                    egui::FontId::new(48.0, egui::FontFamily::Monospace),
                    egui::Color32::WHITE,
                );
            }
            self.note_animation_manager.update_and_draw(ui);
        });

        ctx.request_repaint_after(Duration::from_millis(2));
    }
}
