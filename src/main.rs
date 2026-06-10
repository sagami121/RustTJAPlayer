mod models;
mod utils;
mod parser;
mod audio;
mod gui;
mod judge;
mod input;
mod songselect;
mod song_loader;
mod config;
mod tja;
mod score;
mod measure_jump;
mod animation;

use crate::songselect::SongSelectApp;
use crate::gui::RustTJAPlayerApp;
use crate::song_loader::{load_songs_from_directory, SongInfo};
use eframe::{egui, NativeOptions};
use std::fs;

/// The main application wrapper that manages screen transitions.
struct ManagerApp {
    state: AppState,
}

enum AppState {
    SongSelect(SongSelectApp),
    Playing(RustTJAPlayerApp),
}

impl ManagerApp {
    fn new(cc: &eframe::CreationContext<'_>, songs: Vec<SongInfo>) -> Self {
        setup_japanese_font(&cc.egui_ctx);
        Self {
            state: AppState::SongSelect(SongSelectApp::new(songs)),
        }
    }
}

impl eframe::App for ManagerApp {
    fn update(&mut self, ctx: &egui::Context, frame: &mut eframe::Frame) {
        match &mut self.state {
            AppState::SongSelect(app) => {
                app.update(ctx, frame);
                if app.confirmed {
                    if let Some(song_info) = app.songs.get(app.selected_index) {
                        let game_app = RustTJAPlayerApp::new(song_info.clone(), app.selected_difficulty_index);
                        self.state = AppState::Playing(game_app);
                    }
                }
            }
            AppState::Playing(app) => {
                app.update(ctx, frame);
                if app.exit_requested {
                    // Re-load songs to ensure the list is fresh (optional, but consistent)
                    let songs = load_songs_from_directory("songs");
                    self.state = AppState::SongSelect(SongSelectApp::new(songs));
                }
            }
        }
    }
}

fn setup_japanese_font(ctx: &egui::Context) {
    let mut fonts = egui::FontDefinitions::default();

    // Try to load Meiryo from Windows fonts directory
    let font_path = "C:\\Windows\\Fonts\\meiryo.ttc";
    if let Ok(font_data) = fs::read(font_path) {
        fonts.font_data.insert(
            "japanese_font".to_owned(),
            egui::FontData::from_owned(font_data),
        );

        // Put Japanese font first for both proportional and monospace
        fonts
            .families
            .get_mut(&egui::FontFamily::Proportional)
            .unwrap()
            .insert(0, "japanese_font".to_owned());

        fonts
            .families
            .get_mut(&egui::FontFamily::Monospace)
            .unwrap()
            .push("japanese_font".to_owned());
    }

    ctx.set_fonts(fonts);
}

fn main() {
    // Load songs from the songs directory
    let songs_dir = "songs";
    let songs = load_songs_from_directory(songs_dir);
    eprintln!("Found {} songs in '{}'", songs.len(), songs_dir);

    if songs.is_empty() {
        eprintln!("No songs found in '{}' directory", songs_dir);
        return;
    }

    let config = crate::config::load_config();

    // Set up native options for the window.
    let options = NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([config.window_width as f32, config.window_height as f32]),
        ..Default::default()
    };

    let _ = eframe::run_native(
        "RustTJAPlayer",
        options,
        Box::new(|cc| {
            // Start with the manager app
            Ok(Box::new(ManagerApp::new(cc, songs)))
        }),
    );
}