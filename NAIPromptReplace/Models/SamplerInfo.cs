namespace NAIPromptReplace.Models;

public class SamplerInfo : IEquatable<SamplerInfo>
{
    #region Samplers
    public static readonly SamplerInfo Euler = new SamplerInfo
    {
        Name = "Euler",
        Id = "k_euler"
    };
    public static readonly SamplerInfo EulerAncestral = new SamplerInfo
    {
        Name = "Euler Ancestral",
        Id = "k_euler_ancestral"
    };
    public static readonly SamplerInfo DpmPp2SAncestral = new SamplerInfo
    {
        Name = "DPM++ 2S Ancestral",
        Id = "k_dpmpp_2s_ancestral"
    };
    public static readonly SamplerInfo DpmPp2M = new SamplerInfo
    {
        Name = "DPM++ 2M",
        Id = "k_dpmpp_2m"
    };
    public static readonly SamplerInfo DpmPpSde = new SamplerInfo
    {
        Name = "DPM++ SDE",
        Id = "k_dpmpp_sde"
    };
    public static readonly SamplerInfo DpmPp2MSde = new SamplerInfo
    {
        Name = "DPM++ 2M SDE",
        Id = "k_dpmpp_2m_sde"
    };
    public static readonly SamplerInfo Dpm2 = new SamplerInfo
    {
        Name = "DPM2",
        Id = "k_dpm_2"
    };
    public static readonly SamplerInfo DpmFast = new SamplerInfo
    {
        Name = "DPM Fast",
        Id = "k_dpm_fast"
    };
    public static readonly SamplerInfo Ddim = new SamplerInfo
    {
        Name = "DDIM",
        Id = "ddim",
        AllowSmea = false
    };
    public static readonly SamplerInfo DdimV3 = new SamplerInfo
    {
        Name = "DDIM",
        Id = "ddim_v3",
        AllowSmea = false
    };

    public static SamplerInfo[] Samplers = [Euler, EulerAncestral, DpmPp2SAncestral, DpmPp2M, DpmPp2MSde, DpmPpSde, Dpm2, DpmFast, Ddim, DdimV3];
    #endregion

    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public bool AllowSmea { get; init; } = true;

    public bool Equals(SamplerInfo? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((SamplerInfo)obj);
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(SamplerInfo? left, SamplerInfo? right) => Equals(left, right);

    public static bool operator !=(SamplerInfo? left, SamplerInfo? right) => !Equals(left, right);
}
