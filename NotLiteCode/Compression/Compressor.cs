using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NotLiteCode.Compression
{
  public class Compressor
  {
    public static Task<Byte[]> Compress(byte[] Bytes)
    {
      using (var InputStream = new MemoryStream(Bytes))
      using (var OutputStream = new MemoryStream())
      {
        using (var DeflateStream = new DeflateStream(OutputStream, CompressionMode.Compress))
        {
          InputStream.CopyTo(DeflateStream);
        }
        return Task.FromResult(OutputStream.ToArray());
      }
    }

    public static Task<byte[]> Decompress(byte[] Bytes)
    {
      using (var InputStream = new MemoryStream(Bytes))
      using (var OutputStream = new MemoryStream())
      {
        using (var DeflateStream = new DeflateStream(InputStream, CompressionMode.Decompress))
        {
          DeflateStream.CopyTo(OutputStream);
        }
        return Task.FromResult(OutputStream.ToArray());
      }
    }
  }
}