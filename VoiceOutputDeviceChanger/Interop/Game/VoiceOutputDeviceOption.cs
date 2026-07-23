using System;
using System.Reflection;
using UnityEngine;
using VoiceOutputDeviceChanger.Interop.Settings;

namespace VoiceOutputDeviceChanger.Interop.Game;

internal sealed class VoiceOutputDeviceOption : MonoBehaviour
{
    private AudioDeviceSelectionController? _controller;
    private object? _textElement;
    private PropertyInfo? _textProperty;

    public void Initialize(AudioDeviceSelectionController controller, object? textElement)
    {
        _controller = controller;
        _textElement = textElement;
        _textProperty = textElement is null ? null : textElement.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
        RefreshLabel();
    }

    public void Cycle()
    {
        if (_controller is not null)
        {
            SetText(_controller.CycleSelection());
        }
    }

    private void OnEnable()
    {
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_controller is not null)
        {
            SetText(_controller.ApplyConfiguredSelection());
        }
    }

    private void SetText(string value)
    {
        if (_textElement is not null && _textProperty is not null && _textProperty.CanWrite)
        {
            _textProperty.SetValue(_textElement, value, null);
        }
    }
}
