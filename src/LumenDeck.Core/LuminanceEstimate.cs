namespace LumenDeck;

/// <summary>
/// A linear-model result with profile provenance kept separate from measurement.
/// Even measured endpoints do not establish the current emitted luminance.
/// </summary>
internal sealed record LuminanceEstimate(int Nits, bool UsesUserProfile, bool ProfileEndpointsMeasured)
{
    public string ProfileKind => UsesUserProfile ? "user" : "generic";
    public string ProfileLabel => UsesUserProfile
        ? ProfileEndpointsMeasured ? "user profile, declared measurements" : "user profile"
        : "generic profile";
    public string DisplayText => $"Estimated {Nits} nits · {ProfileLabel}";
    public string Explanation =>
        "Calculated from brightness and a linear peak/floor model; no light sensor is used. " +
        (ProfileEndpointsMeasured
            ? "Endpoint measurements are declared by the profile author, not verified by LumenDeck. "
            : "Profile endpoints are not declared instrument-measured. ") +
        "Picture mode, calibration and the panel's brightness curve can affect the result.";
}
