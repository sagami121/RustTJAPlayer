#![allow(dead_code)]
use std::collections::HashMap;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Course {
    Normal,
    Expert,
    Master,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LevelIcon {
    Minus,
    None,
    Plus,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Side {
    Normal,
    Ex,
    Both,
}

#[derive(Debug, Clone)]
pub struct BpmChange {
    pub bpm: f64,
    pub bpm_change_time: f64,
    pub bpm_change_bmscroll_time: f64,
    pub bpm_change_course: Course,
    pub internal_no: i32,
    pub display_no: i32,
}

#[derive(Debug, Clone)]
pub struct JPosScroll {
    pub move_dt_ms: f64,
    pub orig_x: f64,
    pub orig_y: f64,
    pub move_dx: f64,
    pub move_dy: f64,
    pub internal_no: i32,
    pub display_no: i32,
}

#[derive(Debug, Clone)]
pub struct WavInfo {
    pub use_as_bgm: bool,
    pub channels: Vec<i32>,
    pub chip_size: i32,
    pub position: i32,
    pub song_vol: i32,
    pub internal_no: i32,
    pub display_no: i32,
    pub comment: String,
    pub file_name: String,
    pub is_bass_sound: bool,
    pub is_guitar_sound: bool,
    pub is_drums_sound: bool,
    pub is_se_sound: bool,
    pub is_bgm_sound: bool,
}

#[derive(Debug, Clone)]
pub struct DanSong {
    pub title: String,
    pub subtitle: String,
    pub file_name: String,
    pub genre: String,
    pub score_init: i32,
    pub score_diff: i32,
    pub level: i32,
    pub difficulty: i32,
    pub show_title: bool,
    pub wave: Option<WavInfo>,
}

#[derive(Debug, Clone)]
pub struct BranchPointInfo {
    pub measure_count: i32,
    pub time_db: f64,
    pub bm_scroll_time: f64,
    pub bpm: f64,
    pub measure_s: f32,
    pub measure_m: f32,
}

#[derive(Debug, Clone)]
pub struct BranchScrollState {
    pub scroll: f64,
    pub scroll_y: f64,
    pub scroll_dir: i32,
    pub barline_cue: [i32; 2],
    pub move_wait_time: f64,
    pub appear_time: f64,
    pub gogo_time: bool,
}

#[derive(Debug, Clone)]
pub struct QueryableCourseMetadata {
    pub notes_designer: String,
    pub level_taiko: i32,
    pub level_taiko_icon: LevelIcon,
    pub has_branch: bool,
    pub hidden_branch: bool,
    pub score_mode: i32,
    pub score_init: [i32; 2],
    pub score_diff: i32,
    pub score_point_assigned: [bool; 3],
    pub custom_metadata: HashMap<String, String>,
    pub course_type: String,
    pub balloon: Vec<u32>,
    pub notes: Vec<crate::models::Note>,
}

#[derive(Debug, Clone)]
pub struct Tja {
    pub artist: String,
    pub background: String,
    pub base_bpm: f64,
    pub bpm: f64,
    pub min_bpm: f64,
    pub max_bpm: f64,
    pub comment: String,
    pub genre: String,
    pub maker: String,
    pub explicit: bool,
    pub select_bg: String,
    pub hidden_level: bool,
    pub side: Side,
    pub life: i32,
    pub tower_type: String,
    pub dan_tick: i32,
    
    pub bpm_list: Vec<BpmChange>,
    pub wav_list: HashMap<i32, WavInfo>,
    pub jpos_scroll_list: Vec<JPosScroll>,
    pub dan_songs: Vec<DanSong>,
    
    pub course_metadata: Vec<QueryableCourseMetadata>,
    pub player_side_metadata: QueryableCourseMetadata,
    pub global_custom_metadata: HashMap<String, String>,

    pub title: String,
    pub subtitle: String,
    pub bgm_path: String,
    pub bgm_vol: i32,
}

impl Default for Tja {
    fn default() -> Self {
        Self {
            artist: String::new(),
            background: String::new(),
            base_bpm: 120.0,
            bpm: 120.0,
            min_bpm: 120.0,
            max_bpm: 120.0,
            comment: String::new(),
            genre: String::new(),
            maker: String::new(),
            explicit: false,
            select_bg: String::new(),
            hidden_level: false,
            side: Side::Both,
            life: 0,
            tower_type: String::new(),
            dan_tick: 0,
            bpm_list: Vec::new(),
            wav_list: HashMap::new(),
            jpos_scroll_list: Vec::new(),
            dan_songs: Vec::new(),
            course_metadata: Vec::new(),
            player_side_metadata: QueryableCourseMetadata {
                notes_designer: String::new(),
                level_taiko: -1,
                level_taiko_icon: LevelIcon::None,
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
            },
            global_custom_metadata: HashMap::new(),
            title: String::new(),
            subtitle: String::new(),
            bgm_path: String::new(),
            bgm_vol: 100,
        }
    }
}
