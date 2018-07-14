using NotLiteCode.Network;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace NotLiteCode.Encryption
{
  public enum HASH_STRENGTH
  {
    LOW = 10000,
    MEDIUM = 10000,
    HIGH = 20000
  }

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

  public class Serializer
  {
    public static byte[] Serialize(object Message)
    {
      using (MemoryStream OutputStream = new MemoryStream())
      {
        BinaryFormatter Formatter = new BinaryFormatter();
        Formatter.Serialize(OutputStream, Message);
        return OutputStream.ToArray();
      }
    }

    public static object[] Deserialize(byte[] MessageData)
    {
      using (MemoryStream OutputStream = new MemoryStream(MessageData))
      {
        BinaryFormatter Formatter = new BinaryFormatter();
        return Formatter.Deserialize(OutputStream) as object[];
      }
    }
  }

  public class Encryptor
  {
    public static int CompressionLevel = 1;

    private RNGCryptoServiceProvider CryptoRandom = new RNGCryptoServiceProvider();
    private AesCryptoServiceProvider AESProvider = new AesCryptoServiceProvider();

    private const int IV_LENGTH = 16;
    private const int HASH_LENGTH = 32;

    private readonly byte[] Key;

    private readonly HASH_STRENGTH HashStrength;

    public Encryptor(byte[] Key, HASH_STRENGTH HashStrength)
    {
      this.HashStrength = HashStrength;
      this.Key = Key;

      AESProvider.KeySize = 256;
      AESProvider.BlockSize = 128;
      AESProvider.Mode = CipherMode.CBC;
      AESProvider.Padding = PaddingMode.PKCS7;
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
      return Compressor.Compress(PackagedBytes);
    }

    public byte[] AES_Decrypt(byte[] Bytes)
    {
      byte[] DecryptedBytes;
      byte[] DecompressedBytes = Compressor.Decompress(Bytes);

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

    public static bool TrySendHandshake(NLCSocket Client, out Encryptor Encryptor, HASH_STRENGTH HashStrength = HASH_STRENGTH.LOW)
    {
      CngKey ECDH = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256);
      byte[] ServerPublicKey = ECDH.Export(CngKeyBlobFormat.EccPublicBlob);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ServerPublicKey);

      if (!Client.TryBlockingSend(ServerPublicEvent, false))
      {
        Encryptor = default(Encryptor);
        return false;
      }

      if (!Client.TryBlockingReceive(out var ClientPublicEvent, false))
      {
        Encryptor = default(Encryptor);
        return false;
      }

      if (ClientPublicEvent.Header != NetworkHeader.HEADER_HANDSHAKE)
      {
        Encryptor = default(Encryptor);
        return false;
      }

      byte[] ClientKey = ClientPublicEvent.Data as byte[];

      using (var ECDHDerive = new ECDiffieHellmanCng(ECDH))
      using (CngKey ClientPublicKey = CngKey.Import(ClientKey, CngKeyBlobFormat.EccPublicBlob))
      {
        Encryptor = new Encryptor(ECDHDerive.DeriveKeyMaterial(ClientPublicKey), HashStrength);
        return true;
      }
    }

    public static bool TryReceiveHandshake(NLCSocket Client, out Encryptor Encryptor, HASH_STRENGTH HashStrength = HASH_STRENGTH.LOW)
    {
      CngKey ECDH = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256);
      byte[] ClientPublicKey = ECDH.Export(CngKeyBlobFormat.EccPublicBlob);

      if (!Client.TryBlockingReceive(out var ClientPublicEvent, false))
      {
        Encryptor = default(Encryptor);
        return false;
      }

      if (ClientPublicEvent.Header != NetworkHeader.HEADER_HANDSHAKE)
      {
        Encryptor = default(Encryptor);
        return false;
      }

      byte[] ServerKey = ClientPublicEvent.Data as byte[];

      using (var ECDHDerive = new ECDiffieHellmanCng(ECDH))
      using (CngKey ServerPublicKey = CngKey.Import(ServerKey, CngKeyBlobFormat.EccPublicBlob))
        Encryptor = new Encryptor(ECDHDerive.DeriveKeyMaterial(ServerPublicKey), HashStrength);

      var ServerPublicEvent = new NetworkEvent(NetworkHeader.HEADER_HANDSHAKE, null, ClientPublicKey);

      if (!Client.TryBlockingSend(ServerPublicEvent, false))
      {
        Encryptor = default(Encryptor);
        return false;
      }

      return true;
    }
  }
}