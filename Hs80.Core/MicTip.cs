namespace Hs80.Core;

public static class MicTip
{
    public static int[] Resolve(bool followMute, bool? muted, int[] liveRgb, int[] mutedRgb)
    {
        return followMute && muted == true ? mutedRgb : liveRgb;
    }
}
