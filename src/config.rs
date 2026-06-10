use std::fs;
use std::path::Path;

const CONFIG_FILE: &str = "config.ini";

#[derive(Debug, Clone)]
pub struct AppConfig {
    pub auto_play: bool,
}

impl Default for AppConfig {
    fn default() -> Self {
        Self { auto_play: false }
    }
}

pub fn load_config() -> AppConfig {
    let mut config = AppConfig::default();
    if Path::new(CONFIG_FILE).exists() {
        if let Ok(contents) = fs::read_to_string(CONFIG_FILE) {
            for line in contents.lines() {
                let line = line.trim();
                if line.starts_with("auto_play") {
                    if let Some((_, val)) = line.split_once('=') {
                        let val = val.trim().to_lowercase();
                        config.auto_play = val == "true" || val == "1";
                    }
                }
            }
        }
    }
    config
}

pub fn save_config(auto_play: bool) {
    let contents = if Path::new(CONFIG_FILE).exists() {
        fs::read_to_string(CONFIG_FILE).unwrap_or_default()
    } else {
        String::new()
    };

    let mut lines: Vec<String> = contents.lines().map(|s| s.to_string()).collect();
    let mut game_section_found = false;
    let mut key_found = false;

    let target_val = if auto_play { "true" } else { "false" };
    let new_line = format!("auto_play = {}", target_val);

    let mut i = 0;
    while i < lines.len() {
        let line = lines[i].trim();
        if line == "[Game]" {
            game_section_found = true;
        } else if line.starts_with('[') && game_section_found {
            if !key_found {
                lines.insert(i, new_line.clone());
                key_found = true;
            }
            break;
        } else if game_section_found && line.starts_with("auto_play") {
            lines[i] = new_line.clone();
            key_found = true;
            break;
        }
        i += 1;
    }

    if !game_section_found {
        if !lines.is_empty() && !lines.last().unwrap().is_empty() {
            lines.push(String::new());
        }
        lines.push("[Game]".to_string());
        lines.push(new_line.clone());
    } else if !key_found {
        lines.push(new_line.clone());
    }

    let mut out = lines.join("\n");
    out.push('\n');
    let _ = fs::write(CONFIG_FILE, out);
}
