using System.Numerics;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that sends an absolute VRChat user camera pose over OSC.
/// </summary>
public class VRChatOscCameraSetPose : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the camera pose setter.
    /// </summary>
    public override string Id => "VRChat.OSC.Camera.SetPose";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Set Pose";

    /// <summary>
    /// Gets the user-facing explanation of what pose data this reaction sends.
    /// </summary>
    public override string Description => "Sets the VRChat user camera position and rotation.";

    /// <summary>
    /// Gets or sets the X component of the camera position vector.
    /// </summary>
    public float PositionX { get; set; }

    /// <summary>
    /// Gets or sets the Y component of the camera position vector.
    /// </summary>
    public float PositionY { get; set; }

    /// <summary>
    /// Gets or sets the Z component of the camera position vector.
    /// </summary>
    public float PositionZ { get; set; }

    /// <summary>
    /// Gets or sets the X component of the camera rotation quaternion.
    /// </summary>
    public float RotationX { get; set; }

    /// <summary>
    /// Gets or sets the Y component of the camera rotation quaternion.
    /// </summary>
    public float RotationY { get; set; }

    /// <summary>
    /// Gets or sets the Z component of the camera rotation quaternion.
    /// </summary>
    public float RotationZ { get; set; }

    /// <summary>
    /// Gets or sets the W component of the camera rotation quaternion.
    /// </summary>
    public float RotationW { get; set; } = 1f;

    /// <summary>
    /// Builds the configured position and quaternion values and sends them to VRChat.
    /// </summary>
    public override void Execute()
    {
        var position = new Vector3(PositionX, PositionY, PositionZ);
        var rotation = new Quaternion(RotationX, RotationY, RotationZ, RotationW);
        VRChatHelper.OSC.SetCameraPose(position, rotation);
    }
}
