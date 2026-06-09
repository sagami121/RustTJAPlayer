use crate::audio::AudioSystem;
use crate::input::{GameAction, is_action_pressed};
use crate::judge::{Judgment, judge_note};
use crate::models::{CourseData, NoteType, Note};
use crate::song_loader::SongInfo;
use crate::utils::resolve_path;
use eframe::egui;
use std::time::{Duration, Instant};
use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};

const START_DELAY_MS: f64 = 2000.0;

#[derive(PartialEq, Eq)]
pub enum PlayState {
    Ready,
    Playing,
}

/// Main application state for the TJA player GUI.
pub struct TjaPlayerApp {
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

    // Pre-loaded sound buffers
    dong_buffer: Option<Arc<Vec<u8>>>,
    ka_buffer: Option<Arc<Vec<u8>>>,

    /// Auto play flag
    is_autoplay: bool,

    /// Flag to signal return to song select
    pub exit_requested: bool,
}

impl TjaPlayerApp {
    pub fn new(song_info: SongInfo, selected_course: usize) -> Self {
        let mut audio_system = AudioSystem::new().expect("Failed to initialize audio system");
        audio_system.set_se_volume(song_info.chart.header.sevol);

        let dong_path = resolve_path("theme/default/sound/dong.wav");
        let ka_path = resolve_path("theme/default/sound/ka.wav");

        let dong_buffer = dong_path.and_then(|p| audio_system.load_sound(&p));
        let ka_buffer = ka_path.and_then(|p| audio_system.load_sound(&p));

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
            dong_buffer,
            ka_buffer,
            is_autoplay: false,
            exit_requested: false,
        }
    }

    fn selected_course(&self) -> Option<&CourseData> {
        self.song_info.chart.courses.get(self.selected_course)
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
                            let vol = self.song_info.chart.header.songvol as f32 / 100.0;
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
                if let Some(ref buf) = self.dong_buffer {
                    self.audio_system.play_buffer(Arc::clone(buf));
                }
            }
            GameAction::HitRight => {
                if let Some(ref buf) = self.ka_buffer {
                    self.audio_system.play_buffer(Arc::clone(buf));
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

impl eframe::App for TjaPlayerApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if ctx.input(|i| i.key_pressed(egui::Key::Escape)) {
            if let Some(sink) = &self.bgm_sink {
                sink.stop();
            }
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
        }

        let current_ms = self.current_time_ms();

        // 2. Process Notes
        if self.state == PlayState::Playing {
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
                                        GameAction::HitLeft => if let Some(ref b) = self.dong_buffer { self.audio_system.play_buffer(Arc::clone(b)); },
                                        GameAction::HitRight => if let Some(ref b) = self.ka_buffer { self.audio_system.play_buffer(Arc::clone(b)); },
                                    }
                                }
                            }
                        }
                    }
                }
                break;
            }
        }

        // 3. Render UI
        egui::TopBottomPanel::top("info_panel").show(ctx, |ui| {
            ui.horizontal(|ui| {
                let title = self.song_info.chart.header.title.as_deref().unwrap_or("Unknown");
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
                    
                    // Constant Scroll Speed (BPM Independent):
                    // The distance from the judgment line is determined purely by the time difference (ms)
                    // and a fixed scroll speed (px/ms). BPM changes do not affect the physical speed
                    // at which notes move across the screen.
                    let x = line_x + (dt as f32) * self.scroll_speed * (note.scroll_factor as f32);
                    
                    if x > rect.max.x + 100.0 { break; } 
                    if x < rect.min.x - 100.0 { continue; }

                    let (color, radius) = match note.note_type {
                        NoteType::Don => (egui::Color32::RED, 15.0),
                        NoteType::Ka => (egui::Color32::BLUE, 15.0),
                        NoteType::DonBig => (egui::Color32::RED, 25.0),
                        NoteType::KaBig => (egui::Color32::BLUE, 25.0),
                        _ => (egui::Color32::GRAY, 10.0),
                    };
                    
                    painter.circle_filled(egui::Pos2::new(x, lane_y), radius, color);
                    painter.circle_stroke(egui::Pos2::new(x, lane_y), radius, (1.0, egui::Color32::WHITE));
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
