namespace YtLight.Playback;

public static class LoudnessNormalizer
{
    public const double TargetLufs = -14.0;

    public static float ToLinearGain(double? loudnessDb)
    {
        if (loudnessDb is null)
            return 1f;

        var gainDb = TargetLufs - loudnessDb.Value;
        gainDb = Math.Clamp(gainDb, -12.0, 12.0);
        return (float)Math.Pow(10.0, gainDb / 20.0);
    }

    public static double ToLogVolume(double linear01)
    {
        var clamped = Math.Clamp(linear01, 0, 1);
        if (clamped <= 0)
            return 0;
        return Math.Pow(clamped, 2.2);
    }
}
