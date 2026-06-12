using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that sends chatbox text and optional typing state through VRChat OSC.
/// </summary>
public class VRChatOscChatboxMessage : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the chatbox message sender.
    /// </summary>
    public override string Id => "VRChat.OSC.ChatboxMessage";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Chatbox Message";

    /// <summary>
    /// Gets the user-facing explanation of what the chatbox reaction sends.
    /// </summary>
    public override string Description => "Sends a chatbox message above your avatar and optionally toggles the typing indicator.";

    /// <summary>
    /// Gets or sets the chatbox text to send.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the message should be submitted immediately instead of staged.
    /// </summary>
    public bool SendImmediately { get; set; } = true;

    /// <summary>
    /// Gets or sets whether VRChat should play its chatbox send sound.
    /// </summary>
    public bool PlaySound { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this reaction also updates the chatbox typing indicator.
    /// </summary>
    public bool ChangeTypingIndicator { get; set; }

    /// <summary>
    /// Gets or sets the typing indicator value sent when <see cref="ChangeTypingIndicator"/> is enabled.
    /// </summary>
    public bool TypingIndicatorValue { get; set; }

    /// <summary>
    /// Sends chatbox text when configured and applies the optional typing indicator state.
    /// </summary>
    public override void Execute()
    {
        var text = Message ?? string.Empty;
        if (text.Length > 0 || !SendImmediately)
            VRChatHelper.OSC.SendChatboxMessage(text, SendImmediately, PlaySound);

        if (ChangeTypingIndicator)
            VRChatHelper.OSC.SetChatboxTyping(TypingIndicatorValue);
    }
}
