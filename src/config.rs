use std::fs;
use std::path::Path;

const CONFIG_FILE: &str = "config.ini";

#[derive(Debug, Clone)]
pub struct AppConfig {
    pub auto_play: bool,
    pub chart_create_mode: bool,
    pub window_width: u32,
    pub window_height: u32,
}

impl Default for AppConfig {
    fn default() -> Self {
        Self { 
            auto_play: false,
            chart_create_mode: false,
            window_width: 1280,
            window_height: 720,
        }
    }
}

pub fn load_config() -> AppConfig {
    let mut config = AppConfig::default();
    if Path::new(CONFIG_FILE).exists() {
        if let Ok(contents) = fs::read_to_string(CONFIG_FILE) {
            for line in contents.lines() {
                let line = line.trim();
                if let Some((key, val)) = line.split_once('=') {
                    let key = key.trim();
                    let val = val.trim().to_lowercase();
                    match key {
                        "auto_play" => config.auto_play = val == "true" || val == "1",
                        "chart_create_mode" => config.chart_create_mode = val == "true" || val == "1",
                        "window_width" => config.window_width = val.parse().unwrap_or(1280),
                        "window_height" => config.window_height = val.parse().unwrap_or(720),
                        _ => {}
                    }
                }
            }
        }
    }
    config
}

pub fn save_config(auto_play: bool, chart_create_mode: bool, window_width: u32, window_height: u32) {
    let mut lines = Vec::new();
    lines.push("[Game]".to_string());
    lines.push(format!("auto_play = {}", auto_play));
    lines.push(format!("chart_create_mode = {}", chart_create_mode));
    lines.push(format!("window_width = {}", window_width));
    lines.push(format!("window_height = {}", window_height));

    let out = lines.join("\n");
    let _ = fs::write(CONFIG_FILE, out);
}
