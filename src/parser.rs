use crate::models::{Note, NoteType};
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
    // Fallback se volume (stored here until used)
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
                // Store as negative ms for compatibility with old parser convention
                // (will be used during note parsing below)
                // We temporarily borrow a global_custom_metadata slot
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
            // store in custom metadata for future use
            tja.global_custom_metadata.insert("demostart".to_string(), trimmed["DEMOSTART:".len()..].trim().to_string());
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
                    course.balloon = vals.split(',').filter_map(|s| s.trim().parse::<u32>().ok()).collect();
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
                    let offset_s: f64 = tja.global_custom_metadata.get("offset")
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
            let offset_s: f64 = tja.global_custom_metadata.get("offset")
                .and_then(|s| s.parse().ok())
                .unwrap_or(0.0);
            parse_note_data(&note_data_buffer, tja.bpm, offset_s, &mut course);
        }
        courses.push(course);
    }

    // Store sevol in global metadata for callers
    tja.global_custom_metadata.insert("sevol".to_string(), sevol.to_string());
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
    }
}

fn parse_note_data(data: &str, base_bpm: f64, offset_s: f64, course: &mut QueryableCourseMetadata) {
    let mut bpm = base_bpm;
    let mut measure_beats = 4.0_f64;
    let mut current_scroll: f64 = 1.0;

    let initial_offset_ms = -offset_s * 1000.0;
    let mut anchor_time_ms = initial_offset_ms;
    let mut anchor_beats = 0.0_f64;
    let mut total_beats = 0.0_f64;

    let mut open_roll_idx: Option<usize> = None;
    let mut balloon_idx: usize = 0;

    let mut current_measure_text = String::new();

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

        if trimmed.starts_with('#') && !trimmed.contains(',') {
            let upper = trimmed.to_uppercase();
            if upper.starts_with("#BPMCHANGE") {
                if let Ok(new_bpm) = upper["#BPMCHANGE".len()..].trim().parse::<f64>() {
                    anchor_time_ms += (total_beats - anchor_beats) * (60000.0 / bpm);
                    anchor_beats = total_beats;
                    bpm = new_bpm;
                }
            } else if upper.starts_with("#MEASURE") {
                if let Some(val) = upper["#MEASURE".len()..].trim().split_once('/') {
                    if let (Ok(n), Ok(d)) = (val.0.trim().parse::<f64>(), val.1.trim().parse::<f64>()) {
                        measure_beats = n * 4.0 / d;
                    }
                }
            } else if upper.starts_with("#SCROLL") {
                if let Ok(s) = upper["#SCROLL".len()..].trim().parse::<f64>() {
                    current_scroll = s;
                }
            }
            continue;
        }

        let mut idx = 0usize;
        while idx < trimmed.len() {
            let ch = trimmed[idx..].chars().next().unwrap();
            let ch_len = ch.len_utf8();
            if ch == ',' {
                process_measure(
                    &current_measure_text,
                    &mut bpm,
                    &mut measure_beats,
                    &mut current_scroll,
                    &mut anchor_time_ms,
                    &mut anchor_beats,
                    &mut total_beats,
                    course,
                    &mut open_roll_idx,
                    &mut balloon_idx,
                );
                current_measure_text.clear();
                anchor_time_ms += (total_beats - anchor_beats) * (60000.0 / bpm);
                anchor_beats = total_beats;
            } else {
                current_measure_text.push(ch);
            }
            idx += ch_len;
        }
    }

    if !current_measure_text.trim().is_empty() {
        process_measure(
            &current_measure_text,
            &mut bpm,
            &mut measure_beats,
            &mut current_scroll,
            &mut anchor_time_ms,
            &mut anchor_beats,
            &mut total_beats,
            course,
            &mut open_roll_idx,
            &mut balloon_idx,
        );
    }
}

fn process_measure(
    measure: &str,
    bpm: &mut f64,
    measure_beats: &mut f64,
    current_scroll: &mut f64,
    anchor_time_ms: &mut f64,
    anchor_beats: &mut f64,
    total_beats: &mut f64,
    course: &mut QueryableCourseMetadata,
    open_roll_idx: &mut Option<usize>,
    balloon_idx: &mut usize,
) {
    #[derive(Debug)]
    enum Token {
        Command(String),
        NoteChar(char),
    }

    let mut tokens: Vec<Token> = Vec::new();
    let bytes = measure.as_bytes();
    let mut i = 0usize;
    while i < bytes.len() {
        let b = bytes[i];
        if b == b'#' {
            let start = i;
            i += 1;
            while i < bytes.len() && bytes[i] != b'#' && bytes[i] != b'\n' && bytes[i] != b'\r' {
                i += 1;
            }
            tokens.push(Token::Command(measure[start..i].to_string()));
            continue;
        }
        if b.is_ascii_whitespace() {
            i += 1;
            continue;
        }
        let ch = measure[i..].chars().next().unwrap();
        let ch_len = ch.len_utf8();
        if ch.is_ascii_digit() {
            tokens.push(Token::NoteChar(ch));
        }
        i += ch_len;
    }

    let note_count = tokens.iter().filter(|t| matches!(t, Token::NoteChar(_))).count();
    if note_count == 0 {
        *total_beats += *measure_beats;
        return;
    }

    let beats_per_note = *measure_beats / (note_count as f64);

    for token in tokens {
        match token {
            Token::Command(cmd_text) => {
                let upper = cmd_text.to_uppercase();
                if upper.starts_with("#BPMCHANGE") {
                    if let Some(arg) = cmd_text["#BPMCHANGE".len()..].trim().split_whitespace().next() {
                        if let Ok(new_bpm) = arg.parse::<f64>() {
                            *anchor_time_ms += (*total_beats - *anchor_beats) * (60000.0 / *bpm);
                            *anchor_beats = *total_beats;
                            *bpm = new_bpm;
                        }
                    }
                } else if upper.starts_with("#SCROLL") {
                    if let Some(arg) = cmd_text["#SCROLL".len()..].trim().split_whitespace().next() {
                        if let Ok(s) = arg.parse::<f64>() {
                            *current_scroll = s;
                        }
                    }
                } else if upper.starts_with("#MEASURE") {
                    if let Some(arg) = cmd_text["#MEASURE".len()..].trim().split_whitespace().next() {
                        if let Some((num_s, den_s)) = arg.split_once('/') {
                            if let (Ok(n), Ok(d)) = (num_s.trim().parse::<f64>(), den_s.trim().parse::<f64>()) {
                                *measure_beats = n * 4.0 / d;
                            }
                        }
                    }
                }
            }
            Token::NoteChar(ch) => {
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
                    let time_ms = *anchor_time_ms + (*total_beats - *anchor_beats) * (60000.0 / *bpm);

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

                        let note = Note {
                            note_type: nt,
                            time_ms,
                            end_time_ms: None,
                            balloon_count,
                            scroll_factor: *current_scroll,
                        };

                        course.notes.push(note);

                        if matches!(nt, NoteType::Roll | NoteType::RollBig | NoteType::Balloon) {
                            *open_roll_idx = Some(course.notes.len() - 1);
                        }
                    }
                }

                *total_beats += beats_per_note;
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

        assert_eq!(notes[0].time_ms, 0.0);
        assert_eq!(notes[0].scroll_factor, 1.0);
        assert_eq!(notes[1].time_ms, 2000.0);
        assert_eq!(notes[1].scroll_factor, 1.0);
        assert_eq!(notes[2].time_ms, 2250.0);
        assert_eq!(notes[3].time_ms, 2500.0);
        assert_eq!(notes[3].scroll_factor, 2.0);
    }
}
