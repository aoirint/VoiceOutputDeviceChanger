using System;

namespace VoiceOutputDeviceChanger.Core;

internal sealed class VoiceCaptureStream
{
    private readonly AudioRingBuffer _buffer;

    public VoiceCaptureStream(int capacitySamples)
    {
        _buffer = new AudioRingBuffer(capacitySamples);
    }

    public bool TryWrite(float[] samples, int channels)
    {
        return _buffer.TryWriteStereo(samples, channels);
    }

    public int Read(float[] destination, int sampleCount)
    {
        int read = _buffer.Read(destination, sampleCount);
        if ((read & 1) != 0)
        {
            throw new InvalidOperationException("A stereo stream returned a partial frame.");
        }

        return read;
    }

    public void Clear()
    {
        _buffer.Clear();
    }
}
