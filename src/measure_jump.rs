/// Manages measure jump state with easing.
pub struct MeasureJumpManager {
    current_measure: f32,
    target_measure: f32,
    total_measures: usize,
}

impl MeasureJumpManager {
    pub fn new(total_measures: usize) -> Self {
        Self {
            current_measure: 0.0,
            target_measure: 0.0,
            total_measures,
        }
    }

    pub fn update(&mut self, _dt: f32) {
        // Simple linear interpolation for now, can be swapped with easing functions
        let lerp_factor = 0.15;
        self.current_measure += (self.target_measure - self.current_measure) * lerp_factor;
    }

    pub fn jump_to(&mut self, measure: usize) {
        self.target_measure = (measure.min(self.total_measures - 1)) as f32;
    }

    pub fn move_relative(&mut self, delta: i32) {
        let new_target = (self.target_measure as i32 + delta).clamp(0, (self.total_measures - 1) as i32);
        self.target_measure = new_target as f32;
    }

    pub fn current_measure(&self) -> f32 {
        self.current_measure
    }

    pub fn target_measure(&self) -> usize {
        self.target_measure as usize
    }

    pub fn total_measures(&self) -> usize {
        self.total_measures
    }
}
