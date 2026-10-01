using System;

namespace ZapretReborn;

public class ZapretVersion : IComparable<ZapretVersion>
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Build { get; set; }
    public char Patch { get; set; }

    public static ZapretVersion Parse(string version)
    {
        var v = new ZapretVersion();
        string clean = version.TrimStart('v', 'V').Trim();

        // Извлечение буквенного суффикса (например, 'd' в "1.9.9d")
        if (clean.Length > 0 && char.IsLetter(clean[^1]))
        {
            v.Patch = char.ToLower(clean[^1]);
            clean = clean[..^1];
        }

        var parts = clean.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out int maj)) v.Major = maj;
        if (parts.Length > 1 && int.TryParse(parts[1], out int min)) v.Minor = min;
        if (parts.Length > 2 && int.TryParse(parts[2], out int bld)) v.Build = bld;

        return v;
    }

    public int CompareTo(ZapretVersion? other)
    {
        if (other == null) return 1;
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        if (Build != other.Build) return Build.CompareTo(other.Build);
        return Patch.CompareTo(other.Patch);
    }

    public static bool operator >(ZapretVersion a, ZapretVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(ZapretVersion a, ZapretVersion b) => a.CompareTo(b) < 0;
}