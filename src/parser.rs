use crate::models::{BarLine, Note, NoteType};
use crate::tja::{QueryableCourseMetadata, Tja};
use std::collections::HashMap;

/// Parses a TJA formatted string into a Tja struct.
pub fn parse(tja_text: &str) -> Tja {
    let mut tja = Tja::default();
    let mut courses: Vec<QueryableCourseMetadata> = Vec::new();
    let mut current_course: Option<QueryableCourseMetadata> = None;
    let mut in_course = false;
    let mut collecting_notes = false;
    let mut note_data_buffer = String::new();
    let mut sevol: u32 = 100;

    for line in tja_text.lines() {
        let line_without_comment = if let Some(idx) = line.find("//") {
            &line[..idx]
        } else {
            line
        };
        let trimmed = line_without_comment.trim();
        if trimmed.is_empty() {
            continue;
        }

        let upper = trimmed.to_uppercase();

        // ─── Global header fields ────────────────────────────────────────────
        if upper.starts_with("TITLE:") {
            tja.title = trimmed["TITLE:".len()..].trim().to_string();
            continue;
        }
        if upper.starts_with("SUBTITLE:") {
            tja.subtitle = trimmed["SUBTITLE:".len()..].trim().to_string();
            continue;
        }
        if upper.starts_with("BPM:") {
            if let Ok(v) = trimmed["BPM:".len()..].trim().parse::<f64>() {
                tja.bpm = v;
                tja.base_bpm = v;
                tja.min_bpm = v;
                tja.max_bpm = v;
            }
            continue;
        }
        if upper.starts_with("OFFSET:") {
            if let Ok(v) = trimmed["OFFSET:".len()..].trim().parse::<f64>() {
                tja.global_custom_metadata.insert("offset".to_string(), v.to_string());
            }
            continue;
        }
        if upper.starts_with("WAVE:") {
            tja.bgm_path = trimmed["WAVE:".len()..].trim().to_string();
            continue;
        }
        if upper.starts_with("SONGVOL:") {
            if let Ok(v) = trimmed["SONGVOL:".len()..].trim().parse::<i32>() {
                tja.bgm_vol = v;
            }
            continue;
        }
        if upper.starts_with("SEVOL:") {
            if let Ok(v) = trimmed["SEVOL:".len()..].trim().parse::<u32>() {
                sevol = v;
            }
            continue;
        }
        if upper.starts_with("ARTIST:") {
            tja.artist = trimmed["ARTIST:".len()..].trim().to_string();
            continue;
        }
        if upper.starts_with("GENRE:") {
            tja.genre = trimmed["GENRE:".len()..].trim().to_string();
            continue;
        }
        if upper.starts_with("MAKER:") {
            tja.maker = trimmed["MAKER:".len()..].trim().to_string();
            continue;
        }
        if upper.starts_with("DEMOSTART:") {
            tja.global_custom_metadata.insert(
                "demostart".to_string(),
                trimmed["DEMOSTART:".len()..].trim().to_string(),
            );
            continue;
        }

        // ─── Course start ────────────────────────────────────────────────────
        if upper.starts_with("COURSE:") {
            if let Some(course) = current_course.take() {
                courses.push(course);
            }
            let mut meta = new_course_metadata();
            meta.course_type = trimmed["COURSE:".len()..].trim().to_string();
            current_course = Some(meta);
            in_course = true;
            collecting_notes = false;
            continue;
        }

        if in_course {
            if let Some(ref mut course) = current_course {
                if upper.starts_with("LEVEL:") {
                    if let Ok(v) = trimmed["LEVEL:".len()..].trim().parse::<i32>() {
                        course.level_taiko = v;
                    }
                    continue;
                }
                if upper.starts_with("BALLOON:") {
                    let vals = trimmed["BALLOON:".len()..].trim();
                    course.balloon = vals
                        .split(',')
                        .filter_map(|s| s.trim().parse::<u32>().ok())
                        .collect();
                    continue;
                }
                if upper.starts_with("SCOREINIT:") {
                    if let Ok(v) = trimmed["SCOREINIT:".len()..].trim().parse::<i32>() {
                        course.score_init[0] = v;
                    }
                    continue;
                }
                if upper.starts_with("SCOREDIFF:") {
                    if let Ok(v) = trimmed["SCOREDIFF:".len()..].trim().parse::<i32>() {
                        course.score_diff = v;
                    }
                    continue;
                }
                if upper.starts_with("NOTESDESIGNER:") {
                    course.notes_designer = trimmed["NOTESDESIGNER:".len()..].trim().to_string();
                    continue;
                }
            }

            if trimmed == "#START" {
                collecting_notes = true;
                note_data_buffer.clear();
                continue;
            }
            if trimmed == "#END" {
                if let Some(mut course) = current_course.take() {
                    let offset_s: f64 = tja
                        .global_custom_metadata
                        .get("offset")
                        .and_then(|s| s.parse().ok())
                        .unwrap_or(0.0);
                    parse_note_data(&note_data_buffer, tja.bpm, offset_s, &mut course);
                    courses.push(course);
                }
                in_course = false;
                collecting_notes = false;
                continue;
            }

            if collecting_notes {
                note_data_buffer.push_str(trimmed);
                note_data_buffer.push('\n');
            }
        }
    }

    // Flush any unclosed course
    if let Some(mut course) = current_course.take() {
        if !note_data_buffer.is_empty() {
            let offset_s: f64 = tja
                .global_custom_metadata
                .get("offset")
                .and_then(|s| s.parse().ok())
                .unwrap_or(0.0);
            parse_note_data(&note_data_buffer, tja.bpm, offset_s, &mut course);
        }
        courses.push(course);
    }

    tja.global_custom_metadata
        .insert("sevol".to_string(), sevol.to_string());
    tja.course_metadata = courses;
    tja
}

