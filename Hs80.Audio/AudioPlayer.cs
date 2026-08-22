using NAudio.Wave;

namespace Hs80.Audio;

public sealed class AudioPlayer
{
    private readonly object _gate = new();
    private IWavePlayer? _current;

    public void PlayWave(string path, double volume = 0.6)
    {
        Play(path, false, volume);
    }

    public void PlayMp3(string path, double volume = 0.6)
    {
        Play(path, true, volume);
    }

    public void Stop()
    {
        IWavePlayer? player;
        lock (_gate)
        {
            player = _current;
            _current = null;
        }
        try
        {
            player?.Stop();
        }
        catch
        {
        }
    }

    private void Play(string path, bool mp3, double volume)
    {
        try
        {
            Stop();
            IWaveProvider reader = mp3 ? new Mp3FileReader(path) : new AudioFileReader(path);
            var player = new WaveOut();
            player.Volume = (float)Math.Clamp(volume, 0.0, 1.0);
            player.PlaybackStopped += (_, _) =>
            {
                player.Dispose();
                ((IDisposable)reader).Dispose();
            };
            player.Init(reader);
            lock (_gate)
            {
                _current = player;
            }
            player.Play();
        }
        catch
        {
        }
    }
}
