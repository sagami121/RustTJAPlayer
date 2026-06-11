using ManagedBass;
using System;

namespace TjaPlayer.Audio;

public class AudioManager : IDisposable
{
    public AudioManager()
    {
        // Initialize BASS with default device
        if (!Bass.Init())
        {
            throw new Exception("Failed to initialize BASS");
        }
    }

    public int PlayTrack(string path)
    {
        int stream = Bass.CreateStream(path);
        if (stream == 0) throw new Exception($"Failed to create stream: {Bass.LastError}");
        Bass.ChannelPlay(stream);
        return stream;
    }

    public double GetPositionSeconds(int streamHandle)
    {
        long bytes = Bass.ChannelGetPosition(streamHandle);
        return Bass.ChannelBytes2Seconds(streamHandle, bytes);
    }

    public void PlaySoundEffect(string path)
    {
        // For SE, load as sample for efficient playback
        int sample = Bass.SampleLoad(path, 0, 0, 1, BassFlags.Default);
        if (sample != 0)
        {
            int channel = Bass.SampleGetChannel(sample);
            Bass.ChannelPlay(channel);
        }
    }

    public void Dispose()
    {
        Bass.Free();
    }
}
