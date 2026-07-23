using System;
using System.Threading;

namespace VoiceOutputDeviceChanger.Core;

internal sealed class VoiceAudioMixer
{
    private readonly object _gate = new();
    private VoiceCaptureStream[] _streams = Array.Empty<VoiceCaptureStream>();
    private float[] _scratch = Array.Empty<float>();

    public VoiceCaptureStream Register(int capacitySamples)
    {
        var stream = new VoiceCaptureStream(capacitySamples);
        lock (_gate)
        {
            VoiceCaptureStream[] current = _streams;
            var updated = new VoiceCaptureStream[current.Length + 1];
            Array.Copy(current, updated, current.Length);
            updated[current.Length] = stream;
            Volatile.Write(ref _streams, updated);
        }

        return stream;
    }

    public void Unregister(VoiceCaptureStream stream)
    {
        if (stream is null)
        {
            return;
        }

        lock (_gate)
        {
            VoiceCaptureStream[] current = _streams;
            int index = Array.IndexOf(current, stream);
            if (index < 0)
            {
                return;
            }

            var updated = new VoiceCaptureStream[current.Length - 1];
            Array.Copy(current, 0, updated, 0, index);
            Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
            Volatile.Write(ref _streams, updated);
        }

        stream.Clear();
    }

    public bool Mix(float[] destination, int sampleCount)
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (sampleCount < 0 || sampleCount > destination.Length || (sampleCount & 1) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        Array.Clear(destination, 0, sampleCount);
        EnsureScratchCapacity(sampleCount);

        bool readAny = false;
        VoiceCaptureStream[] streams = Volatile.Read(ref _streams);
        for (int streamIndex = 0; streamIndex < streams.Length; streamIndex++)
        {
            int read = streams[streamIndex].Read(_scratch, sampleCount);
            readAny |= read > 0;
            for (int sample = 0; sample < read; sample++)
            {
                destination[sample] += _scratch[sample];
            }
        }

        if (readAny)
        {
            for (int sample = 0; sample < sampleCount; sample++)
            {
                destination[sample] = Math.Max(-1f, Math.Min(1f, destination[sample]));
            }
        }

        return readAny;
    }

    public void Clear()
    {
        VoiceCaptureStream[] streams = Volatile.Read(ref _streams);
        for (int index = 0; index < streams.Length; index++)
        {
            streams[index].Clear();
        }
    }

    private void EnsureScratchCapacity(int sampleCount)
    {
        if (_scratch.Length < sampleCount)
        {
            _scratch = new float[sampleCount];
        }
    }
}
