using System;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using VoiceOutputDeviceChanger.Core;

namespace VoiceOutputDeviceChanger.Interop.WindowsAudio;

internal sealed class WasapiRenderer : IDisposable
{
    private readonly VoiceAudioMixer _mixer;
    private readonly AutoResetEvent _renderEvent;
    private readonly IAudioClient _audioClient;
    private readonly IAudioRenderClient _renderClient;
    private readonly IMmDevice _device;
    private readonly uint _bufferFrameCount;
    private float[] _mixBuffer = Array.Empty<float>();
    private bool _started;
    private bool _disposed;

    private WasapiRenderer(
        VoiceAudioMixer mixer,
        AutoResetEvent renderEvent,
        IAudioClient audioClient,
        IAudioRenderClient renderClient,
        IMmDevice device,
        uint bufferFrameCount,
        string endpointId)
    {
        _mixer = mixer;
        _renderEvent = renderEvent;
        _audioClient = audioClient;
        _renderClient = renderClient;
        _device = device;
        _bufferFrameCount = bufferFrameCount;
        EndpointId = endpointId;
    }

    public WaitHandle RenderEvent => _renderEvent;

    public string EndpointId { get; }

    public static WasapiRenderer Open(VoiceAudioMixer mixer, string endpointId, int sampleRate)
    {
        IMmDeviceEnumerator? enumerator = null;
        IMmDevice? device = null;
        IAudioClient? audioClient = null;
        IAudioRenderClient? renderClient = null;
        AutoResetEvent? renderEvent = null;
        WasapiRenderer? renderer = null;
        IntPtr formatPointer = IntPtr.Zero;

        try
        {
            enumerator = (IMmDeviceEnumerator)(object)new MmDeviceEnumeratorComObject();
            if (string.IsNullOrEmpty(endpointId))
            {
                ComInterop.ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out device));
            }
            else
            {
                ComInterop.ThrowIfFailed(enumerator.GetDevice(endpointId, out device));
            }

            ComInterop.ThrowIfFailed(device.GetId(out string resolvedEndpointId));
            Guid audioClientId = typeof(IAudioClient).GUID;
            ComInterop.ThrowIfFailed(device.Activate(ref audioClientId, ClassContext.All, IntPtr.Zero, out object audioClientObject));
            audioClient = (IAudioClient)audioClientObject;

            WaveFormat format = WaveFormat.CreateFloatStereo(sampleRate);
            formatPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormat>());
            Marshal.StructureToPtr(format, formatPointer, false);
            const AudioClientStreamFlags flags = AudioClientStreamFlags.EventCallback |
                                                 AudioClientStreamFlags.AutoConvertPcm |
                                                 AudioClientStreamFlags.SourceDefaultQuality;
            ComInterop.ThrowIfFailed(audioClient.Initialize(AudioClientShareMode.Shared, flags, 0, 0, formatPointer, IntPtr.Zero));
            ComInterop.ThrowIfFailed(audioClient.GetBufferSize(out uint bufferFrameCount));

            renderEvent = new AutoResetEvent(false);
            ComInterop.ThrowIfFailed(audioClient.SetEventHandle(renderEvent.SafeWaitHandle.DangerousGetHandle()));

            Guid renderClientId = typeof(IAudioRenderClient).GUID;
            ComInterop.ThrowIfFailed(audioClient.GetService(ref renderClientId, out object renderClientObject));
            renderClient = (IAudioRenderClient)renderClientObject;

            renderer = new WasapiRenderer(mixer, renderEvent, audioClient, renderClient, device, bufferFrameCount, resolvedEndpointId);
            renderEvent = null;
            audioClient = null;
            renderClient = null;
            device = null;
            try
            {
                renderer.Start();
                return renderer;
            }
            catch (Exception startFailure)
            {
                try
                {
                    renderer.Dispose();
                }
                catch (Exception disposeFailure)
                {
                    throw new AggregateException("WASAPI start and cleanup both failed.", startFailure, disposeFailure);
                }

                throw;
            }
        }
        finally
        {
            if (formatPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(formatPointer);
            }

            renderEvent?.Dispose();
            ComInterop.Release(renderClient);
            ComInterop.Release(audioClient);
            ComInterop.Release(device);
            ComInterop.Release(enumerator);
        }
    }

    public void Render()
    {
        ThrowIfDisposed();
        ComInterop.ThrowIfFailed(_audioClient.GetCurrentPadding(out uint padding));
        if (padding > _bufferFrameCount)
        {
            throw new InvalidOperationException("WASAPI reported padding larger than its buffer.");
        }

        uint availableFrames = _bufferFrameCount - padding;
        if (availableFrames == 0)
        {
            return;
        }

        int sampleCount = checked((int)availableFrames * 2);
        if (_mixBuffer.Length < sampleCount)
        {
            _mixBuffer = new float[sampleCount];
        }

        bool hasAudio = _mixer.Mix(_mixBuffer, sampleCount);
        ComInterop.ThrowIfFailed(_renderClient.GetBuffer(availableFrames, out IntPtr destination));
        bool released = false;
        try
        {
            if (hasAudio)
            {
                Marshal.Copy(_mixBuffer, 0, destination, sampleCount);
                ComInterop.ThrowIfFailed(_renderClient.ReleaseBuffer(availableFrames, AudioClientBufferFlags.None));
            }
            else
            {
                ComInterop.ThrowIfFailed(_renderClient.ReleaseBuffer(availableFrames, AudioClientBufferFlags.Silent));
            }

            released = true;
        }
        finally
        {
            if (!released)
            {
                _renderClient.ReleaseBuffer(availableFrames, AudioClientBufferFlags.Silent);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Exception? firstFailure = null;
        if (_started)
        {
            CaptureFailure(() => ComInterop.ThrowIfFailed(_audioClient.Stop()), ref firstFailure);
            CaptureFailure(() => ComInterop.ThrowIfFailed(_audioClient.Reset()), ref firstFailure);
            _started = false;
        }

        CaptureFailure(() => ComInterop.Release(_renderClient), ref firstFailure);
        CaptureFailure(() => ComInterop.Release(_audioClient), ref firstFailure);
        CaptureFailure(() => ComInterop.Release(_device), ref firstFailure);
        CaptureFailure(_renderEvent.Dispose, ref firstFailure);
        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    private void Start()
    {
        ComInterop.ThrowIfFailed(_audioClient.Start());
        _started = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WasapiRenderer));
        }
    }

    private static void CaptureFailure(Action action, ref Exception? firstFailure)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }
    }
}
