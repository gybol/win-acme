﻿using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using PKISharp.WACS.Services;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PKISharp.WACS.Plugins.CsrPlugins
{
    class Ec : CsrPlugin<Ec, EcOptions>, IDisposable
    {
        private PemService _pemService;
        private ECDsaCng _algorithm;
        private AsymmetricCipherKeyPair _keyPair;

        public Ec(ILogService log, PemService pemService, EcOptions options) : base(log, options)
        {
            _pemService = pemService;
        }

        public override CertificateRequest GenerateCsr(X500DistinguishedName commonName)
        {
            return new CertificateRequest(commonName, Algorithm, HashAlgorithmName.SHA256);
        }

        /// <summary>
        /// Create or return algorithm
        /// </summary>
        private ECDsaCng Algorithm
        {
            get {
                if (_algorithm == null)
                {
                    var bcKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(GetPrivateKey());
                    var pkcs8Blob = bcKeyInfo.GetDerEncoded();
                    var importedKey = CngKey.Import(pkcs8Blob, CngKeyBlobFormat.Pkcs8PrivateBlob);
                    _algorithm = new ECDsaCng(importedKey);

                }
                return _algorithm;
            }
        }

        /// <summary>
        /// Generate or return private key
        /// </summary>
        /// <returns></returns>
        public override AsymmetricKeyParameter GetPrivateKey()
        {
            if (_keyPair == null)
            {
                if (_cacheData == null)
                {
                    _keyPair = GenerateNewKeyPair();
                    _cacheData = _pemService.GetPem(_keyPair.Private);
                }
                else
                {
                    try
                    {
                        _keyPair = _pemService.ParsePem<AsymmetricCipherKeyPair>(_cacheData);
                        if (_keyPair == null)
                        {
                            throw new InvalidDataException("key");
                        }
                    }
                    catch
                    {
                        _log.Error($"Unable to read cache data, creating new key...");
                        _cacheData = null;
                        return GetPrivateKey();
                    }
                }
            }
            return _keyPair.Private;
        }

        private AsymmetricCipherKeyPair GenerateNewKeyPair()
        {
            var generator = new ECKeyPairGenerator();
            var curve = GetEcCurve();
            var genParam = new ECKeyGenerationParameters(
                SecNamedCurves.GetOid(curve),
                new SecureRandom());
            generator.Init(genParam);
            return generator.GenerateKeyPair();
        }

        /// <summary>
        /// Parameters to generate the key for
        /// </summary>
        /// <returns></returns>
        private string GetEcCurve()
        {
            var ret = "secp384r1"; // Default
            try
            {
                var config = Properties.Settings.Default.ECCurve;
                DerObjectIdentifier curveOid = null;
                try
                {
                    curveOid = SecNamedCurves.GetOid(config);
                }
                catch {}
                if (curveOid != null)
                {
                    ret = config;
                }
                else
                {
                    _log.Warning("Unknown curve {ECCurve}", config);
                }
            }
            catch (Exception ex)
            {
                _log.Warning("Unable to get EC name, error: {@ex}", ex);
            }
            _log.Debug("ECCurve: {ECCurve}", ret);
            return ret;
        }

        #region IDisposable

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_algorithm != null)
                    {
                        _algorithm.Dispose();
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
