use crate::models::{Note, NoteType, TjaChart, TjaHeader, CourseData};

/// Parses a TJA formatted string into a TjaChart.
pub fn parse(tja: &str) -> TjaChart {
    let mut header = TjaHeader::default();
    let mut courses: Vec<CourseData> = Vec::new();
    let mut current_course: Option<CourseData> = None;
    let mut in_course = false;
    let mut collecting_notes = false;
    let mut note_data_buffer: String = String::new();

    for line in tja.lines() {
        // Strip comments starting with //
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

        // Header parsing
        if upper.starts_with("TITLE:") {
            header.title = Some(trimmed["TITLE:".len()..].trim().to_string());
            continue;
        }
        if upper.starts_with("BPM:") {
            if let Ok(val) = trimmed["BPM:".len()..].trim().parse::<f64>() {
                header.bpm = Some(val);
            }
            continue;
        }
        if upper.starts_with("OFFSET:") {
            if let Ok(val) = trimmed["OFFSET:".len()..].trim().parse::<f64>() {
                header.offset = Some(val);
            }
            continue;
        }
        if upper.starts_with("WAVE:") {
            header.wave = Some(trimmed["WAVE:".len()..].trim().to_string());
            continue;
        }
        if upper.starts_with("SONGVOL:") {
            if let Ok(val) = trimmed["SONGVOL:".len()..].trim().parse::<u32>() {
                header.songvol = val;
            }
            continue;
        }
        if upper.starts_with("SEVOL:") {
            if let Ok(val) = trimmed["SEVOL:".len()..].trim().parse::<u32>() {
                header.sevol = val;
            }
            continue;
        }

        if upper.starts_with("COURSE:") {
            if let Some(course) = current_course.take() {
                courses.push(course);
            }
            current_course = Some(CourseData::default());
            in_course = true;
            collecting_notes = false;
            let course_type_val = trimmed["COURSE:".len()..].trim().to_string();
            if let Some(ref mut course) = current_course {
                course.course_type = Some(course_type_val);
            }
            continue;
        }

        if in_course {
            if let Some(ref mut course) = current_course {
                if upper.starts_with("LEVEL:") {
                    if let Ok(val) = trimmed["LEVEL:".len()..].trim().parse::<u32>() {
                        course.level = Some(val);
                    }
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
                    parse_note_data(&note_data_buffer, &header, &mut course);
                    courses.push(course);
                }
                in_course = false;
                collecting_notes = false;
                continue;
            }

            if collecting_notes {
                // Keep commands and note data in the buffer
                note_data_buffer.push_str(trimmed);
                note_data_buffer.push('\n');
                continue;
            }
        } else if upper.starts_with("LEVEL:") {
            if let Ok(val) = trimmed["LEVEL:".len()..].trim().parse::<u32>() {
                header.level = Some(val);
            }
            continue;
        }
    }

    if let Some(mut course) = current_course.take() {
        if !note_data_buffer.is_empty() {
            parse_note_data(&note_data_buffer, &header, &mut course);
        }
        courses.push(course);
    }

    TjaChart { header, courses }
}

