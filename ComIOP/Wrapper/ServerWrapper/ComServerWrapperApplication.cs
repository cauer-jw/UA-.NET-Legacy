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

using Opc.Ua;
using Opc.Ua.Configuration;

namespace Opc.Ua.Com.Client
{
    internal class ComServerWrapperApplication : ApplicationInstance
    {
        protected override void Install(bool silent, Dictionary<string, string> args)
        {
            base.Install(silent, args);

            if (!InstallConfig.InstallAsService)
            {
                return;
            }

            // In silent mode base.Install may return without throwing if install preconditions fail.
            // Only apply post-install settings when the service actually exists.
            Service service = ServiceManager.GetService(InstallConfig.ApplicationName);

            if (service == null)
            {
                Utils.Trace(Utils.TraceMasks.Error, "Skipping post-install service settings because service '{0}' is not installed.", InstallConfig.ApplicationName);
                return;
            }

            try
            {
                ServicePostInstallConfigurator.ConfigureForProduction(InstallConfig.ApplicationName);
                Utils.Trace(Utils.TraceMasks.Information, "Configured service '{0}' for delayed auto start and restart recovery.", InstallConfig.ApplicationName);
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Could not configure post-install service settings for '{0}'.", InstallConfig.ApplicationName);
                throw ServiceResultException.Create(StatusCodes.BadConfigurationError, e, "The service was installed but the post-install service configuration failed for '{0}'.", InstallConfig.ApplicationName);
            }
        }
    }
}