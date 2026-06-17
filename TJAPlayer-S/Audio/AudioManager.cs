using ManagedBass;
using System;

namespace TjaPlayer.Audio;

public class AudioManager : IDisposable
{
    public AudioManager()
    {
        try
        {
            // Initialize BASS with default device
            if (!Bass.Init())
            {
                throw new Exception("Failed to initialize BASS");
            }
        }
        catch (DllNotFoundException)
        {
            throw new Exception("bass.dll が見つかりません。http://www.un4seen.com/ から BASS をダウンロードし、実行ファイルと同じフォルダに配置してください。");
        }
    }

    public int LoadTrack(string path)
    {
        string fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (!System.IO.File.Exists(fullPath))
        {
            throw new System.IO.FileNotFoundException($"Audio file not found: {fullPath}");
        }

        int stream = Bass.CreateStream(fullPath);
        if (stream == 0) throw new Exception($"Failed to create stream: {Bass.LastError} (Path: {fullPath})");
        return stream;
    }

    public int PlayTrack(string path)
    {
        string fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (!System.IO.File.Exists(fullPath))
        {
            throw new System.IO.FileNotFoundException($"Audio file not found: {fullPath}");
        }

        int stream = Bass.CreateStream(fullPath);
        if (stream == 0) throw new Exception($"Failed to create stream: {Bass.LastError} (Path: {fullPath})");
        Bass.ChannelPlay(stream);
        return stream;
    }

    public double GetPositionSeconds(int streamHandle)
    {
        long bytes = Bass.ChannelGetPosition(streamHandle);
        return Bass.ChannelBytes2Seconds(streamHandle, bytes);
    }

    private readonly System.Collections.Generic.Dictionary<string, int> sampleCache = new();

    public void PlaySoundEffect(string path)
    {
        string fullPath = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (!System.IO.File.Exists(fullPath)) return;

        if (!sampleCache.TryGetValue(fullPath, out int sample))
        {
            // 最大8同時再生から32同時再生に増やして、高速な連打に対応
            sample = Bass.SampleLoad(fullPath, 0, 0, 32, BassFlags.Default);
            if (sample != 0) sampleCache[fullPath] = sample;
        }

        if (sample != 0)
        {
            int channel = Bass.SampleGetChannel(sample);
            Bass.ChannelPlay(channel);
        }
    }

    public void StopTrack(int streamHandle)
    {
        if (streamHandle != 0)
        {
            Bass.ChannelStop(streamHandle);
            Bass.StreamFree(streamHandle);
        }
    }

    public void Dispose()
    {
        foreach (var sample in sampleCache.Values)
        {
            Bass.SampleFree(sample);
        }
        sampleCache.Clear();
        Bass.Free();
    }
}
