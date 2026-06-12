using Nyautomator.Runtime.Abstractions;
using Nyautomator;

namespace Nyautomator.Modules.VRChat;

internal sealed class VRChatSessionStoreAdapter(IIntegrationTokenStore tokens) : IVRChatSessionStore
{
    private const string TokenKey = "vrchat";
    private readonly IIntegrationTokenStore _tokens = tokens;

    public VRChatSessionToken? Get()
    {
        var token = _tokens.Get(TokenKey);
        if (token is null)
            return null;

        return new VRChatSessionToken
        {
            AccountId = token.AccountId,
            AccountLogin = token.AccountLogin,
            AccountDisplayName = token.AccountDisplayName,
            Metadata = token.Metadata is null
                ? null
                : new Dictionary<string, string>(token.Metadata, StringComparer.OrdinalIgnoreCase),
            UpdatedAtUtc = token.UpdatedAtUtc
        };
    }

    public void Set(VRChatSessionToken token)
    {
        if (token is null)
            throw new ArgumentNullException(nameof(token));

        _tokens.Set(TokenKey, new IntegrationToken
        {
            AccountId = token.AccountId,
            AccountLogin = token.AccountLogin,
            AccountDisplayName = token.AccountDisplayName,
            Metadata = token.Metadata is null
                ? null
                : new Dictionary<string, string>(token.Metadata, StringComparer.OrdinalIgnoreCase),
            UpdatedAtUtc = token.UpdatedAtUtc
        });
    }

    public void Clear()
        => _tokens.Clear(TokenKey);
}
