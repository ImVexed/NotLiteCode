using System.Security.Cryptography;

namespace NotLiteCode.Cryptography
{
  public class EncryptionOptions
  {
    public bool DisableEncryption;

    public int KeySize;
    public int BlockSize;
    public CipherMode Mode;
    public PaddingMode Padding;

    public EncryptionOptions() : this(256, 128, CipherMode.CBC, PaddingMode.PKCS7)
    { }

    public EncryptionOptions(int KeySize, int BlockSize, CipherMode Mode, PaddingMode Padding)
    {
      this.KeySize = KeySize;
      this.BlockSize = BlockSize;
      this.Mode = Mode;
      this.Padding = Padding;
      this.DisableEncryption = false;
    }
  }
}