using System;
using System.Diagnostics;
using System.Threading;
using BepInEx.Logging;
using VoiceOutputDeviceChanger.Core;

namespace VoiceOutputDeviceChanger.Interop.WindowsAudio;

internal sealed class VoiceAudioRouter : IDisposable
{
    private const int CaptureCapacitySamples = 1 << 17;
    private static readonly long DefaultEndpointCheckIntervalTicks = Stopwatch.Frequency * 2;
    private readonly object _selectionGate = new();
    private readonly object _readinessGate = new();
    private readonly ManualLogSource _logger;
    private readonly VoiceAudioMixer _mixer = new();
    private readonly ManualResetEvent _stop = new(false);
    private readonly AutoResetEvent _reconfigure = new(false);
    private readonly AtomicRegistration<RoutingEpoch> _routingEpoch = new();
    private readonly Thread _worker;
    private readonly int _sampleRate;
    private string _selectedEndpointId;
    private int _selectionGeneration;
    private bool _suspended;
    private bool _disposed;

    public VoiceAudioRouter(ManualLogSource logger, int sampleRate, string selectedEndpointId)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        _sampleRate = sampleRate;
        _selectedEndpointId = selectedEndpointId ?? string.Empty;
        _worker = new Thread(RenderThreadMain)
        {
            IsBackground = true,
            Name = "VoiceOutputDeviceChanger WASAPI",
        };
        _worker.Start();
    }

    public bool IsReady => _routingEpoch.Read() is not null;

    public VoiceCaptureStream RegisterCapture()
    {
        ThrowIfDisposed();
        return _mixer.Register(CaptureCapacitySamples);
    }

    public void UnregisterCapture(VoiceCaptureStream stream)
    {
        _mixer.Unregister(stream);
    }

    public VoiceSubmissionCommit? TrySubmit(VoiceCaptureStream stream, float[] samples, int channels)
    {
        RoutingEpoch? epoch = _routingEpoch.Read();
        if (epoch is null || !stream.TryWrite(samples, channels))
        {
            return null;
        }

        if (!epoch.Usage.TryBegin())
        {
            stream.Clear();
            return null;
        }

        if (!_routingEpoch.IsCurrent(epoch))
        {
            epoch.Usage.End();
            stream.Clear();
            return null;
        }

        return new VoiceSubmissionCommit(epoch.Usage);
    }

    public void SelectEndpoint(string endpointId)
    {
        ThrowIfDisposed();
        lock (_selectionGate)
        {
            string normalizedEndpointId = endpointId ?? string.Empty;
            if (!_suspended && string.Equals(_selectedEndpointId, normalizedEndpointId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedEndpointId = normalizedEndpointId;
            _suspended = false;
            _selectionGeneration = unchecked(_selectionGeneration + 1);
        }

        SetNotReady();
        _reconfigure.Set();
    }

    public void Suspend()
    {
        ThrowIfDisposed();
        lock (_selectionGate)
        {
            if (_suspended)
            {
                return;
            }

            _suspended = true;
            _selectionGeneration = unchecked(_selectionGeneration + 1);
        }

        SetNotReady();
        _reconfigure.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SetNotReady();
        _stop.Set();
        _reconfigure.Set();
        if (_worker.Join(TimeSpan.FromSeconds(5)))
        {
            _stop.Dispose();
            _reconfigure.Dispose();
        }
        else
        {
            TryLog(LogLevel.Warning, "WASAPI render thread did not stop within five seconds; synchronization handles were left intact for safe process shutdown.");
        }
    }

    private void RenderThreadMain()
    {
        try
        {
            using ComApartmentScope apartment = ComApartmentScope.EnterMultithreaded();
            RunRenderLoop();
        }
        catch (Exception exception)
        {
            TryLog(LogLevel.Error, $"Remote voice render thread stopped unexpectedly; Unity output remains enabled. {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            SetNotReady();
        }
    }

    private void RunRenderLoop()
    {
        string? lastFailure = null;
        while (!_stop.WaitOne(0))
        {
            (string endpointId, int generation, bool suspended) = ReadSelection();
            bool reopenImmediately = false;
            if (suspended)
            {
                WaitHandle.WaitAny(new WaitHandle[] { _stop, _reconfigure });
                continue;
            }

            try
            {
                using WasapiRenderer renderer = WasapiRenderer.Open(_mixer, endpointId, _sampleRate);
                if (!TrySetReady(generation))
                {
                    continue;
                }

                if (lastFailure is not null)
                {
                    TryLog(LogLevel.Info, "Remote voice output recovered after a WASAPI initialization failure.");
                    lastFailure = null;
                }

                var waits = new[] { _stop, _reconfigure, renderer.RenderEvent };
                long nextDefaultEndpointCheck = Stopwatch.GetTimestamp() + DefaultEndpointCheckIntervalTicks;
                while (!_stop.WaitOne(0) && generation == ReadSelection().Generation)
                {
                    int signaled = WaitHandle.WaitAny(waits, TimeSpan.FromSeconds(2));
                    if (signaled == 0 || signaled == 1)
                    {
                        break;
                    }

                    if (signaled == 2)
                    {
                        renderer.Render();
                    }

                    if (string.IsNullOrEmpty(endpointId) &&
                        Stopwatch.GetTimestamp() >= nextDefaultEndpointCheck)
                    {
                        nextDefaultEndpointCheck = Stopwatch.GetTimestamp() + DefaultEndpointCheckIntervalTicks;
                        string currentDefaultEndpointId = AudioEndpointService.GetDefaultRenderEndpointId();
                        if (!string.Equals(renderer.EndpointId, currentDefaultEndpointId, StringComparison.Ordinal))
                        {
                            TryLog(LogLevel.Info, "Windows multimedia default output changed; reopening remote voice output.");
                            reopenImmediately = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                string failure = $"{exception.GetType().Name}: {exception.Message}";
                if (!string.Equals(lastFailure, failure, StringComparison.Ordinal))
                {
                    TryLog(LogLevel.Error, $"Remote voice WASAPI output is unavailable; Unity output remains enabled. {failure}");
                    lastFailure = failure;
                }
            }
            finally
            {
                SetNotReady();
            }

            if (!_stop.WaitOne(0) && !reopenImmediately)
            {
                WaitHandle.WaitAny(new WaitHandle[] { _stop, _reconfigure }, TimeSpan.FromSeconds(5));
            }
        }
    }

    private void TryLog(LogLevel level, string message)
    {
        try
        {
            _logger.Log(level, message);
        }
        catch
        {
            // Diagnostic failures must not terminate audio routing or shutdown.
        }
    }

    private (string EndpointId, int Generation, bool Suspended) ReadSelection()
    {
        lock (_selectionGate)
        {
            return (_selectedEndpointId, _selectionGeneration, _suspended);
        }
    }

    private bool TrySetReady(int generation)
    {
        lock (_selectionGate)
        {
            if (_suspended || _selectionGeneration != generation || _stop.WaitOne(0))
            {
                return false;
            }

            lock (_readinessGate)
            {
                var epoch = new RoutingEpoch();
                if (_routingEpoch.Exchange(epoch) is not null)
                {
                    throw new InvalidOperationException("A routing epoch was already active.");
                }

                return true;
            }
        }
    }

    private void SetNotReady()
    {
        lock (_readinessGate)
        {
            RoutingEpoch? epoch = _routingEpoch.Exchange(null);
            epoch?.Usage.Retire();
            _mixer.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VoiceAudioRouter));
        }
    }

    private sealed class RoutingEpoch
    {
        public AtomicUsageLease Usage { get; } = new();
    }
}

internal sealed class VoiceSubmissionCommit : IDisposable
{
    private AtomicUsageLease? _usage;

    public VoiceSubmissionCommit(AtomicUsageLease usage)
    {
        _usage = usage;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _usage, null)?.End();
    }
}
