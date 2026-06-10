use eframe::egui;
use crate::song_loader::SongInfo;

#[derive(PartialEq, Eq)]
pub enum SongSelectState {
    SelectingSong,
    SelectingDifficulty,
}

/// Song select screen state, styled after Taiko no Tatsujin Jiro 2
pub struct SongSelectApp {
    /// Available songs
    pub songs: Vec<SongInfo>,
    /// Index of currently selected song
    pub selected_index: usize,
    /// Index of currently selected difficulty within the song
    pub selected_difficulty_index: usize,
    /// Current selection state
    pub state: SongSelectState,
    /// Flag indicating selection is fully confirmed
    pub confirmed: bool,
}

impl SongSelectApp {
    pub fn new(songs: Vec<SongInfo>) -> Self {
        Self {
            songs,
            selected_index: 0,
            selected_difficulty_index: 0,
            state: SongSelectState::SelectingSong,
            confirmed: false,
        }
    }

    /// Get list of song titles
    fn get_song_titles(&self) -> Vec<String> {
        self.songs
            .iter()
            .enumerate()
            .map(|(idx, song_info)| {
                if song_info.chart.title.is_empty() {
                    format!("Song {}", idx)
                } else {
                    song_info.chart.title.clone()
                }
            })
            .collect()
    }

    /// Handle keyboard input for song selection
    fn handle_input(&mut self, ctx: &egui::Context) {
        let input = ctx.input(|i| i.clone());
        
        match self.state {
            SongSelectState::SelectingSong => {
                let song_count = self.songs.len();
                if song_count == 0 { return; }

                if input.key_pressed(egui::Key::ArrowUp) || input.key_pressed(egui::Key::K) {
                    if self.selected_index > 0 {
                        self.selected_index -= 1;
                    } else {
                        self.selected_index = song_count - 1;
                    }
                }
                if input.key_pressed(egui::Key::ArrowDown) || input.key_pressed(egui::Key::D) {
                    self.selected_index = (self.selected_index + 1) % song_count;
                }

                if input.key_pressed(egui::Key::Enter) || input.key_pressed(egui::Key::F) || input.key_pressed(egui::Key::J) {
                    self.state = SongSelectState::SelectingDifficulty;
                    self.selected_difficulty_index = 0;
                }
            }
            SongSelectState::SelectingDifficulty => {
                let song = &self.songs[self.selected_index];
                let course_count = song.chart.course_metadata.len();
                if course_count == 0 {
                    self.state = SongSelectState::SelectingSong;
                    return;
                }

                if input.key_pressed(egui::Key::ArrowUp) || input.key_pressed(egui::Key::K) {
                    if self.selected_difficulty_index > 0 {
                        self.selected_difficulty_index -= 1;
                    } else {
                        self.selected_difficulty_index = course_count - 1;
                    }
                }
                if input.key_pressed(egui::Key::ArrowDown) || input.key_pressed(egui::Key::D) {
                    self.selected_difficulty_index = (self.selected_difficulty_index + 1) % course_count;
                }

                if input.key_pressed(egui::Key::Escape) || input.key_pressed(egui::Key::Backspace) {
                    self.state = SongSelectState::SelectingSong;
                }

                if input.key_pressed(egui::Key::Enter) || input.key_pressed(egui::Key::F) || input.key_pressed(egui::Key::J) {
                    self.confirmed = true;
                }
            }
        }
    }
}

impl eframe::App for SongSelectApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        self.handle_input(ctx);
        ctx.request_repaint();

        egui::CentralPanel::default()
            .frame(egui::Frame::none().fill(egui::Color32::from_rgb(26, 26, 26)))
            .show(ctx, |ui| {
                let painter = ui.painter();
                let screen_rect = ui.available_rect_before_wrap();

                // Background song title
                painter.text(
                    egui::pos2(20.0, screen_rect.bottom() - 50.0),
                    egui::Align2::LEFT_CENTER,
                    "SONG SELECT",
                    egui::FontId::new(36.0, egui::FontFamily::Monospace),
                    egui::Color32::from_gray(100),
                );

                let songs = self.get_song_titles();
                if songs.is_empty() { return; }

                // Central bar
                let bar_height = 60.0;
                let bar_y_center = screen_rect.center().y;
                let bar_rect = egui::Rect::from_x_y_ranges(
                    screen_rect.x_range(),
                    (bar_y_center - bar_height / 2.0)..=(bar_y_center + bar_height / 2.0),
                );
                painter.rect_filled(bar_rect, 0.0, egui::Color32::from_rgba_unmultiplied(10, 10, 10, 200));

                // Draw song list
                let song_height = 50.0;
                for (i, song_title) in songs.iter().enumerate() {
                    let offset = (i as i32 - self.selected_index as i32) as f32;
                    let y = bar_y_center + offset * song_height;
                    let is_selected = i == self.selected_index;
                    
                    let font_size = if is_selected { 28.0 } else { 20.0 };
                    let color = if is_selected { egui::Color32::WHITE } else { egui::Color32::from_gray(150) };
                    
                    painter.text(
                        egui::pos2(screen_rect.center().x, y),
                        egui::Align2::CENTER_CENTER,
                        song_title,
                        egui::FontId::new(font_size, egui::FontFamily::Proportional),
                        color,
                    );
                }

                // Difficulty Selection Popup
                if self.state == SongSelectState::SelectingDifficulty {
                    // Dim background
                    painter.rect_filled(screen_rect, 0.0, egui::Color32::from_rgba_unmultiplied(0, 0, 0, 150));

                    let song = &self.songs[self.selected_index];
                    let popup_width = 300.0;
                    let popup_height = (song.chart.course_metadata.len() as f32 * 40.0) + 60.0;
                    let popup_rect = egui::Rect::from_center_size(screen_rect.center(), egui::vec2(popup_width, popup_height));
                    
                    painter.rect_filled(popup_rect, 5.0, egui::Color32::from_rgb(20, 20, 20));
                    painter.rect_stroke(popup_rect, 5.0, (2.0, egui::Color32::WHITE));

                    painter.text(
                        egui::pos2(popup_rect.center().x, popup_rect.top() + 25.0),
                        egui::Align2::CENTER_CENTER,
                        "SELECT DIFFICULTY",
                        egui::FontId::new(20.0, egui::FontFamily::Monospace),
                        egui::Color32::YELLOW,
                    );

                    for (i, course) in song.chart.course_metadata.iter().enumerate() {
                        let y = popup_rect.top() + 60.0 + (i as f32 * 40.0);
                        let is_selected = i == self.selected_difficulty_index;
                        
                        let name = &course.course_type;
                        let level = course.level_taiko.max(0) as usize;
                        let text = format!("{}  {}", name, "★".repeat(level.min(10)));
                        
                        let color = if is_selected { egui::Color32::RED } else { egui::Color32::WHITE };
                        let font_id = egui::FontId::new(18.0, egui::FontFamily::Proportional);

                        if is_selected {
                            painter.rect_filled(
                                egui::Rect::from_center_size(egui::pos2(popup_rect.center().x, y), egui::vec2(popup_width - 20.0, 30.0)),
                                3.0,
                                egui::Color32::from_rgba_unmultiplied(255, 0, 0, 50),
                            );
                        }

                        painter.text(
                            egui::pos2(popup_rect.center().x, y),
                            egui::Align2::CENTER_CENTER,
                            text,
                            font_id,
                            color,
                        );
                    }
                }
            });
    }
}
