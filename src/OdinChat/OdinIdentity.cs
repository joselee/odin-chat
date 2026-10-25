using Splatform;

namespace OdinChat
{
    internal static class OdinIdentity
    {
        internal const string DisplayName = "[Odin]";
        internal const string PlatformName = "Odin";
        internal const string PlatformId = "Relay";

        internal static readonly PlatformUserID UserId = new PlatformUserID(PlatformName, PlatformId);

        // A negative peer-style user id is used deliberately: real Valheim peer ids
        // are positive, so this cannot collide with a real player's routed-RPC target.
        internal const long CharacterUserId = -0x0D1D0D1D0D1D0001L;
        internal const uint CharacterId = 0x0D1D0001u;
    }
}
