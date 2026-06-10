use std::fs;
use std::path::{Path, PathBuf};
use crate::parser::parse;
use crate::tja::Tja;
use encoding_rs::SHIFT_JIS;

/// Song information with file paths
#[derive(Clone, Debug)]
pub struct SongInfo {
    pub chart: Tja,
    #[allow(dead_code)]
    pub tja_path: PathBuf,
    pub audio_path: Option<PathBuf>,
}

use crate::utils::resolve_path;

/// Load all songs from the songs directory
pub fn load_songs_from_directory(songs_dir: &str) -> Vec<SongInfo> {
    let mut songs = Vec::new();
    let path = resolve_path(songs_dir);

    if path.is_none() {
        eprintln!("Songs directory not found: {} (searched current dir, exe location, and ancestors)", songs_dir);
        return songs;
    }
    let path = path.unwrap();

    match fs::read_dir(path) {
        Ok(entries) => {
            for entry in entries.flatten() {
                let entry_path = entry.path();
                if let Some(ext) = entry_path.extension() {
                    if ext == "tja" {
                        if let Some(song_info) = load_song(&entry_path) {
                            songs.push(song_info);
                        }
                    }
                }
            }
        }
        Err(e) => eprintln!("Error reading songs directory: {}", e),
    }

    songs
}

fn load_song(tja_path: &Path) -> Option<SongInfo> {
    let bytes = fs::read(tja_path).ok()?;

    let content = match String::from_utf8(bytes.clone()) {
        Ok(s) => s,
        Err(_) => {
            let (cow, _, _had_errors) = SHIFT_JIS.decode(&bytes);
            cow.into_owned()
        }
    };

    let chart = parse(&content);

    // Determine audio file path from bgm_path field
    let audio_path = if !chart.bgm_path.is_empty() {
        let mut audio_file = tja_path.parent()?.to_path_buf();
        audio_file.push(&chart.bgm_path);

        if audio_file.exists() {
            Some(audio_file)
        } else {
            let stem = audio_file.file_stem()?;
            let parent = audio_file.parent()?;
            let alternatives = vec![
                parent.join(format!("{}.ogg", stem.to_string_lossy())),
                parent.join(format!("{}.wav", stem.to_string_lossy())),
                parent.join(format!("{}.mp3", stem.to_string_lossy())),
            ];
            alternatives.into_iter().find(|p| p.exists())
        }
    } else {
        let stem = tja_path.file_stem()?;
        let parent = tja_path.parent()?;
        let alternatives = vec![
            parent.join(format!("{}.ogg", stem.to_string_lossy())),
            parent.join(format!("{}.wav", stem.to_string_lossy())),
            parent.join(format!("{}.mp3", stem.to_string_lossy())),
        ];
        alternatives.into_iter().find(|p| p.exists())
    };

    Some(SongInfo {
        chart,
        tja_path: tja_path.to_path_buf(),
        audio_path,
    })
}

/// Get a list of all available songs
#[allow(dead_code)]
pub fn get_available_songs(songs_dir: &str) -> Vec<String> {
    let songs = load_songs_from_directory(songs_dir);
    songs
        .iter()
        .map(|song| {
            if song.chart.title.is_empty() {
                "Unknown Song".to_string()
            } else {
                song.chart.title.clone()
            }
        })
        .collect()
}