/// Parses the note data while maintaining strict timing and avoiding drift.
fn parse_note_data(data: &str, header: &TjaHeader, course: &mut CourseData) {
    let mut bpm = header.bpm.unwrap_or(120.0);
    let mut measure_beats = 4.0;
    let mut current_scroll: f64 = 1.0;

    // OFFSET (in seconds in header) -> ms
    let initial_offset_ms = -header.offset.unwrap_or(0.0) * 1000.0;

    // Anchors for converting beats to ms accounting for BPM changes
    let mut anchor_time_ms = initial_offset_ms;
    let mut anchor_beats = 0.0;
    let mut total_beats = 0.0;

    // Accumulate characters until a comma (end of measure)
    let mut current_measure_text = String::new();

    for line in data.lines() {
        // Strip comments starting with //
        let line_without_comment = if let Some(idx) = line.find("//") {
            &line[..idx]
        } else {
            line
        };
        let trimmed = line_without_comment.trim();
        if trimmed.is_empty() { continue; }

        // Allow standalone commands on their own lines
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

        // Process the line character-by-character, splitting measures on commas
        let mut idx = 0usize;
        while idx < trimmed.len() {
            let ch = trimmed[idx..].chars().next().unwrap();
            let ch_len = ch.len_utf8();
            if ch == ',' {
                // Process the accumulated measure text
                process_measure(&current_measure_text, &mut bpm, &mut measure_beats, &mut current_scroll, &mut anchor_time_ms, &mut anchor_beats, &mut total_beats, course);
                current_measure_text.clear();
                
                // Advance anchor to the end of the measure to prevent drift
                anchor_time_ms += (total_beats - anchor_beats) * (60000.0 / bpm);
                anchor_beats = total_beats;

                idx += ch_len;
                continue;
            } else {
                current_measure_text.push(ch);
                idx += ch_len;
            }
        }
    }

    // Process any remaining measure that wasn't terminated by a comma
    if !current_measure_text.trim().is_empty() {
        process_measure(&current_measure_text, &mut bpm, &mut measure_beats, &mut current_scroll, &mut anchor_time_ms, &mut anchor_beats, &mut total_beats, course);
    }
}

// Helper: tokenizes a measure string (which may contain inline commands) and emits notes with exact timing
fn process_measure(measure: &str, bpm: &mut f64, measure_beats: &mut f64, current_scroll: &mut f64, anchor_time_ms: &mut f64, anchor_beats: &mut f64, total_beats: &mut f64, course: &mut CourseData) {
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
            // collect until next '#' or newline or end
            while i < bytes.len() && bytes[i] != b'#' && bytes[i] != b'\n' && bytes[i] != b'\r' {
                i += 1;
            }
            let cmd = &measure[start..i];
            tokens.push(Token::Command(cmd.to_string()));
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
                    '7' => Some(NoteType::Balloon),
                    '8' => Some(NoteType::End),
                    _ => None,
                };

                if let Some(nt) = note_type {
                    let time_ms = *anchor_time_ms + (*total_beats - *anchor_beats) * (60000.0 / *bpm);
                    course.notes.push(Note { note_type: nt, time_ms, scroll_factor: *current_scroll });
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
        let notes = &chart.courses[0].notes;
        
        // Note 1: 120BPM, 4/4, beat 0. time = 0ms.
        assert_eq!(notes[0].time_ms, 0.0);
        assert_eq!(notes[0].scroll_factor, 1.0);

        // Measure 1 ends. total_beats = 4.0.
        // anchor_time_ms = 0 + 4 * (60000/120) = 2000ms.
        // anchor_beats = 4.0. BPM = 240. measure_beats = 2.0.

        // Note 2: 240BPM, 2/4, note_count=2, beat 4.0. time = 2000ms.
        assert_eq!(notes[1].time_ms, 2000.0);
        assert_eq!(notes[1].scroll_factor, 1.0);

        // Note 3: 240BPM, 2/4, beat 4.0 + (2.0/2) = 5.0. 
        // time = 2000 + (5.0 - 4.0) * (60000/240) = 2000 + 1 * 250 = 2250ms.
        assert_eq!(notes[2].time_ms, 2250.0);
        
        // Measure 2 ends. total_beats = 6.0.
        // anchor_time_ms = 2000 + (6.0 - 4.0) * (60000/240) = 2500ms.
        // anchor_beats = 6.0.

        // Note 4: 240BPM, 2/4, note_count=1, beat 6.0. time = 2500ms.
        // scroll_factor = 2.0.
        assert_eq!(notes[3].time_ms, 2500.0);
        assert_eq!(notes[3].scroll_factor, 2.0);
    }
}
