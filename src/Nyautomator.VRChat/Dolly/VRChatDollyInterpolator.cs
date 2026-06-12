using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Samples dolly keyframes into per-frame camera pose and settings values.
/// </summary>
public static class VRChatDollyInterpolator
{
    /// <summary>
    /// Samples a track at a specific time using endpoint handling, position interpolation, rotation interpolation, and slider interpolation.
    /// </summary>
    /// <param name="track">Track whose interpolation defaults are used.</param>
    /// <param name="sortedKeyframes">Keyframes ordered by track time.</param>
    /// <param name="timeSeconds">Track-relative sample time in seconds.</param>
    /// <returns>A dolly frame for the requested time.</returns>
    public static VRChatDollyFrame Sample(VRChatDollyTrack track, IReadOnlyList<VRChatDollyKeyframe> sortedKeyframes, double timeSeconds)
    {
        if (sortedKeyframes.Count == 0)
        {
            return new VRChatDollyFrame(
                TimeSpan.Zero,
                Vector3.Zero,
                Vector3.Zero,
                Quaternion.Identity,
                null,
                new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase));
        }

        if (sortedKeyframes.Count == 1 || timeSeconds <= sortedKeyframes[0].TimeSeconds)
            return FromKeyframe(sortedKeyframes[0], timeSeconds);

        var last = sortedKeyframes[^1];
        if (timeSeconds >= last.TimeSeconds)
            return FromKeyframe(last, timeSeconds);

        var nextIndex = 1;
        while (nextIndex < sortedKeyframes.Count && sortedKeyframes[nextIndex].TimeSeconds < timeSeconds)
            nextIndex++;

        var previousIndex = Math.Max(0, nextIndex - 1);
        var previous = sortedKeyframes[previousIndex];
        var next = sortedKeyframes[nextIndex];
        var span = Math.Max(0.0001d, next.TimeSeconds - previous.TimeSeconds);
        var t = (float)Math.Clamp((timeSeconds - previous.TimeSeconds) / span, 0d, 1d);

        var position = InterpolatePosition(track, sortedKeyframes, previousIndex, nextIndex, t);
        var rotation = InterpolateRotation(track, previous, next, t);
        var euler = VRChatHelper.OSC.QuaternionToEulerDegrees(rotation);
        var settings = InterpolateSettings(previous, next, t);

