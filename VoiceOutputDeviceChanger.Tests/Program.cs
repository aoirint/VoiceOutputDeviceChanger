using System;
using System.IO;
using System.Threading;
using VoiceOutputDeviceChanger.Core;
using VoiceOutputDeviceChanger.Interop.WindowsAudio;

namespace VoiceOutputDeviceChanger.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 3)
            {
                throw new ArgumentException(
                    "Pass the built plugin DLL, project version, artifact version, optional package ZIP, and optional --skip-live-audio.",
                    nameof(args));
            }

            string assemblyPath = Path.GetFullPath(args[0]);
            string expectedVersion = args[1];
            string expectedArtifactVersion = args[2];
            string? archivePath = null;
            bool skipLiveAudio = false;
            for (int index = 3; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--skip-live-audio", StringComparison.Ordinal))
                {
                    if (skipLiveAudio)
                    {
                        throw new ArgumentException("--skip-live-audio was supplied more than once.", nameof(args));
                    }

                    skipLiveAudio = true;
                    continue;
                }

                if (archivePath is not null)
                {
                    throw new ArgumentException("Only one package ZIP may be supplied.", nameof(args));
                }

                archivePath = Path.GetFullPath(args[index]);
            }

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Built plugin DLL was not found.", assemblyPath);
            }

            if (archivePath is not null && !File.Exists(archivePath))
            {
                throw new FileNotFoundException("Package ZIP was not found.", archivePath);
            }

            RejectsInvalidCapacity();
            ConvertsMonoAndPreservesWrappedOrder();
            RejectsOverflowWithoutPartialWrite();
            MixesAndClampsConcurrentStreams();
            ClearDropsQueuedSamples();
            ConcurrentClearCannotRestoreReadCursor();
            ReactivationInvalidatesCapturedRegistration();
            RetirementWaitsForActiveCommit();
            RoutingRetirementWaitsForActiveSubmissions();

            if (skipLiveAudio)
            {
                Console.WriteLine("All deterministic core audio tests passed; live Windows Core Audio tests skipped.");
            }
            else
            {
                EnumeratesWindowsAudioEndpoints();
                OpensAndRendersSilentWasapiBuffer();
                Console.WriteLine("All core audio, endpoint, and WASAPI tests passed.");
            }

            PackageContractTests.Run(
                assemblyPath,
                Directory.GetCurrentDirectory(),
                expectedVersion,
                expectedArtifactVersion,
                archivePath);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RejectsInvalidCapacity()
    {
        AssertThrows<ArgumentOutOfRangeException>(() => _ = new AudioRingBuffer(12));
    }

    private static void ConvertsMonoAndPreservesWrappedOrder()
    {
        var buffer = new AudioRingBuffer(8);
        Assert(buffer.TryWriteStereo(new[] { 1f, 2f, 3f }, 1), "Initial mono write failed.");

        var first = new float[4];
        AssertEqual(4, buffer.Read(first, first.Length), "Unexpected first read length.");
        AssertSequence(new[] { 1f, 1f, 2f, 2f }, first, "Mono conversion failed.");

        Assert(buffer.TryWriteStereo(new[] { 4f, 40f, 5f, 50f }, 2), "Wrapped stereo write failed.");
        var second = new float[6];
        AssertEqual(6, buffer.Read(second, second.Length), "Unexpected wrapped read length.");
        AssertSequence(new[] { 3f, 3f, 4f, 40f, 5f, 50f }, second, "Wrapped order changed.");
    }

    private static void RejectsOverflowWithoutPartialWrite()
    {
        var buffer = new AudioRingBuffer(4);
        Assert(buffer.TryWriteStereo(new[] { 0.1f, 0.2f }, 1), "Initial write failed.");
        Assert(!buffer.TryWriteStereo(new[] { 0.3f }, 1), "Overflow write should fail.");

        var output = new float[4];
        AssertEqual(4, buffer.Read(output, output.Length), "Overflow changed queued length.");
        AssertSequence(new[] { 0.1f, 0.1f, 0.2f, 0.2f }, output, "Overflow partially mutated the ring.");
    }

    private static void MixesAndClampsConcurrentStreams()
    {
        var mixer = new VoiceAudioMixer();
        VoiceCaptureStream first = mixer.Register(16);
        VoiceCaptureStream second = mixer.Register(16);
        Assert(first.TryWrite(new[] { 0.75f, -0.75f }, 2), "First stream write failed.");
        Assert(second.TryWrite(new[] { 0.75f, -0.75f }, 2), "Second stream write failed.");

        var output = new float[2];
        Assert(mixer.Mix(output, output.Length), "Mixer reported no input.");
        AssertSequence(new[] { 1f, -1f }, output, "Mixer did not clamp the sum.");

        mixer.Unregister(first);
        mixer.Unregister(second);
    }

    private static void ClearDropsQueuedSamples()
    {
        var mixer = new VoiceAudioMixer();
        VoiceCaptureStream stream = mixer.Register(8);
        Assert(stream.TryWrite(new[] { 0.5f }, 1), "Queued write failed.");
        mixer.Clear();

        var output = new float[2];
        Assert(!mixer.Mix(output, output.Length), "Cleared mixer reported queued input.");
        AssertSequence(new[] { 0f, 0f }, output, "Cleared mixer retained samples.");
    }

    private static void ConcurrentClearCannotRestoreReadCursor()
    {
        var buffer = new AudioRingBuffer(8);
        Assert(buffer.TryWriteStereo(new[] { 0.25f, 0.5f }, 1), "Raced write failed.");

        using var copied = new ManualResetEventSlim(false);
        using var resume = new ManualResetEventSlim(false);
        var output = new float[4];
        int read = -1;
        Exception? readerFailure = null;
        var reader = new Thread(
            () =>
            {
                try
                {
                    read = buffer.Read(
                        output,
                        output.Length,
                        () =>
                        {
                            copied.Set();
                            resume.Wait();
                        });
                }
                catch (Exception exception)
                {
                    readerFailure = exception;
                }
            });

        reader.Start();
        try
        {
            Assert(copied.Wait(TimeSpan.FromSeconds(2)), "Reader did not reach the publication barrier.");
            buffer.Clear();
        }
        finally
        {
            resume.Set();
        }

        Assert(reader.Join(TimeSpan.FromSeconds(2)), "Reader did not finish after the clear race.");
        if (readerFailure is not null)
        {
            throw new InvalidOperationException("Raced reader failed.", readerFailure);
        }

        AssertEqual(0, read, "A clear racing publication returned stale samples.");
        AssertEqual(0, buffer.AvailableSamples, "A raced reader restored the pre-clear cursor.");
        AssertSequence(new[] { 0f, 0f, 0f, 0f }, output, "A raced reader exposed stale samples.");

        Assert(buffer.TryWriteStereo(new[] { 0.75f }, 1), "Post-clear write failed.");
        var current = new float[2];
        AssertEqual(2, buffer.Read(current, current.Length), "Post-clear samples were unavailable.");
        AssertSequence(new[] { 0.75f, 0.75f }, current, "Post-clear samples were replaced by stale audio.");
    }

    private static void ReactivationInvalidatesCapturedRegistration()
    {
        var registrations = new AtomicRegistration<object>();
        var original = new object();
        var replacement = new object();
        registrations.Exchange(original);

        using var captured = new ManualResetEventSlim(false);
        using var resume = new ManualResetEventSlim(false);
        bool mayCommit = true;
        var callback = new Thread(
            () =>
            {
                object? registration = registrations.Read();
                captured.Set();
                resume.Wait();
                mayCommit = registration is not null && registrations.IsCurrent(registration);
            });

        callback.Start();
        try
        {
            Assert(captured.Wait(TimeSpan.FromSeconds(2)), "Callback did not capture its registration.");
            registrations.Exchange(null);
            registrations.Exchange(replacement);
        }
        finally
        {
            resume.Set();
        }

        Assert(callback.Join(TimeSpan.FromSeconds(2)), "Callback did not finish after reactivation.");
        Assert(!mayCommit, "A callback from the old activation committed against the replacement.");
        Assert(registrations.IsCurrent(replacement), "Reactivation did not retain the replacement registration.");
    }

    private static void RetirementWaitsForActiveCommit()
    {
        var lease = new AtomicCommitLease();
        Assert(lease.TryBegin(), "Initial callback could not begin its commit.");

        using var retirementStarted = new ManualResetEventSlim(false);
        using var retirementFinished = new ManualResetEventSlim(false);
        var retirement = new Thread(
            () =>
            {
                retirementStarted.Set();
                lease.Retire();
                retirementFinished.Set();
            });

        retirement.Start();
        Assert(retirementStarted.Wait(TimeSpan.FromSeconds(2)), "Retirement did not start.");
        Assert(
            !retirementFinished.Wait(TimeSpan.FromMilliseconds(100)),
            "Retirement completed before the active callback committed.");

        lease.End();
        Assert(retirement.Join(TimeSpan.FromSeconds(2)), "Retirement did not finish after the callback committed.");
        Assert(retirementFinished.IsSet, "Retirement completion was not published.");
        Assert(!lease.TryBegin(), "A retired registration accepted another callback commit.");
    }

    private static void RoutingRetirementWaitsForActiveSubmissions()
    {
        var lease = new AtomicUsageLease();
        Assert(lease.TryBegin(), "First routing submission could not begin.");
        Assert(lease.TryBegin(), "Second routing submission could not begin.");

        using var retirementStarted = new ManualResetEventSlim(false);
        using var retirementFinished = new ManualResetEventSlim(false);
        var retirement = new Thread(
            () =>
            {
                retirementStarted.Set();
                lease.Retire();
                retirementFinished.Set();
            });

        retirement.Start();
        Assert(retirementStarted.Wait(TimeSpan.FromSeconds(2)), "Routing retirement did not start.");
        Assert(
            !retirementFinished.Wait(TimeSpan.FromMilliseconds(100)),
            "Routing retirement completed while submissions were active.");

        lease.End();
        Assert(
            !retirementFinished.Wait(TimeSpan.FromMilliseconds(100)),
            "Routing retirement ignored a remaining submission.");

        lease.End();
        Assert(retirement.Join(TimeSpan.FromSeconds(2)), "Routing retirement did not finish after submissions ended.");
        Assert(!lease.TryBegin(), "A retired routing epoch accepted another submission.");
    }

    private static void EnumeratesWindowsAudioEndpoints()
    {
        AudioEndpointSnapshot snapshot = AudioEndpointService.EnumerateActiveRenderEndpoints();
        Assert(!string.IsNullOrWhiteSpace(snapshot.DefaultEndpointId), "Default render endpoint ID was empty.");
        Assert(snapshot.Endpoints.Count > 0, "No active Windows render endpoint was found.");

        bool foundDefault = false;
        foreach (AudioEndpoint endpoint in snapshot.Endpoints)
        {
            Assert(!string.IsNullOrWhiteSpace(endpoint.Id), "An active render endpoint ID was empty.");
            Assert(!string.IsNullOrWhiteSpace(endpoint.Name), "An active render endpoint name was empty.");
            foundDefault |= string.Equals(endpoint.Id, snapshot.DefaultEndpointId, StringComparison.Ordinal);
        }

        Assert(foundDefault, "The multimedia default was not present in the active render endpoint collection.");
    }

    private static void OpensAndRendersSilentWasapiBuffer()
    {
        var mixer = new VoiceAudioMixer();
        using WasapiRenderer renderer = WasapiRenderer.Open(mixer, string.Empty, 48000);
        Assert(!string.IsNullOrWhiteSpace(renderer.EndpointId), "WASAPI did not retain the resolved default endpoint ID.");
        Assert(renderer.RenderEvent.WaitOne(TimeSpan.FromSeconds(2)), "WASAPI did not signal its event-driven render buffer.");
        renderer.Render();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
        }
    }

    private static void AssertSequence(float[] expected, float[] actual, string message)
    {
        if (expected.Length != actual.Length)
        {
            throw new InvalidOperationException($"{message} Length mismatch.");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (Math.Abs(expected[index] - actual[index]) > 0.00001f)
            {
                throw new InvalidOperationException($"{message} Index {index}: expected {expected[index]}, got {actual[index]}.");
            }
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
