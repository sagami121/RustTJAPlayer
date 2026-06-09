use std::fs::File;
use std::io::{BufReader, Cursor, Read};
use rodio::{Decoder, OutputStream, OutputStreamHandle, Sink, Source, Sample};
use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::Duration;

/// A source wrapper that tracks the number of samples played.
pub struct TrackedSource<S: Source> where S::Item: Sample {
    inner: S,
    counter: Arc<AtomicU64>,
}

impl<S: Source> Iterator for TrackedSource<S> where S::Item: Sample {
    type Item = S::Item;
    fn next(&mut self) -> Option<Self::Item> {
        let s = self.inner.next();
        if s.is_some() {
            self.counter.fetch_add(1, Ordering::SeqCst);
        }
        s
    }
}

impl<S: Source> Source for TrackedSource<S> where S::Item: Sample {
    fn current_frame_len(&self) -> Option<usize> { self.inner.current_frame_len() }
    fn channels(&self) -> u16 { self.inner.channels() }
    fn sample_rate(&self) -> u32 { self.inner.sample_rate() }
    fn total_duration(&self) -> Option<Duration> { self.inner.total_duration() }
}

/// Audio module for playing audio files using rodio.
pub struct AudioSystem {
    _stream: OutputStream,
    stream_handle: OutputStreamHandle,
    /// Global sound effect volume (1.0 = 100%)
    pub se_volume: f32,
}

impl AudioSystem {
    pub fn new() -> Option<Self> {
        if let Ok((stream, stream_handle)) = OutputStream::try_default() {
            Some(Self { 
                _stream: stream, 
                stream_handle,
                se_volume: 1.0,
            })
        } else {
            None
        }
    }

    /// Play an audio file and track its progress.
    pub fn play_tracked_file(&self, path: &std::path::Path) -> Option<(Sink, Arc<AtomicU64>, u32, u16)> {
        let file = File::open(path).ok()?;
        let source = Decoder::new(BufReader::new(file)).ok()?;
        let sample_rate = source.sample_rate();
        let channels = source.channels();
        
        let counter = Arc::new(AtomicU64::new(0));
        let tracked_source = TrackedSource {
            inner: source,
            counter: Arc::clone(&counter),
        };
        
        // Use a Sink but ensure it doesn't block the main thread
        let sink = Sink::try_new(&self.stream_handle).ok()?;
        sink.append(tracked_source);
        
        Some((sink, counter, sample_rate, channels))
    }

    /// Load sound data into memory.
    pub fn load_sound(&self, path: &std::path::Path) -> Option<Arc<Vec<u8>>> {
        let mut file = File::open(path).ok()?;
        let mut buffer = Vec::new();
        file.read_to_end(&mut buffer).ok()?;
        Some(Arc::new(buffer))
    }

    /// Play a sound from memory buffer.
    pub fn play_buffer(&self, buffer: Arc<Vec<u8>>) {
        let data: &[u8] = &buffer;
        let cursor = Cursor::new(data.to_vec());
        if let Ok(source) = Decoder::new(cursor) {
            // Detach immediately to avoid blocking
            if let Ok(sink) = Sink::try_new(&self.stream_handle) {
                sink.set_volume(self.se_volume);
                sink.append(source);
                sink.detach();
            }
        }
    }

    pub fn set_se_volume(&mut self, vol: u32) {
        self.se_volume = vol as f32 / 100.0;
    }
}
