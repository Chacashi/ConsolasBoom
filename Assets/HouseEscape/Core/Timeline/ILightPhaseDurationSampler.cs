namespace HouseEscape.Core.Timeline
{
    public interface ILightPhaseDurationSampler
    {
        double Sample(LightPhaseDurationRange range);
    }
}
