using System.Security.Cryptography;

namespace NotLiteCode.Cryptography
{
  public class EncryptorOptions
  {
    public bool DisableEncryption;

    public int KeySize;
    public int BlockSize;
    public CipherMode Mode;
    public PaddingMode Padding;

    public EncryptorOptions() : this(256, 128, CipherMode.CBC, PaddingMode.PKCS7)
    { }

    public EncryptorOptions(int KeySize, int BlockSize, CipherMode Mode, PaddingMode Padding)
    {
      this.KeySize = KeySize;
      this.BlockSize = BlockSize;
      this.Mode = Mode;
      this.Padding = Padding;
      this.DisableEncryption = false;
    }
  }
}