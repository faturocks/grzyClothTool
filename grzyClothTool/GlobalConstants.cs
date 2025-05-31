using System;

namespace grzyClothTool;

public static class GlobalConstants
{
    public const int MAX_DRAWABLES_IN_ADDON = 128; //128 drawables per type per YMT file for all resource types. Alt:V uses optimized bin-packing to minimize YMT count.
    public const int MAX_DRAWABLE_TEXTURES = 26;
    public static readonly Uri DISCORD_INVITE_URL = new("https://discord.gg/HCQutNhxWt");
}
