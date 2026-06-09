/// Judgment module for determining how well a note was hit.
use eframe::egui::Color32;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Judgment {
    Perfect,
    Good,
    Bad,
    Miss,
}

impl Judgment {
    /// Returns a string representation suitable for display.
    pub fn to_str(self) -> &'static str {
        match self {
            Judgment::Perfect => "良",
            Judgment::Good => "可",
            Judgment::Bad => "不可",
            Judgment::Miss => "MISS",
        }
    }

    /// Returns the color associated with this judgment.
    pub fn color(self) -> Color32 {
        match self {
            Judgment::Perfect => Color32::GOLD,
            Judgment::Good => Color32::LIGHT_BLUE,
            Judgment::Bad => Color32::from_rgb(255, 165, 0),
            Judgment::Miss => Color32::RED,
        }
    }

    /// Returns the score value for this judgment.
    pub fn score(self) -> u32 {
        match self {
            Judgment::Perfect => 300,
            Judgment::Good => 100,
            Judgment::Bad => 50,
            Judgment::Miss => 0,
        }
    }

    /// Whether this judgment counts as a hit (increases combo).
    pub fn is_hit(self) -> bool {
        matches!(self, Judgment::Perfect | Judgment::Good | Judgment::Bad)
    }
}

/// Judges a note hit given the current playback time and note time.
pub fn judge_note(current_ms: f64, note_ms: f64, window: f64) -> Judgment {
    let diff = (current_ms - note_ms).abs();
    if diff <= window * 0.5 {
        Judgment::Perfect
    } else if diff <= window * 1.0 {
        Judgment::Good
    } else if diff <= window * 1.5 {
        Judgment::Bad
    } else {
        Judgment::Miss
    }
}
