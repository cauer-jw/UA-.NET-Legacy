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
using System.Windows.Forms;
using System.ServiceProcess;

using Opc.Ua;
using Opc.Ua.Server;
using Opc.Ua.Configuration;

namespace Opc.Ua.Com.Client
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            ApplicationInstance application = new ComServerWrapperApplication();
            application.ApplicationName   = "UA COM Server Wrapper";
            application.ApplicationType   = ApplicationType.Server;
            application.ConfigSectionName = "Opc.Ua.ComServerWrapper";

            try
            {
                // process and command line arguments.
                if (application.ProcessCommandLine())
                {
                    return;
                }

                // Warn if the system clock looks invalid (e.g. dead CMOS battery reset to ~year 2000).
                if (DateTime.UtcNow.Year < 2020)
                {
                    Utils.Trace(Utils.TraceMasks.Error,
                        "System clock appears invalid ({0:u}). Check CMOS battery. " +
                        "MaxRequestAge=0 in config suppresses the timestamp check as a workaround.",
                        DateTime.UtcNow);
                }

                // check if running as a service.
                if (!Environment.UserInteractive)
                {
                    application.StartAsService(new ComWrapperServer());
                    return;
                }

                // load the application configuration.
                application.LoadApplicationConfiguration(false);

                // check the application certificate.
                application.CheckApplicationInstanceCertificate(false, 0);

                // start the server.
                application.Start(new ComWrapperServer());

                // run the application interactively.
                Application.Run(new ServerForm(application));
            }
            catch (Exception e)
            {
                ExceptionDlg.Show(application.ApplicationName, e);
                return;
            }
        }
    }
}
