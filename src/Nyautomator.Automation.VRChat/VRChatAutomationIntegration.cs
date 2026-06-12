using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Registers VRChat OSC camera and dolly runtime events as an automation event source.
/// </summary>
public static class VRChatAutomationIntegration
{
    /// <summary>
    /// Automation engine source name used for every VRChat external event dispatch.
    /// </summary>
    private const string SourceName = "VRChat";

    /// <summary>
    /// Tracks whether the source has already been registered with the automation engine.
    /// </summary>
    private static bool _registered;

    /// <summary>
    /// Tracks whether local handlers are currently subscribed to VRChat runtime events.
    /// </summary>
    private static bool _wired;

    /// <summary>
    /// Makes the VRChat source available to the automation engine once per process.
    /// </summary>
    public static void Register()
    {
        if (_registered)
            return;

        AutomationFlowEngine.RegisterExternalEventSource(SourceName, Link, Unlink);
        _registered = true;
    }

    /// <summary>
    /// Subscribes automation dispatchers to live VRChat OSC camera and dolly events.
    /// </summary>
    private static void Link()
    {
        if (_wired)
            return;

        VRChatHelper.OSC.OnCameraModeChanged += OnCameraModeChanged;
        VRChatHelper.OSC.OnCameraToggleChanged += OnCameraToggleChanged;
        VRChatHelper.OSC.OnCameraSliderChanged += OnCameraSliderChanged;
        VRChatHelper.OSC.OnCameraPoseChanged += OnCameraPoseChanged;
        VRChatHelper.OSC.OnCameraActionTriggered += OnCameraActionTriggered;
        VRChatDollyRuntime.EventEmitted += OnDollyEvent;
        _wired = true;
    }

    /// <summary>
    /// Removes automation dispatchers from live VRChat OSC camera and dolly events.
    /// </summary>
    private static void Unlink()
    {
        if (!_wired)
            return;

        VRChatHelper.OSC.OnCameraModeChanged -= OnCameraModeChanged;
        VRChatHelper.OSC.OnCameraToggleChanged -= OnCameraToggleChanged;
        VRChatHelper.OSC.OnCameraSliderChanged -= OnCameraSliderChanged;
        VRChatHelper.OSC.OnCameraPoseChanged -= OnCameraPoseChanged;
        VRChatHelper.OSC.OnCameraActionTriggered -= OnCameraActionTriggered;
        VRChatDollyRuntime.EventEmitted -= OnDollyEvent;
        _wired = false;
    }

    /// <summary>
    /// Dispatches a VRChat camera mode payload to matching automation graph actions.
    /// </summary>
    /// <param name="payload">Mode change data raised by the VRChat OSC helper.</param>
    private static void OnCameraModeChanged(VRChatCameraModeChangedEvent payload)
        => AutomationFlowEngine.DispatchExternalAction(SourceName, VRChatOscCameraModeChangedEvent.TypeId, payload);

    /// <summary>
    /// Dispatches a VRChat camera toggle payload to matching automation graph actions.
    /// </summary>
    /// <param name="payload">Toggle change data raised by the VRChat OSC helper.</param>
    private static void OnCameraToggleChanged(VRChatCameraToggleChangedEvent payload)
        => AutomationFlowEngine.DispatchExternalAction(SourceName, VRChatOscCameraToggleChangedEvent.TypeId, payload);

    /// <summary>
    /// Dispatches a VRChat camera slider payload to matching automation graph actions.
    /// </summary>
    /// <param name="payload">Slider change data raised by the VRChat OSC helper.</param>
    private static void OnCameraSliderChanged(VRChatCameraSliderChangedEvent payload)
        => AutomationFlowEngine.DispatchExternalAction(SourceName, VRChatOscCameraSliderChangedEvent.TypeId, payload);

    /// <summary>
    /// Dispatches a VRChat camera pose payload to matching automation graph actions.
    /// </summary>
    /// <param name="payload">Pose data raised by the VRChat OSC helper.</param>
    private static void OnCameraPoseChanged(VRChatCameraPoseChangedEvent payload)
        => AutomationFlowEngine.DispatchExternalAction(SourceName, VRChatOscCameraPoseChangedEvent.TypeId, payload);

    /// <summary>
    /// Dispatches a VRChat camera action payload to matching automation graph actions.
    /// </summary>
    /// <param name="payload">Camera action data raised by the VRChat OSC helper.</param>
    private static void OnCameraActionTriggered(VRChatCameraActionTriggeredEvent payload)
        => AutomationFlowEngine.DispatchExternalAction(SourceName, VRChatOscCameraActionTriggeredEvent.TypeId, payload);

    /// <summary>
    /// Maps a dolly runtime event name to its automation action type and dispatches it.
    /// </summary>
    /// <param name="payload">Dolly runtime event emitted by Nyautomator's VRChat dolly runtime.</param>
    private static void OnDollyEvent(VRChatDollyEvent payload)
    {
        var triggerType = payload.Type switch
        {
            "keyframeCaptured" => VRChatDollyKeyframeCapturedEvent.TypeId,
            "playbackStarted" => VRChatDollyPlaybackStartedEvent.TypeId,
            "frameSent" => VRChatDollyFrameSentEvent.TypeId,
            "loopCompleted" => VRChatDollyLoopCompletedEvent.TypeId,
            "playbackStopped" => VRChatDollyPlaybackStoppedEvent.TypeId,
            "playbackFailed" => VRChatDollyPlaybackFailedEvent.TypeId,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(triggerType))
            AutomationFlowEngine.DispatchExternalAction(SourceName, triggerType, payload);
    }
}
