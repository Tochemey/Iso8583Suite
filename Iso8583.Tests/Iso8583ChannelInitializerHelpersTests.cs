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
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Common;
using Iso8583.Common.Netty.Pipelines;
using Xunit;

namespace Iso8583.Tests;

/// <summary>
///   Direct unit tests for the TLS-setup and certificate-resolution helpers that
///   <see cref="Iso8583ChannelInitializer{TC}"/> uses from <c>InitChannel</c>. These
///   tests sit below the DotNetty TLS handshake so they exercise the pipeline wiring
///   on every platform — including macOS where the full TLS integration tests are
///   skipped because of a DotNetty handshake hang.
/// </summary>
public class Iso8583ChannelInitializerHelpersTests
{
    // ---------------- TryAddTlsHandler ----------------

    [Fact]
    public void TryAddTlsHandler_SslDisabled_DoesNothing()
    {
        var channel = new EmbeddedChannel();
        Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
            channel.Pipeline, ssl: null, isClient: true, clientRemoteAddress: null);

        Assert.Null(channel.Pipeline.Get("tls"));

        Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
            channel.Pipeline,
            new SslConfiguration { Enabled = false },
            isClient: true,
            clientRemoteAddress: null);

        Assert.Null(channel.Pipeline.Get("tls"));
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void TryAddTlsHandler_ClientOneWayTls_InstallsTlsHandler()
    {
        using var cert = GenerateSelfSignedCertificate();
        var channel = new EmbeddedChannel();
        var ssl = new SslConfiguration
        {
            Enabled = true,
            TargetHost = "localhost",
            AllowUntrustedCertificates = true
        };

        Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
            channel.Pipeline, ssl, isClient: true, clientRemoteAddress: null);

        Assert.NotNull(channel.Pipeline.Get("tls"));
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void TryAddTlsHandler_ClientMutualTls_WithCert_InstallsTlsHandler()
    {
        using var cert = GenerateSelfSignedCertificate();
        var channel = new EmbeddedChannel();
        var ssl = new SslConfiguration
        {
            Enabled = true,
            MutualTls = true,
            Certificate = cert,
            TargetHost = "localhost"
        };

        Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
            channel.Pipeline, ssl, isClient: true, clientRemoteAddress: null);

        Assert.NotNull(channel.Pipeline.Get("tls"));
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void TryAddTlsHandler_ClientNullTargetHost_FallsBackToRemoteAddress()
    {
        var channel = new EmbeddedChannel();
        var ssl = new SslConfiguration { Enabled = true };

        Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
            channel.Pipeline, ssl, isClient: true, clientRemoteAddress: "10.0.0.1:443");

        Assert.NotNull(channel.Pipeline.Get("tls"));
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void TryAddTlsHandler_ServerWithCert_InstallsTlsHandler()
    {
        using var cert = GenerateSelfSignedCertificate();
        var channel = new EmbeddedChannel();
        var ssl = new SslConfiguration { Enabled = true, Certificate = cert };

        Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
            channel.Pipeline, ssl, isClient: false, clientRemoteAddress: null);

        Assert.NotNull(channel.Pipeline.Get("tls"));
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void TryAddTlsHandler_ServerWithoutCert_Throws()
    {
        var channel = new EmbeddedChannel();
        var ssl = new SslConfiguration { Enabled = true };

        Assert.Throws<InvalidOperationException>(() =>
            Iso8583ChannelInitializer<ClientConfiguration_TestShim>.TryAddTlsHandler(
                channel.Pipeline, ssl, isClient: false, clientRemoteAddress: null));

        channel.CloseAsync().Wait();
    }

    // ---------------- ResolveCertificate ----------------

    [Fact]
    public void ResolveCertificate_PrefersPreloadedCertificate()
    {
        using var cert = GenerateSelfSignedCertificate();
        var ssl = new SslConfiguration { Certificate = cert };

        var resolved = Iso8583ChannelInitializer<ClientConfiguration_TestShim>.ResolveCertificate(ssl);

        Assert.Same(cert, resolved);
    }

    [Fact]
    public void ResolveCertificate_NoCertificateOrPath_ReturnsNull()
    {
        var resolved = Iso8583ChannelInitializer<ClientConfiguration_TestShim>.ResolveCertificate(new SslConfiguration());
        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveCertificate_FromPath_LoadsCertificate()
    {
        // A non-empty password is used because the legacy X509Certificate2(string, string)
        // constructor on .NET 8 on macOS fails to verify the PKCS#12 MAC when the password
        // is empty, even though the file round-trips fine on .NET 9+.
        const string pfxPassword = "iso8583-test";
        var (rsa, original, pfxBytes) = GenerateSelfSignedWithPfxBytes(pfxPassword);
        using (rsa)
        using (original)
        {
            var path = Path.Combine(Path.GetTempPath(), $"iso8583-test-{Guid.NewGuid():N}.pfx");
            File.WriteAllBytes(path, pfxBytes);

            try
            {
                var ssl = new SslConfiguration { CertificatePath = path, CertificatePassword = pfxPassword };
                var resolved = Iso8583ChannelInitializer<ClientConfiguration_TestShim>.ResolveCertificate(ssl);

                Assert.NotNull(resolved);
                Assert.Equal(original.Thumbprint, resolved!.Thumbprint);
                resolved.Dispose();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    // ---------------- Test helpers ----------------

    // Concrete shim used purely as the generic argument. InitChannel is never invoked
    // here; TryAddTlsHandler and ResolveCertificate are static, so the generic argument
    // is only a type-parameter placeholder.
    private sealed class ClientConfiguration_TestShim : ConnectorConfiguration { }

    private static X509Certificate2 GenerateSelfSignedCertificate()
    {
        var (rsa, cert, _) = GenerateSelfSignedWithPfxBytes(string.Empty);
        rsa.Dispose();
        return cert;
    }

    /// <summary>
    ///   Builds a self-signed cert via <see cref="Pkcs12Builder"/> the same way
    ///   <c>TlsMutualAuthTests</c> does, so the resulting PFX bytes round-trip on macOS
    ///   (which fails under <c>X509Certificate2.Export(Pfx)</c> due to a keychain bug).
    /// </summary>
    private static (RSA rsa, X509Certificate2 cert, byte[] pfxBytes) GenerateSelfSignedWithPfxBytes(string password)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=iso8583-helpers-test", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
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
        pfxBuilder.SealWithMac(password, HashAlgorithmName.SHA256, 1);
        var pfxBytes = pfxBuilder.Encode();

#if NET9_0_OR_GREATER
        var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, password);
#else
#pragma warning disable SYSLIB0057
        var cert = new X509Certificate2(pfxBytes, password, X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057
#endif
        return (rsa, cert, pfxBytes);
    }
}
