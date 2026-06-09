use std::path::PathBuf;
use std::env;

/// Utility functions for TJA parsing and timing calculations.

/// Returns the duration of a single beat in milliseconds.
pub fn beat_duration_ms(bpm: f64) -> f64 {
    60000.0 / bpm
}

/// Returns the duration of a full measure in milliseconds,
/// given the number of beats in the measure (usually 4.0).
pub fn measure_duration_ms(bpm: f64, measure_beats: f64) -> f64 {
    (60000.0 / bpm) * measure_beats
}

/// Robustly resolve a path by checking several likely locations.
pub fn resolve_path(relative_path: &str) -> Option<PathBuf> {
    // 1) Relative to current working directory
    if let Ok(cwd) = env::current_dir() {
        let cand = cwd.join(relative_path);
        if cand.exists() {
            return Some(cand);
        }
    }

    // 2) Relative to executable location
    if let Ok(exe) = env::current_exe() {
        if let Some(exe_parent) = exe.parent() {
            let cand = exe_parent.join(relative_path);
            if cand.exists() {
                return Some(cand);
            }
            // also try one level up from exe parent (common when running via cargo)
            if let Some(grand) = exe_parent.parent() {
                let cand2 = grand.join(relative_path);
                if cand2.exists() {
                    return Some(cand2);
                }
            }
        }
    }

    // 3) Walk up from cwd to look for a directory containing Cargo.toml, then sibling relative_path
    if let Ok(cwd) = env::current_dir() {
        let mut anc = cwd.as_path();
        for _ in 0..6 {
            let cargo = anc.join("Cargo.toml");
            if cargo.exists() {
                let cand = anc.join(relative_path);
                if cand.exists() {
                    return Some(cand);
                }
            }
            if let Some(parent) = anc.parent() {
                anc = parent;
            } else {
                break;
            }
        }
    }

    None
}
