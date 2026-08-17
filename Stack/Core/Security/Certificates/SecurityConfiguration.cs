/*
* Copyright (c) 2005-2026 - OPC Foundation
* 
* All Rights Reserved.
* 
* NOTICE:  All information contained herein is, and remains the property of 
* OPC Foundation. The intellectual and technical concepts contained 
* herein are proprietary to OPC Foundation and may be covered by 
* U.S. and Foreign Patents, patents in process, and are protected by trade secret 
* or copyright law. Dissemination of this information or reproduction of this 
* material is strictly forbidden unless prior written permission is obtained 
* from OPC Foundation.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Opc.Ua
{
    #region SecurityConfiguration Class
    /// <summary>
    /// The security configuration for the application.
    /// </summary>
    public partial class SecurityConfiguration
    {
        #region Public Methods
        /// <summary>
        /// Adds a certificate as a trusted peer.
        /// </summary>
        public void AddTrustedPeer(byte[] certificate)
        {
            this.TrustedPeerCertificates.TrustedCertificates.Add(new CertificateIdentifier(certificate));
        }

        /// <summary>
        /// Validates the security configuration.
        /// </summary>
        public void Validate()
        {
            if (m_applicationCertificate == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, "ApplicationCertificate must be specified.");
            }

            TrustedIssuerCertificates = CreateDefaultTrustList(TrustedIssuerCertificates);
            TrustedPeerCertificates = CreateDefaultTrustList(TrustedPeerCertificates);

            if (!String.Equals(m_certificateSignatureAlgorithm, "SHA1", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(m_certificateSignatureAlgorithm, "SHA256", StringComparison.OrdinalIgnoreCase))
            {
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, "CertificateSignatureAlgorithm must be 'SHA1' or 'SHA256'.");
            }

            //set a default rejected certificate store.
            if (RejectedCertificateStore == null)
            {
                RejectedCertificateStore = new CertificateStoreIdentifier();
                RejectedCertificateStore.StoreType = CertificateStoreType.Directory;
                RejectedCertificateStore.StorePath = "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\RejectedCertificates";
            }             
        }

        /// <summary>
        /// Ensure valid trust lists.
        /// </summary>
        private CertificateTrustList CreateDefaultTrustList(CertificateTrustList trustList)
        {
            if (trustList != null)
            {
                if (trustList.StorePath != null)
                {
                    return trustList;
                }
            }

            return new CertificateTrustList();
        }

        #endregion
    }
    #endregion
}