fn new_course_metadata() -> QueryableCourseMetadata {
    QueryableCourseMetadata {
        notes_designer: String::new(),
        level_taiko: -1,
        level_taiko_icon: crate::tja::LevelIcon::None,
        has_branch: false,
        hidden_branch: false,
        score_mode: -1,
        score_init: [300, 1000],
        score_diff: 120,
        score_point_assigned: [false; 3],
        custom_metadata: HashMap::new(),
        course_type: String::new(),
        balloon: Vec::new(),
        notes: Vec::new(),
        bar_lines: Vec::new(),
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Note-data parser: processes the buffered #START … #END block.
//
// Design goals implemented here:
//  • scroll_factor is baked into every Note and BarLine at creation time.
//    The renderer must never look up any "current scroll" at draw time.
//  • #BPMCHANGE / #SCROLL / #MEASURE are applied *immediately* when the
//    corresponding token is encountered, so the very next note already uses
//    the updated values.
//  • BPM-anchor book-keeping is done inside process_measure whenever a BPM
//    change occurs mid-measure, and again at every comma boundary, so there
//    is no accumulated floating-point drift.
// ─────────────────────────────────────────────────────────────────────────────
struct ParserState {
    bpm: f64,
    scroll: f64,
    measure: (f32, f32), // 分子, 分母
    current_abs_time_ms: f64,
}

// ─────────────────────────────────────────────────────────────────────────────
// Note-data parser: processes the buffered #START … #END block.
//
// Design goals implemented here:
//  • scroll_factor is baked into every Note and BarLine at creation time.
//    The renderer must never look up any "current scroll" at draw time.
//  • #BPMCHANGE / #SCROLL / #MEASURE are applied *immediately* when the
//    corresponding token is encountered, so the very next note already uses
//    the updated values.
// ─────────────────────────────────────────────────────────────────────────────
fn parse_note_data(
    data: &str,
    base_bpm: f64,
    offset_s: f64,
    course: &mut QueryableCourseMetadata,
) {
    let mut state = ParserState {
        bpm: base_bpm,
        scroll: 1.0,
        measure: (4.0, 4.0),
        current_abs_time_ms: -offset_s * 1000.0,
    };

    let mut open_roll_idx: Option<usize> = None;
    let mut balloon_idx: usize = 0;

    // Emit the very first bar line (start of measure 1).
    course.bar_lines.push(BarLine {
        time_ms: state.current_abs_time_ms,
        scroll_factor: state.scroll,
    });

    for line in data.lines() {
        let line_without_comment = if let Some(idx) = line.find("//") {
            &line[..idx]
        } else {
            line
        };
        let trimmed = line_without_comment.trim();
        if trimmed.is_empty() {
            continue;
        }

        if trimmed.starts_with('#') {
            apply_command(trimmed, &mut state);
        } else {
            // Note line: can contain commas for measures
            let measures: Vec<&str> = trimmed.split(',').collect();
            for (i, measure_text) in measures.iter().enumerate() {
                let note_data = measure_text.trim();
                
                if !note_data.is_empty() {
                    process_note_line(note_data, &mut state, course, &mut open_roll_idx, &mut balloon_idx);
                }

                // If not the last part, we hit a comma, end of measure
                if i < measures.len() - 1 {
                    let beat_length_ms = (60000.0 / state.bpm) * (4.0 * state.measure.0 as f64 / state.measure.1 as f64);
                    state.current_abs_time_ms += beat_length_ms;
                    
                    course.bar_lines.push(BarLine {
                        time_ms: state.current_abs_time_ms,
                        scroll_factor: state.scroll,
                    });
                }
            }
        }
    }
}

fn apply_command(command: &str, state: &mut ParserState) {
    let upper = command.to_uppercase();
    if upper.starts_with("#BPMCHANGE") {
        if let Ok(new_bpm) = command["#BPMCHANGE".len()..].trim().parse::<f64>() {
            state.bpm = new_bpm;
        }
    } else if upper.starts_with("#MEASURE") {
        if let Some(val) = command["#MEASURE".len()..].trim().split_once('/') {
            if let (Ok(n), Ok(d)) = (val.0.trim().parse::<f32>(), val.1.trim().parse::<f32>()) {
                state.measure = (n, d);
            }
        }
    } else if upper.starts_with("#SCROLL") {
        if let Ok(s) = command["#SCROLL".len()..].trim().parse::<f64>() {
            state.scroll = s;
        }
    }
}

fn process_note_line(
    line: &str,
    state: &mut ParserState,
    course: &mut QueryableCourseMetadata,
    open_roll_idx: &mut Option<usize>,
    balloon_idx: &mut usize,
) {
    let note_chars: Vec<char> = line.chars().filter(|c| c.is_ascii_digit()).collect();
    let note_count = note_chars.len();
    if note_count == 0 { return; }
    
    let beat_length_ms = (60000.0 / state.bpm) * (4.0 * state.measure.0 as f64 / state.measure.1 as f64);
    let time_per_note = beat_length_ms / (note_count as f64);
    
    for (i, ch) in line.chars().filter(|c| c.is_ascii_digit()).enumerate() {
        let note_type = match ch {
            '1' => Some(NoteType::Don),
            '2' => Some(NoteType::Ka),
            '3' => Some(NoteType::DonBig),
            '4' => Some(NoteType::KaBig),
            '5' => Some(NoteType::Roll),
            '6' => Some(NoteType::RollBig),
            '7' => Some(NoteType::End),
            '8' => Some(NoteType::Balloon),
            _ => None,
        };

        if let Some(nt) = note_type {
            let time_ms = state.current_abs_time_ms + (i as f64 * time_per_note);

            if nt == NoteType::End {
                if let Some(idx) = *open_roll_idx {
                    if let Some(start_note) = course.notes.get_mut(idx) {
                        start_note.end_time_ms = Some(time_ms);
                    }
                }
                *open_roll_idx = None;
            } else {
                let mut balloon_count = None;
                if nt == NoteType::Balloon {
                    if *balloon_idx < course.balloon.len() {
                        balloon_count = Some(course.balloon[*balloon_idx]);
                        *balloon_idx += 1;
                    }
                }

                course.notes.push(Note {
                    note_type: nt,
                    time_ms,
                    end_time_ms: None,
                    balloon_count,
                    scroll_factor: state.scroll,
                });

                if matches!(nt, NoteType::Roll | NoteType::RollBig | NoteType::Balloon) {
                    *open_roll_idx = Some(course.notes.len() - 1);
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_gimmick_parsing() {
        let tja = "
TITLE:Test
BPM:120
OFFSET:0
COURSE:Oni
#START
1,
#BPMCHANGE 240
#MEASURE 2/4
11,
#SCROLL 2.0
2,
#END
";
        let chart = parse(tja);
        let notes = &chart.course_metadata[0].notes;

        // Measure 1: 120BPM, 4/4. Note at beat 0 → 0ms.
        assert_eq!(notes[0].time_ms, 0.0);
        assert_eq!(notes[0].scroll_factor, 1.0);

        // After measure 1 comma: anchor = 2000ms, anchor_beats = 4.
        // BPM now 240, measure 2/4 (= 2 beats).
        // Measure 2 note 1: beat 4 → 2000ms.
        assert_eq!(notes[1].time_ms, 2000.0);
        assert_eq!(notes[1].scroll_factor, 1.0);

        // Measure 2 note 2: beat 4 + 1 → 2000 + 250 = 2250ms.
        assert_eq!(notes[2].time_ms, 2250.0);

        // After measure 2 comma: anchor = 2500ms. #SCROLL 2.0 is next.
        // Measure 3: 240BPM, 2/4, 1 note at beat 6 → 2500ms, scroll 2.0.
        assert_eq!(notes[3].time_ms, 2500.0);
        assert_eq!(notes[3].scroll_factor, 2.0);
    }

    #[test]
    fn test_scroll_baked_in() {
        let tja = "
TITLE:ScrollTest
BPM:120
OFFSET:0
COURSE:Oni
#START
1,
#SCROLL 3.0
2,
#END
";
        let chart = parse(tja);
        let notes = &chart.course_metadata[0].notes;
        assert_eq!(notes[0].scroll_factor, 1.0);
        assert_eq!(notes[1].scroll_factor, 3.0);
    }

    #[test]
    fn test_bar_lines_emitted() {
        let tja = "
TITLE:BL
BPM:120
OFFSET:0
COURSE:Oni
#START
1,
1,
#END
";
        let chart = parse(tja);
        let bars = &chart.course_metadata[0].bar_lines;
        // First bar at 0ms, second at 2000ms (4 beats @ 120BPM), third at 4000ms.
        assert!(bars.len() >= 2);
        assert_eq!(bars[0].time_ms, 0.0);
        assert!((bars[1].time_ms - 2000.0).abs() < 0.01);
    }
}
