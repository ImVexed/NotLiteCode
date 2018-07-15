using NotLiteCode.Compression;
using NotLiteCode.Network;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace NotLiteCode.Cryptography
{
  public class Encryptor
  {
    private RNGCryptoServiceProvider CryptoRandom = new RNGCryptoServiceProvider();
    private AesCryptoServiceProvider AESProvider = new AesCryptoServiceProvider();
    private CompressorOptions CompressorOptions;

    private const int IV_LENGTH = 16;
    private const int HASH_LENGTH = 32;

    private readonly byte[] Key;

    public Encryptor(byte[] Key, EncryptionOptions EncryptionOptions, CompressorOptions CompressorOptions)
    {
      this.Key = Key;
      this.CompressorOptions = CompressorOptions;

      AESProvider.KeySize = EncryptionOptions.KeySize;
      AESProvider.BlockSize = EncryptionOptions.BlockSize;
      AESProvider.Mode = EncryptionOptions.Mode;
      AESProvider.Padding = EncryptionOptions.Padding;
    }

    public byte[] AES_Encrypt(byte[] Bytes)
    {
      byte[] EncryptedBytes;
      byte[] IV = new byte[IV_LENGTH];

      CryptoRandom.GetBytes(IV);

      AESProvider.Key = this.Key;
      AESProvider.IV = IV;

      using (var AESEncryptor = AESProvider.CreateEncryptor())
        EncryptedBytes = AESEncryptor.TransformFinalBlock(Bytes, 0, Bytes.Length);

      byte[] PackagedBytes = new byte[EncryptedBytes.Length + IV_LENGTH + HASH_LENGTH];

      Array.Copy(IV, 0, PackagedBytes, 0, IV_LENGTH);

      using (var HMAC = new HMACSHA256(this.Key))
        Array.Copy(HMAC.ComputeHash(EncryptedBytes), 0, PackagedBytes, IV_LENGTH, HASH_LENGTH);

      Array.Copy(EncryptedBytes, 0, PackagedBytes, IV_LENGTH + HASH_LENGTH, EncryptedBytes.Length);

      return CompressorOptions.DisableCompression ? PackagedBytes : Compressor.Compress(PackagedBytes);
    }

    public byte[] AES_Decrypt(byte[] Bytes)
    {
      byte[] DecryptedBytes;
      byte[] DecompressedBytes = CompressorOptions.DisableCompression ? Bytes : Compressor.Decompress(Bytes);

      byte[] IV = DecompressedBytes.Slice(0, IV_LENGTH);
      byte[] Hash = DecompressedBytes.Slice(IV_LENGTH, HASH_LENGTH);
      byte[] EncryptedBytes = DecompressedBytes.Slice(IV_LENGTH + HASH_LENGTH, DecompressedBytes.Length - IV_LENGTH - HASH_LENGTH);

      AESProvider.Key = this.Key;
      AESProvider.IV = IV;

      using (var HMAC = new HMACSHA256(this.Key))
        if (!HMAC.ComputeHash(EncryptedBytes).SequenceEqual(Hash))
          throw new Exception("Data has been modified! Oracle padding attack? Who cares! Run!");

      using (var AESDecryptor = AESProvider.CreateDecryptor())
        DecryptedBytes = AESDecryptor.TransformFinalBlock(EncryptedBytes, 0, EncryptedBytes.Length);

      return DecryptedBytes;
    }
  }
}