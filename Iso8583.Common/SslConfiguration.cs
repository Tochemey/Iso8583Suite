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

using System.Security.Cryptography.X509Certificates;

namespace Iso8583.Common
{
  /// <summary>
  ///   SSL/TLS configuration for ISO 8583 connections.
  /// </summary>
  public class SslConfiguration
  {
    /// <summary>
    ///   Whether SSL/TLS is enabled. Default is false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///   Path to the certificate file (PFX/PKCS12 format for server, PEM for client CA).
    ///   Ignored when <see cref="Certificate"/> is set.
    /// </summary>
    public string CertificatePath { get; set; }

    /// <summary>
    ///   Password for the certificate file. Ignored when <see cref="Certificate"/> is set.
    /// </summary>
    public string CertificatePassword { get; set; }

    /// <summary>
    ///   An already-loaded certificate instance. When set, takes precedence over
    ///   <see cref="CertificatePath"/> and <see cref="CertificatePassword"/>. Use this
    ///   overload when sourcing certificates from Key Vault, a secret store, or any
    ///   scenario where the cert is produced programmatically rather than from a file.
    /// </summary>
    public X509Certificate2 Certificate { get; set; }

    /// <summary>
    ///   Whether mutual TLS (client certificate authentication) is required.
    /// </summary>
    public bool MutualTls { get; set; }

    /// <summary>
    ///   Path to the CA certificate for verifying the remote peer.
    /// </summary>
    public string CaCertificatePath { get; set; }

    /// <summary>
    ///   The target host name for server certificate validation (client-side).
    ///   If null, uses the connection hostname.
    /// </summary>
    public string TargetHost { get; set; }

    /// <summary>
    ///   When set to <c>true</c>, the client accepts self-signed certificates, name-mismatch
    ///   certificates, and certificate chain errors. <b>This is a development-only escape hatch</b>
    ///   for local testing and staging environments where provisioning a trusted CA is impractical.
    ///   Production deployments must leave this <c>false</c> and rely on a properly signed server
    ///   certificate. The flag has no effect on the server side.
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; }
  }
}
