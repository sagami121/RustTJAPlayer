#![allow(dead_code)]
#[derive(Debug, Clone)]
pub struct ScoreIniInfo {
    pub last_update_time: u64,
    pub file_size: u64,
}

#[derive(Debug, Clone)]
pub struct FileInfo {
    pub absolute_file_path: String,
    pub absolute_folder_path: String,
    pub last_update_time: u64,
    pub file_size: u64,
}

#[derive(Debug, Clone)]
pub struct SkillInfo {
    pub drums: f64,
    pub guitar: f64,
    pub bass: f64,
}

#[derive(Debug, Clone)]
pub struct History {
    pub lines: [String; 7],
}

#[derive(Debug, Clone)]
pub struct ChartInfo {
    pub title: String,
    pub artist: String,
    pub comment: String,
    pub genre: String,
    pub preimage: String,
    pub premovie: String,
    pub presound: String,
    pub background: String,
    pub level: [i32; 3], // Represents STDGBVALUE
    pub max_skill: SkillInfo,
    pub full_combo: [bool; 3],
    pub play_count: [i32; 3],
    pub history: History,
    pub hide_level: bool,
    pub bpm: f64,
    pub base_bpm: f64,
    pub min_bpm: f64,
    pub max_bpm: f64,
    pub duration: i32,
    pub bgm_file_name: String,
    pub song_vol: i32,
    pub demo_bgm_offset: i32,
    pub has_branch: Vec<bool>,
    pub high_score: i32,
    pub high_scores: Vec<i32>,
    pub subtitle: String,
    pub n_level: Vec<i32>,
    pub clear_status: [i32; 5],
    pub score_rank: [i32; 5],
    pub level_icons: Vec<i32>, // Represents ELevelIcon
    pub n_life: i32,
    pub n_total_floor: i32,
    pub tower_type: String,
    pub dan_tick: i32,
    pub dan_tick_color: String,
    pub exam_results: Vec<Vec<i32>>,
}

#[derive(Debug, Clone)]
pub struct Score {
    pub score_ini_info: ScoreIniInfo,
    pub file_info: FileInfo,
    pub chart_info: ChartInfo,
    pub has_cached_song_db: bool,
}

impl Default for Score {
    fn default() -> Self {
        Self {
            score_ini_info: ScoreIniInfo { last_update_time: 0, file_size: 0 },
            file_info: FileInfo {
                absolute_file_path: String::new(),
                absolute_folder_path: String::new(),
                last_update_time: 0,
                file_size: 0,
            },
            chart_info: ChartInfo {
                title: String::new(),
                artist: String::new(),
                comment: String::new(),
                genre: String::new(),
                preimage: String::new(),
                premovie: String::new(),
                presound: String::new(),
                background: String::new(),
                level: [0; 3],
                max_skill: SkillInfo { drums: 0.0, guitar: 0.0, bass: 0.0 },
                full_combo: [false; 3],
                play_count: [0; 3],
                history: History { lines: Default::default() },
                hide_level: false,
                bpm: 120.0,
                base_bpm: 120.0,
                min_bpm: 120.0,
                max_bpm: 120.0,
                duration: 0,
                bgm_file_name: String::new(),
                song_vol: 100,
                demo_bgm_offset: 0,
                has_branch: Vec::new(),
                high_score: 0,
                high_scores: Vec::new(),
                subtitle: String::new(),
                n_level: vec![-1, -1, -1, -1, -1, -1, -1],
                clear_status: [0; 5],
                score_rank: [0; 5],
                level_icons: vec![0, 0, 0, 0, 0, 0, 0],
                n_life: 5,
                n_total_floor: 140,
                tower_type: String::new(),
                dan_tick: 0,
                dan_tick_color: String::new(),
                exam_results: Vec::new(),
            },
            has_cached_song_db: false,
        }
    }
}
