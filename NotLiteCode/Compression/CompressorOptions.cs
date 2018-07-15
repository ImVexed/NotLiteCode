using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
