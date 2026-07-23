using System;
using System.Threading;

namespace VoiceOutputDeviceChanger.Core;

internal sealed class AudioRingBuffer
{
    private readonly float[] _samples;
    private readonly int _mask;
    private int _readSequence;
    private int _writeSequence;

    public AudioRingBuffer(int capacitySamples)
    {
        if (capacitySamples < 2 || (capacitySamples & (capacitySamples - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacitySamples), "Capacity must be a power of two.");
        }

        _samples = new float[capacitySamples];
        _mask = capacitySamples - 1;
    }

    public int CapacitySamples => _samples.Length;

    public int AvailableSamples
    {
        get
        {
            int write = Volatile.Read(ref _writeSequence);
            int read = Volatile.Read(ref _readSequence);
            uint available = unchecked((uint)(write - read));
            return available <= _samples.Length ? (int)available : 0;
        }
    }

    public bool TryWriteStereo(float[] source, int channels)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (channels <= 0 || source.Length % channels != 0)
        {
            return false;
        }

        int frameCount = source.Length / channels;
        int sampleCount = checked(frameCount * 2);
        int write = _writeSequence;
        int read = Volatile.Read(ref _readSequence);
        uint available = unchecked((uint)(write - read));

        if (available > _samples.Length || sampleCount > _samples.Length - (int)available)
        {
            return false;
        }

        int destination = write & _mask;
        for (int frame = 0; frame < frameCount; frame++)
        {
            int sourceOffset = frame * channels;
            float left = source[sourceOffset];
            float right = channels == 1 ? left : source[sourceOffset + 1];
            _samples[destination] = left;
            _samples[(destination + 1) & _mask] = right;
            destination = (destination + 2) & _mask;
        }

        Volatile.Write(ref _writeSequence, unchecked(write + sampleCount));
        return true;
    }

    public int Read(float[] destination, int sampleCount)
    {
        return Read(destination, sampleCount, beforePublish: null);
    }

    internal int Read(float[] destination, int sampleCount, Action? beforePublish)
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (sampleCount < 0 || sampleCount > destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        int read = Volatile.Read(ref _readSequence);
        int write = Volatile.Read(ref _writeSequence);
        uint available = unchecked((uint)(write - read));
        if (available > _samples.Length)
        {
            return 0;
        }

        int toRead = Math.Min(sampleCount, (int)available);
        int source = read & _mask;
        int first = Math.Min(toRead, _samples.Length - source);
        Array.Copy(_samples, source, destination, 0, first);
        Array.Copy(_samples, 0, destination, first, toRead - first);
        beforePublish?.Invoke();
        if (Interlocked.CompareExchange(ref _readSequence, unchecked(read + toRead), read) != read)
        {
            Array.Clear(destination, 0, toRead);
            return 0;
        }

        return toRead;
    }

    public void Clear()
    {
        int write = Volatile.Read(ref _writeSequence);
        Interlocked.Exchange(ref _readSequence, write);
    }
}
