using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace VoiceOutputDeviceChanger.Interop.WindowsAudio;

internal sealed class AudioEndpoint
{
    public AudioEndpoint(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }
}

internal sealed class AudioEndpointSnapshot
{
    public AudioEndpointSnapshot(string defaultEndpointId, IReadOnlyList<AudioEndpoint> endpoints)
    {
        DefaultEndpointId = defaultEndpointId;
        Endpoints = endpoints;
    }

    public string DefaultEndpointId { get; }

    public IReadOnlyList<AudioEndpoint> Endpoints { get; }
}

internal static class AudioEndpointService
{
    private static PropertyKey DeviceFriendlyName => new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public static string GetDefaultRenderEndpointId()
    {
        using ComApartmentScope apartment = ComApartmentScope.EnterMultithreaded();
        IMmDeviceEnumerator? enumerator = null;
        IMmDevice? defaultDevice = null;

        try
        {
            enumerator = (IMmDeviceEnumerator)(object)new MmDeviceEnumeratorComObject();
            ComInterop.ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out defaultDevice));
            return GetId(defaultDevice);
        }
        finally
        {
            ComInterop.ReleaseAll(defaultDevice, enumerator);
        }
    }

    public static AudioEndpointSnapshot EnumerateActiveRenderEndpoints()
    {
        using ComApartmentScope apartment = ComApartmentScope.EnterMultithreaded();
        IMmDeviceEnumerator? enumerator = null;
        IMmDeviceCollection? collection = null;
        IMmDevice? defaultDevice = null;

        try
        {
            enumerator = (IMmDeviceEnumerator)(object)new MmDeviceEnumeratorComObject();
            ComInterop.ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out defaultDevice));
            string defaultId = GetId(defaultDevice);

            ComInterop.ThrowIfFailed(enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active, out collection));
            ComInterop.ThrowIfFailed(collection.GetCount(out uint count));
            var endpoints = new List<AudioEndpoint>(checked((int)count));
            for (uint index = 0; index < count; index++)
            {
                IMmDevice? device = null;
                try
                {
                    ComInterop.ThrowIfFailed(collection.Item(index, out device));
                    endpoints.Add(new AudioEndpoint(GetId(device), GetFriendlyName(device)));
                }
                finally
                {
                    ComInterop.Release(device);
                }
            }

            endpoints.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
            return new AudioEndpointSnapshot(defaultId, endpoints);
        }
        finally
        {
            ComInterop.ReleaseAll(defaultDevice, collection, enumerator);
        }
    }

    private static string GetId(IMmDevice device)
    {
        ComInterop.ThrowIfFailed(device.GetId(out string id));
        return id;
    }

    private static string GetFriendlyName(IMmDevice device)
    {
        IPropertyStore? properties = null;
        PropVariant value = default;
        try
        {
            ComInterop.ThrowIfFailed(device.OpenPropertyStore(0, out properties));
            PropertyKey key = DeviceFriendlyName;
            ComInterop.ThrowIfFailed(properties.GetValue(ref key, out value));
            return value.GetString();
        }
        finally
        {
            Exception? firstFailure = null;
            try
            {
                ComInterop.ThrowIfFailed(CoreAudioNative.PropVariantClear(ref value));
            }
            catch (Exception exception)
            {
                firstFailure = exception;
            }

            try
            {
                ComInterop.Release(properties);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }

            if (firstFailure is not null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }
    }
}
