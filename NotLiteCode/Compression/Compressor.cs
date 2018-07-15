using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace NotLiteCode.Compression
{
  public class Compressor
  {
    public static byte[] Compress(byte[] Bytes)
    {
      using (var InputStream = new MemoryStream(Bytes))
      using (var OutputStream = new MemoryStream())
      {
        using (var DeflateStream = new DeflateStream(OutputStream, CompressionMode.Compress))
        {
          InputStream.CopyTo(DeflateStream);
        }
        return OutputStream.ToArray();
      }
    }

    public static byte[] Decompress(byte[] Bytes)
    {
      using (var InputStream = new MemoryStream(Bytes))
      using (var OutputStream = new MemoryStream())
      {
        using (var DeflateStream = new DeflateStream(InputStream, CompressionMode.Decompress))
        {
          DeflateStream.CopyTo(OutputStream);
        }
        return OutputStream.ToArray();
      }
    }
  }
}
