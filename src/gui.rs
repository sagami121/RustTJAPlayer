use crate::audio::AudioSystem;
use crate::input::{GameAction, is_action_pressed};
use crate::judge::{Judgment, judge_note};
use crate::models::{NoteType, Note, ActiveRoll};
use crate::tja::QueryableCourseMetadata;
use crate::song_loader::SongInfo;
use crate::utils::resolve_path;
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
    /// Song information (paths, chart)
    song_info: SongInfo,
    /// Index of the currently selected course.
    selected_course: usize,
    /// Audio system.
    audio_system: AudioSystem,
    /// Playback sink for the background music.
    bgm_sink: Option<rodio::Sink>,
    /// Tracked BGM samples.
    bgm_samples: Option<Arc<AtomicU64>>,
    /// BGM sample rate.
    bgm_sample_rate: u32,
    /// BGM channels.
    bgm_channels: u16,
    /// Current play state
    pub state: PlayState,
    /// Whether the music has actually started playing.
    music_started: bool,
    /// Real-world start time (fallback and for latency compensation)
    wall_start: Instant,
    /// Current combo count.
    combo: u32,
    /// Total score.
    score: u32,
    /// Most recent judgment result.
    last_judgment: Option<Judgment>,
    /// Index of the next note to be judged in the selected course.
    next_note_idx: usize,
    /// Scroll speed in pixels per millisecond.
    scroll_speed: f32,
    /// X position of the judgment line.
    judgment_line_x: f32,

    // Pre-loaded and decoded sound sources (buffered for instant playback)
    dong_source: Option<Buffered<Decoder<BufReader<File>>>>,
    ka_source: Option<Buffered<Decoder<BufReader<File>>>>,

    /// Auto play flag
    is_autoplay: bool,

    /// Flag to signal return to song select
    pub exit_requested: bool,
    /// Active roll currently being processed
    pub active_roll: Option<ActiveRoll>,
    /// Last time an autoplay roll hit was triggered
    pub last_roll_hit_time_ms: f64,
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
            scroll_speed: 0.6,
            judgment_line_x: 200.0,
            dong_source,
            ka_source,
            is_autoplay: crate::config::load_config().auto_play,
            exit_requested: false,
            active_roll: None,
            last_roll_hit_time_ms: 0.0,
        }
    }

    fn selected_course(&self) -> Option<&QueryableCourseMetadata> {
        self.song_info.chart.course_metadata.get(self.selected_course)
    }

    /// Returns current playback time in milliseconds.
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
            if current_ms >= 0.0 {
                if let Some(ref audio_path) = self.song_info.audio_path {
                    if let Some((sink, counter, rate, channels)) = self.audio_system.play_tracked_file(audio_path) {
                        self.bgm_sink = Some(sink);
                        self.bgm_samples = Some(counter);
                        self.bgm_sample_rate = rate;
                        self.bgm_channels = channels;
                        
                        if let Some(ref s) = self.bgm_sink {
                            let vol = self.song_info.chart.bgm_vol as f32 / 100.0;
                            s.set_volume(vol);
                        }
                    }
                }
                self.music_started = true;
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
        } else {
            self.combo = 0;
        }
        self.last_judgment = Some(judgment);
        self.next_note_idx += 1;
    }
}

impl eframe::App for RustTJAPlayerApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if ctx.input(|i| i.key_pressed(egui::Key::Escape)) {
            if let Some(sink) = &self.bgm_sink {
                sink.stop();
            }
            crate::config::save_config(self.is_autoplay);
            self.exit_requested = true;
            return;
        }

        if self.state == PlayState::Ready {
            if ctx.input(|i| i.key_pressed(egui::Key::Space)) {
                self.state = PlayState::Playing;
                self.wall_start = Instant::now();
            }
        }

        self.maybe_start_playback();

        if ctx.input(|i| i.key_pressed(egui::Key::F1)) {
            self.is_autoplay = !self.is_autoplay;
            crate::config::save_config(self.is_autoplay);
        }

        let current_ms = self.current_time_ms();

        // 2. Process Notes
        if self.state == PlayState::Playing {
            // Expire active roll if it ended
            if let Some(ref active_roll) = self.active_roll {
                if current_ms > active_roll.end_time_ms {
                    self.active_roll = None;
                }
            }

            // Handle Active Roll Inputs
            if let Some(ref mut active_roll) = self.active_roll {
                if self.is_autoplay {
                    // Simulate rolling (16th note equivalent: ~60ms gap)
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
                    // Prevent infinite loop if something goes wrong
                    loop_count += 1;
                    if loop_count > 100 { break; }

                    let note_info: Option<Note> = if let Some(course) = self.selected_course() {
                        course.notes.get(self.next_note_idx).cloned()
                    } else {
                        None
                    };

                    if let Some(note) = note_info {
                        // Check if it's a Roll/Balloon
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

        // 3. Render UI
        egui::TopBottomPanel::top("info_panel").show(ctx, |ui| {
            ui.horizontal(|ui| {
                let title = if self.song_info.chart.title.is_empty() { "Unknown" } else { &self.song_info.chart.title };
                ui.label(egui::RichText::new(title).size(24.0).strong());
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

            let lane_y = rect.center().y;
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
                
                for i in start_draw_idx..course.notes.len() {
                    let note = &course.notes[i];
                    let dt = note.time_ms - current_ms;
                    let diff_sec = dt / 1000.0;
                    const BASE_PIXELS_PER_SECOND: f64 = 400.0;
                    let scroll_factor = self.scroll_speed as f64 * note.scroll_factor as f64;
                    let x = line_x + ((diff_sec * BASE_PIXELS_PER_SECOND * scroll_factor) as f32);
                    
                    if x > rect.max.x + 100.0 { break; } 
                    let mut end_x = x;
                    if let Some(end_time_ms) = note.end_time_ms {
                        let end_dt = end_time_ms - current_ms;
                        let end_diff_sec = end_dt / 1000.0;
                        end_x = line_x + ((end_diff_sec * BASE_PIXELS_PER_SECOND * scroll_factor) as f32);
                    }

                    if end_x < rect.min.x - 100.0 { continue; }

                    // Draw Roll Band
                    if matches!(note.note_type, NoteType::Roll | NoteType::RollBig | NoteType::Balloon) {
                        if note.end_time_ms.is_some() {
                            let band_height = if matches!(note.note_type, NoteType::RollBig) { 50.0 } else { 30.0 };
                            let band_rect = egui::Rect::from_min_max(
                                egui::Pos2::new(x, lane_y - band_height / 2.0),
                                egui::Pos2::new(end_x, lane_y + band_height / 2.0),
                            );
                            let color = if note.note_type == NoteType::Balloon {
                                egui::Color32::from_rgb(255, 100, 100) // Reddish for balloon
                            } else {
                                egui::Color32::from_rgb(255, 200, 50) // Yellow for roll
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

            if self.state == PlayState::Ready {
                painter.text(
                    rect.center(),
                    egui::Align2::CENTER_CENTER,
                    "PRESS SPACE KEY",
                    egui::FontId::new(48.0, egui::FontFamily::Monospace),
                    egui::Color32::WHITE,
                );
            }
        });

        // Request repaint after a short delay to keep the loop alive without hogging CPU
        ctx.request_repaint_after(Duration::from_millis(2));
    }
}
