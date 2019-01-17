using NotLiteCode.Network;
using NotLiteCode.Server;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace NotLiteCode___Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "NLC Server";

            var ServerSocket = new NLCSocket(true, GenerateSelfSignedCert("NLC", "localhost"));

            var Server = new Server<SharedClass>(ServerSocket);

            Server.OnServerClientConnected += (x, y) => Log($"Client {y.Client} connected!", ConsoleColor.Green);
            Server.OnServerClientDisconnected += (x, y) => Log($"Client {y.Client} disconnected!", ConsoleColor.Yellow);
            Server.OnServerExceptionOccurred += (x, y) => Log($"Exception Occured! {y.Exception}", ConsoleColor.Red);

            // This line intentionally left commented due to excessive lock contestion during performance testing (1000's of calls a second) which starves other performance critical threads thus skewing performance results
            //Server.OnServerMethodInvoked += (x, y) => Log($"Client {y.Client} {(y.WasErroneous ? "failed to invoke" : "invoked")} {y.Identifier} for {y.Duration.TotalMilliseconds}ms.", y.WasErroneous ? ConsoleColor.Yellow : ConsoleColor.Cyan);

            Server.Start();

            Log("Server Started!", ConsoleColor.Green);

            Process.GetCurrentProcess().WaitForExit();
        }

        private static X509Certificate2 GenerateSelfSignedCert(string CertificateName, string HostName)
        {
            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(HostName);

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={CertificateName}");

            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

                request.CertificateExtensions.Add(sanBuilder.Build());

                var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
                certificate.FriendlyName = CertificateName;

                return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
            }
        }

        private static readonly SemaphoreSlim LogSem = new SemaphoreSlim(1, 1);

        private static async void Log(string message, ConsoleColor color)
        {
            await LogSem.WaitAsync();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
            Console.ForegroundColor = color;
            Console.Write("{0}{1}", message, Environment.NewLine);
            Console.ResetColor();

            LogSem.Release();
        }
    }
}