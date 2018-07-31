namespace NotLiteCode.Compression
{
  public class CompressorOptions
  {
    public bool DisableCompression;

    public CompressorOptions() : this(false)
    { }

    public CompressorOptions(bool DisableCompression)
    {
      this.DisableCompression = DisableCompression;
    }
  }
}