        return new VRChatDollyFrame(
            TimeSpan.FromSeconds(Math.Max(0d, timeSeconds)),
            position,
            euler,
            rotation,
            settings.Mode,
            settings.Toggles,
            settings.Sliders);
    }

    /// <summary>
    /// Converts a single keyframe into a frame, deriving Euler degrees from quaternion data when needed.
    /// </summary>
    /// <param name="keyframe">Keyframe to convert.</param>
    /// <param name="timeSeconds">Track-relative sample time to attach to the frame.</param>
    /// <returns>A frame containing the keyframe's stored pose and settings.</returns>
    private static VRChatDollyFrame FromKeyframe(VRChatDollyKeyframe keyframe, double timeSeconds)
    {
        var rotation = keyframe.Rotation.ToQuaternion();
        var euler = keyframe.EulerDegrees.ToVector3();
        if (euler == Vector3.Zero && rotation != Quaternion.Identity)
            euler = VRChatHelper.OSC.QuaternionToEulerDegrees(rotation);

        return new VRChatDollyFrame(
            TimeSpan.FromSeconds(Math.Max(0d, timeSeconds)),
            keyframe.Position.ToVector3(),
            euler,
            rotation,
            keyframe.Camera.Mode,
            new Dictionary<string, bool>(keyframe.Camera.Toggles, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, float>(keyframe.Camera.Sliders, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Interpolates camera position between two keyframes using the selected linear or Catmull-Rom mode.
    /// </summary>
    /// <param name="track">Track whose default position interpolation may be used.</param>
    /// <param name="keyframes">Ordered keyframes for the track.</param>
    /// <param name="previousIndex">Index of the segment's starting keyframe.</param>
    /// <param name="nextIndex">Index of the segment's ending keyframe.</param>
    /// <param name="t">Normalized segment time from zero to one.</param>
    /// <returns>Interpolated position.</returns>
    private static Vector3 InterpolatePosition(
        VRChatDollyTrack track,
        IReadOnlyList<VRChatDollyKeyframe> keyframes,
        int previousIndex,
        int nextIndex,
        float t)
    {
        var mode = keyframes[previousIndex].PositionInterpolation ?? track.DefaultPositionInterpolation;
        var p1 = keyframes[previousIndex].Position.ToVector3();
        var p2 = keyframes[nextIndex].Position.ToVector3();

        if (!string.Equals(mode, "catmullRom", StringComparison.OrdinalIgnoreCase) || keyframes.Count < 3)
            return Vector3.Lerp(p1, p2, t);

        var p0 = keyframes[Math.Max(0, previousIndex - 1)].Position.ToVector3();
        var p3 = keyframes[Math.Min(keyframes.Count - 1, nextIndex + 1)].Position.ToVector3();
        var t2 = t * t;
        var t3 = t2 * t;

        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>
    /// Interpolates camera rotation using hold, normalized linear interpolation, or slerp.
    /// </summary>
    /// <param name="track">Track whose default rotation interpolation may be used.</param>
    /// <param name="previous">Segment starting keyframe.</param>
    /// <param name="next">Segment ending keyframe.</param>
    /// <param name="t">Normalized segment time from zero to one.</param>
    /// <returns>Interpolated normalized rotation.</returns>
    private static Quaternion InterpolateRotation(
        VRChatDollyTrack track,
        VRChatDollyKeyframe previous,
        VRChatDollyKeyframe next,
        float t)
    {
        var mode = previous.RotationInterpolation ?? track.DefaultRotationInterpolation;
        var from = previous.Rotation.ToQuaternion();
        var to = next.Rotation.ToQuaternion();

        if (string.Equals(mode, "hold", StringComparison.OrdinalIgnoreCase))
            return from;

        if (string.Equals(mode, "linear", StringComparison.OrdinalIgnoreCase))
            return VRChatHelper.OSC.SafeNormalize(Quaternion.Lerp(from, to, t));

        return VRChatHelper.OSC.SafeNormalize(Quaternion.Slerp(from, to, t));
    }

    /// <summary>
    /// Builds interpolated camera settings by holding mode and toggles from the previous keyframe while blending sliders.
    /// </summary>
    /// <param name="previous">Segment starting keyframe.</param>
    /// <param name="next">Segment ending keyframe.</param>
    /// <param name="t">Normalized segment time from zero to one.</param>
    /// <returns>Camera settings for the sampled frame.</returns>
    private static VRChatDollyCameraSettings InterpolateSettings(VRChatDollyKeyframe previous, VRChatDollyKeyframe next, float t)
    {
        var settings = new VRChatDollyCameraSettings
        {
            Mode = previous.Camera.Mode,
            ModeName = previous.Camera.ModeName,
            Toggles = new Dictionary<string, bool>(previous.Camera.Toggles, StringComparer.OrdinalIgnoreCase),
            Sliders = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        };

        var sliderNames = previous.Camera.Sliders.Keys
            .Concat(next.Camera.Sliders.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var sliderName in sliderNames)
        {
            var hasPrev = previous.Camera.Sliders.TryGetValue(sliderName, out var prevValue);
            var hasNext = next.Camera.Sliders.TryGetValue(sliderName, out var nextValue);

            if (hasPrev && hasNext)
                settings.Sliders[sliderName] = prevValue + (nextValue - prevValue) * t;
            else if (hasPrev)
                settings.Sliders[sliderName] = prevValue;
            else if (hasNext)
                settings.Sliders[sliderName] = nextValue;
        }

        return settings;
    }
}
