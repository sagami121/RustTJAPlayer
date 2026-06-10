use std::fmt;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(u8)]
pub enum NoteType {
    Don = 1,
    Ka = 2,
    DonBig = 3,
    KaBig = 4,
    Roll = 5,
    RollBig = 6,
    End = 7,
    Balloon = 8,
}

impl NoteType {
    #[allow(dead_code)]
    pub fn from_u8(val: u8) -> Option<Self> {
        match val {
            1 => Some(NoteType::Don),
            2 => Some(NoteType::Ka),
            3 => Some(NoteType::DonBig),
            4 => Some(NoteType::KaBig),
            5 => Some(NoteType::Roll),
            6 => Some(NoteType::RollBig),
            7 => Some(NoteType::End),
            8 => Some(NoteType::Balloon),
            _ => None,
        }
    }
}

impl fmt::Display for NoteType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let s = match self {
            NoteType::Don => "Don",
            NoteType::Ka => "Ka",
            NoteType::DonBig => "DonBig",
            NoteType::KaBig => "KaBig",
            NoteType::Roll => "Roll",
            NoteType::RollBig => "RollBig",
            NoteType::Balloon => "Balloon",
            NoteType::End => "End",
        };
        write!(f, "{}", s)
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Note {
    pub note_type: NoteType,
    pub time_ms: f64,
    pub end_time_ms: Option<f64>,
    pub balloon_count: Option<u32>,
    /// Scroll multiplier baked in at parse time (never changes after parsing)
    pub scroll_factor: f64,
}

/// A measure bar line with its own scroll_factor baked in at parse time.
#[derive(Debug, Clone, PartialEq)]
pub struct BarLine {
    pub time_ms: f64,
    pub scroll_factor: f64,
}

pub struct ActiveRoll {
    pub note_type: NoteType,
    #[allow(dead_code)]
    pub start_time_ms: f64,
    pub end_time_ms: f64,
    pub count: u32,
}

#[allow(dead_code)]
#[derive(Debug, Clone, PartialEq)]
pub struct TjaHeader {
    pub title: Option<String>,
    pub bpm: Option<f64>,
    pub offset: Option<f64>,
    pub wave: Option<String>,
    pub demostart: Option<f64>,
    pub songvol: u32,
    pub sevol: u32,
    pub level: Option<u32>,
}

impl Default for TjaHeader {
    fn default() -> Self {
        TjaHeader {
            title: None,
            bpm: None,
            offset: None,
            wave: None,
            demostart: None,
            songvol: 100,
            sevol: 100,
            level: None,
        }
    }
}

#[allow(dead_code)]
#[derive(Debug, Clone, PartialEq)]
pub struct CourseData {
    pub course_type: Option<String>,
    pub level: Option<u32>,
    pub balloon: Vec<u32>,
    pub score_init: Option<u32>,
    pub score_diff: Option<u32>,
    pub notes: Vec<Note>,
}

impl Default for CourseData {
    fn default() -> Self {
        CourseData {
            course_type: None,
            level: None,
            balloon: Vec::new(),
            score_init: None,
            score_diff: None,
            notes: Vec::new(),
        }
    }
}

#[allow(dead_code)]
#[derive(Debug, Clone, PartialEq)]
pub struct TjaChart {
    pub header: TjaHeader,
    pub courses: Vec<CourseData>,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_note_type_from_u8() {
        assert_eq!(NoteType::from_u8(1), Some(NoteType::Don));
        assert_eq!(NoteType::from_u8(2), Some(NoteType::Ka));
        assert_eq!(NoteType::from_u8(3), Some(NoteType::DonBig));
        assert_eq!(NoteType::from_u8(4), Some(NoteType::KaBig));
        assert_eq!(NoteType::from_u8(5), Some(NoteType::Roll));
        assert_eq!(NoteType::from_u8(6), Some(NoteType::RollBig));
        assert_eq!(NoteType::from_u8(7), Some(NoteType::End));
        assert_eq!(NoteType::from_u8(8), Some(NoteType::Balloon));
        assert_eq!(NoteType::from_u8(0), None);
        assert_eq!(NoteType::from_u8(9), None);
    }

    #[test]
    fn test_note_type_display() {
        assert_eq!(NoteType::Don.to_string(), "Don");
        assert_eq!(NoteType::Ka.to_string(), "Ka");
        assert_eq!(NoteType::DonBig.to_string(), "DonBig");
        assert_eq!(NoteType::KaBig.to_string(), "KaBig");
        assert_eq!(NoteType::Roll.to_string(), "Roll");
        assert_eq!(NoteType::RollBig.to_string(), "RollBig");
        assert_eq!(NoteType::Balloon.to_string(), "Balloon");
        assert_eq!(NoteType::End.to_string(), "End");
    }

    #[test]
    fn test_note_creation() {
        let note = Note {
            note_type: NoteType::Don,
            time_ms: 0.0,
            end_time_ms: None,
            balloon_count: None,
            scroll_factor: 1.0,
        };
        assert_eq!(note.note_type, NoteType::Don);
        assert_eq!(note.time_ms, 0.0);
        assert_eq!(note.scroll_factor, 1.0);
    }

    #[test]
    fn test_tja_header_default() {
        let header = TjaHeader::default();
        assert_eq!(header.title, None);
        assert_eq!(header.bpm, None);
        assert_eq!(header.offset, None);
        assert_eq!(header.wave, None);
        assert_eq!(header.demostart, None);
        assert_eq!(header.songvol, 100);
        assert_eq!(header.sevol, 100);
        assert_eq!(header.level, None);
    }

    #[test]
    fn test_tja_header_set() {
        let header = TjaHeader {
            title: Some("Test".to_string()),
            bpm: Some(120.0),
            offset: Some(-1.0),
            wave: Some("song.ogg".to_string()),
            demostart: Some(5.0),
            songvol: 80,
            sevol: 90,
            level: Some(5),
        };
        assert_eq!(header.title, Some("Test".to_string()));
        assert_eq!(header.bpm, Some(120.0));
        assert_eq!(header.offset, Some(-1.0));
        assert_eq!(header.wave, Some("song.ogg".to_string()));
        assert_eq!(header.demostart, Some(5.0));
        assert_eq!(header.songvol, 80);
        assert_eq!(header.sevol, 90);
        assert_eq!(header.level, Some(5));
    }

    #[test]
    fn test_course_data_default() {
        let course = CourseData::default();
        assert_eq!(course.course_type, None);
        assert_eq!(course.level, None);
        assert!(course.balloon.is_empty());
        assert_eq!(course.score_init, None);
        assert_eq!(course.score_diff, None);
        assert!(course.notes.is_empty());
    }

    #[test]
    fn test_tja_chart() {
        let header = TjaHeader::default();
        let course = CourseData::default();
        let chart = TjaChart {
            header,
            courses: vec![course],
        };
        assert_eq!(chart.courses.len(), 1);
        assert!(chart.courses[0].notes.is_empty());
    }
}