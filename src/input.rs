/// Input module for handling key bindings.

use egui::Key;

/// Mapping of game actions to keys.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GameAction {
    HitLeft,  // Don (red) - corresponds to F or J
    HitRight, // Ka (blue) - corresponds to D or K
}

/// Returns the set of keys that trigger the given action.
pub fn action_keys(action: GameAction) -> &'static [Key] {
    match action {
        GameAction::HitLeft => &[Key::F, Key::J],
        GameAction::HitRight => &[Key::D, Key::K],
    }
}

/// Checks whether any of the keys for the given action were pressed this frame.
pub fn is_action_pressed(ctx: &egui::Context, action: GameAction) -> bool {
    let input = ctx.input(|i| i.clone());
    let keys = action_keys(action);
    keys.iter().any(|&k| input.key_pressed(k))
}