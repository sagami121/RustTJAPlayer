use std::fs;
use std::path::{Path, PathBuf};
use crate::parser::parse;
use crate::models::TjaChart;
use encoding_rs::SHIFT_JIS;

/// Song information with file paths
#[derive(Clone, Debug)]
pub struct SongInfo {
    pub chart: TjaChart,
    pub tja_path: PathBuf,
    pub audio_path: Option<PathBuf>,
}

use crate::utils::resolve_path;

/// Load all songs from the songs directory
pub fn load_songs_from_directory(songs_dir: &str) -> Vec<SongInfo> {
    let mut songs = Vec::new();
    // Resolve songs directory robustly
    let path = resolve_path(songs_dir);

    if path.is_none() {
        eprintln!("Songs directory not found: {} (searched current dir, exe location, and ancestors)", songs_dir);
        return songs;
    }
    let path = path.unwrap();

    // Scan for .tja files
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

/// Load a single TJA file
fn load_song(tja_path: &Path) -> Option<SongInfo> {
    // Read TJA file content as bytes so we can try multiple encodings
    let bytes = fs::read(tja_path).ok()?;

    // Try UTF-8 first, then fall back to Shift_JIS (common on Windows)
    let content = match String::from_utf8(bytes.clone()) {
        Ok(s) => s,
        Err(_) => {
            let (cow, _, _had_errors) = SHIFT_JIS.decode(&bytes);
            cow.into_owned()
        }
    };

    // Parse TJA data
    let chart = parse(&content);
    
    // Determine audio file path
    let audio_path = if let Some(wave_name) = &chart.header.wave {
        let mut audio_file = tja_path.parent()?.to_path_buf();
        audio_file.push(wave_name);
        
        // Check if file exists
        if audio_file.exists() {
            Some(audio_file)
        } else {
            // Try different extensions if not found
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
        // Try to find audio file with same name as TJA
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
pub fn get_available_songs(songs_dir: &str) -> Vec<String> {
    let songs = load_songs_from_directory(songs_dir);
    songs
        .iter()
        .map(|song| {
            song.chart
                .header
                .title
                .as_deref()
                .unwrap_or("Unknown Song")
                .to_string()
        })
        .collect()
}
