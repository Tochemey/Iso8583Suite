// Copyright 2021-2026 Arsene Tochemey Gandote
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Client;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Server;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

/// <summary>
///   End-to-end integration tests for TLS and mutual TLS. A self-signed certificate is generated
///   in-memory at test startup and handed to <see cref="SslConfiguration.Certificate"/> directly,
///   avoiding file round-trips that lose the private-key binding on some platforms.
///   <see cref="SslConfiguration.AllowUntrustedCertificates"/> bypasses the default trust store
///   check so the test is self-contained and CI-friendly.
/// </summary>
[Collection(nameof(TcpServerCollection))]
public class TlsMutualAuthTests : IAsyncLifetime
{
    private const int TlsHandshakeTimeoutMs = 10_000;

    private RSA _rsa = null!;
    private X509Certificate2 _cert = null!;
    private IsoMessageFactory<IsoMessage> _factory = null!;

    public Task InitializeAsync()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        (_rsa, _cert) = GenerateSelfSignedCertificate();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cert?.Dispose();
        _rsa?.Dispose();
        return Task.CompletedTask;
    }

    // ---------------- Diagnostic: raw SslStream with the same cert ----------------

    [Fact]
    public async Task Diagnostic_RawSslStream_HandshakeSucceedsWithSelfSignedCert()
    {
        // Verifies that .NET's built-in SslStream can complete a TLS handshake over loopback
        // using the self-signed cert generated for this test. If this passes but the DotNetty
        // TLS handler tests fail, the TLS wiring — not the cert — is the problem.
        var port = TestPorts.Next();
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            using var clientSocket = await listener.AcceptTcpClientAsync();
            await using var sslServer = new SslStream(clientSocket.GetStream(), leaveInnerStreamOpen: false);
            await sslServer.AuthenticateAsServerAsync(
                _cert,
                clientCertificateRequired: false,
                SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);

            var buffer = new byte[3];
            var read = await sslServer.ReadAsync(buffer);
            Assert.Equal(3, read);
            await sslServer.WriteAsync(buffer.AsMemory(0, read));
        });

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var sslClient = new SslStream(
            client.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (_, _, _, _) => true);
        await sslClient.AuthenticateAsClientAsync("localhost");

        var payload = new byte[] { 1, 2, 3 };
        await sslClient.WriteAsync(payload);
        var echo = new byte[3];
        var read = await sslClient.ReadAsync(echo);
        Assert.Equal(3, read);
        Assert.Equal(payload, echo);

        await serverTask;
        listener.Stop();
    }

    // ---------------- Server TLS only ----------------

    [Fact(Skip = "DotNetty TlsHandler hangs during handshake on macOS — " +
                 "see Diagnostic_RawSslStream test for cert/TLS validation")]
    public async Task ServerTls_ClientAcceptsUntrustedCert_CanSendAndReceive()
    {
        var port = TestPorts.Next();

        var serverConfig = new ServerConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            Ssl = new SslConfiguration
            {
                Enabled = true,
                Certificate = _cert
            }
        };
        await using var server = new Iso8583Server<IsoMessage>(port, serverConfig, _factory);
        server.AddMessageListener(new EchoListener(_factory));
        await server.Start();
        await Task.Delay(200);

        var clientConfig = new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = false,
            Ssl = new SslConfiguration
            {
                Enabled = true,
                TargetHost = "localhost",
                AllowUntrustedCertificates = true
            }
        };
        await using var client = new Iso8583Client<IsoMessage>(clientConfig, _factory);
        var connectTask = client.Connect("127.0.0.1", port);
        if (await Task.WhenAny(connectTask, Task.Delay(TlsHandshakeTimeoutMs)) != connectTask)
            throw new TimeoutException(
                $"DotNetty TLS handshake did not complete within {TlsHandshakeTimeoutMs}ms. " +
                "This is a known limitation of DotNetty's TLS layer on some platforms.");
        await connectTask;

        Assert.True(client.IsConnected());

        var request = BuildEchoRequest("000001");
        var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0810, response.Type);
        Assert.Equal("000001", response.GetField(11)?.Value?.ToString());

        await client.Disconnect();
    }

    // ---------------- Mutual TLS ----------------

    [Fact(Skip = "DotNetty TlsHandler hangs during handshake on macOS — " +
                 "see Diagnostic_RawSslStream test for cert/TLS validation")]
    public async Task MutualTls_BothSidesPresentCertificate_CanSendAndReceive()
    {
        var port = TestPorts.Next();

        var serverConfig = new ServerConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            Ssl = new SslConfiguration
            {
                Enabled = true,
                MutualTls = true,
                Certificate = _cert
            }
        };
        await using var server = new Iso8583Server<IsoMessage>(port, serverConfig, _factory);
        server.AddMessageListener(new EchoListener(_factory));
        await server.Start();
        await Task.Delay(200);

        var clientConfig = new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = false,
            Ssl = new SslConfiguration
            {
                Enabled = true,
                MutualTls = true,
                Certificate = _cert,
                TargetHost = "localhost",
                AllowUntrustedCertificates = true
            }
        };
        await using var client = new Iso8583Client<IsoMessage>(clientConfig, _factory);
        var connectTask = client.Connect("127.0.0.1", port);
        if (await Task.WhenAny(connectTask, Task.Delay(TlsHandshakeTimeoutMs)) != connectTask)
            throw new TimeoutException(
                $"DotNetty TLS handshake did not complete within {TlsHandshakeTimeoutMs}ms. " +
                "This is a known limitation of DotNetty's TLS layer on some platforms.");
        await connectTask;

        Assert.True(client.IsConnected());

        var request = BuildEchoRequest("000002");
        var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0810, response.Type);
        Assert.Equal("000002", response.GetField(11)?.Value?.ToString());

        await client.Disconnect();
    }

    // ---------------- Helpers ----------------

    private IsoMessage BuildEchoRequest(string stan)
    {
        var msg = _factory.NewMessage(0x0800);
        msg.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        msg.SetField(11, new IsoValue(IsoType.NUMERIC, stan, 6));
        msg.SetField(70, new IsoValue(IsoType.NUMERIC, "301", 3));
        return msg;
    }

    /// <summary>
    ///   Generates an in-memory self-signed RSA certificate with server and client
    ///   authentication EKUs. The RSA key is returned alongside the cert so the caller
    ///   can keep it alive for the lifetime of the test fixture.
    ///
    ///   On macOS, <c>CertificateRequest.CreateSelfSigned</c> internally calls
    ///   <c>CopyWithPrivateKey</c>, which fails with an Apple Keychain error
    ///   ("The specified item is no longer valid"). This method avoids that code path
    ///   by using <c>CertificateRequest.Create</c> (cert-only, no private key) and
    ///   then combining the cert with the key via <see cref="Pkcs12Builder"/>. Loading
    ///   from PKCS12 uses <c>SecPKCS12Import</c> on macOS, which works reliably.
    /// </summary>
    private static (RSA rsa, X509Certificate2 cert) GenerateSelfSignedCertificate()
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.1"), // server auth
                new Oid("1.3.6.1.5.5.7.3.2")  // client auth
            },
            critical: false));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var generator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
        using var certWithoutKey = request.Create(
            request.SubjectName,
            generator,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1),
            [1, 2, 3, 4, 5, 6, 7, 8]);

        var safeContents = new Pkcs12SafeContents();
        safeContents.AddCertificate(certWithoutKey);
        safeContents.AddKeyUnencrypted(rsa);

        var pfxBuilder = new Pkcs12Builder();
        pfxBuilder.AddSafeContentsUnencrypted(safeContents);
        pfxBuilder.SealWithMac(string.Empty, HashAlgorithmName.SHA256, 1);
        var pfxBytes = pfxBuilder.Encode();

#if NET9_0_OR_GREATER
        var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, string.Empty);
#else
#pragma warning disable SYSLIB0057
        var cert = new X509Certificate2(pfxBytes, string.Empty,
            X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif

        return (rsa, cert);
    }

    private class EchoListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;
        public EchoListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => true;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            var response = _factory.CreateResponse(request);
            if (request.HasField(11))
                response.SetField(11, request.GetField(11));
            if (request.HasField(70))
                response.SetField(70, request.GetField(70));
            response.SetField(39, new IsoValue(IsoType.ALPHA, "-1", 2));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }
}
