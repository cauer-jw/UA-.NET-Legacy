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
using Opc.Ua.Server;
using Opc.Ua.Com;
using Opc.Ua.Com.Client;
using OpcRcw.Da;

namespace Opc.Ua.Com.Client
{
    /// <summary>
    /// A class that implements the IOPCDataCallback interface.
    /// </summary>
    internal class ComDaDataCallback : OpcRcw.Da.IOPCDataCallback, IDisposable
    {
	    #region Constructors
	    /// <summary>
	    /// Initializes the object with the containing subscription object.
	    /// </summary>
	    public ComDaDataCallback(ComDaGroup group)
	    { 
            // save group.
            m_group = group;

		    // create connection point.
		    m_connectionPoint = new ConnectionPoint(group.Unknown, typeof(OpcRcw.Da.IOPCDataCallback).GUID);

		    // advise.
		    m_connectionPoint.Advise(this);
	    }
	    #endregion
        
        #region IDisposable Members
        /// <summary>
        /// Frees any unmanaged resources.
        /// </summary>
        public void Dispose()
        {   
            Dispose(true);
        }

        /// <summary>
        /// An overrideable version of the Dispose.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (m_connectionPoint != null)
            {
                if (disposing)
                {
                    m_connectionPoint.Dispose();
                    m_connectionPoint = null;
                }
            }
        }
        #endregion

	    #region Public Properties
        /// <summary>
        /// Whether the callback is connected.
        /// </summary>
        public bool Connected 
        {
            get 
            {
                return m_connectionPoint != null;
            }
        }
        #endregion

	    #region IOPCDataCallback Members
	    /// <summary>
	    /// Called when a data changed event is received.
	    /// </summary>
	    public void OnDataChange(
		    int                  dwTransid,
		    int                  hGroup,
		    int                  hrMasterquality,
		    int                  hrMastererror,
		    int                  dwCount,
		    int[]                phClientItems,
		    object[]             pvValues,
		    short[]              pwQualities,
		    System.Runtime.InteropServices.ComTypes.FILETIME[] pftTimeStamps,
		    int[]                pErrors)
	    {
		    try
		    {
			    // unmarshal item values.
			    DaValue[] values = ComDaGroup.GetItemValues(
				    dwCount,
				    pvValues, 
				    pwQualities, 
				    pftTimeStamps, 
				    pErrors);

			    int[] clientHandles = phClientItems;

			    if (clientHandles == null || clientHandles.Length != values.Length)
			    {
				    Utils.Trace(
				        Utils.TraceMasks.Information,
				        "OnDataChange handle mismatch: expected={0}, received={1}",
				        values.Length,
				        clientHandles != null ? clientHandles.Length : -1);
				    clientHandles = m_group.ResolveCallbackClientHandles(values.Length, clientHandles);
			    }

			    // invoke the callback.
			    m_group.OnDataChange(clientHandles, values);
		    }
		    catch (Exception e) 
		    { 
                Utils.Trace(e, "Unexpected error processing OnDataChange callback.");
		    }
	    }

	    /// <summary>
	    /// Called when an asynchronous read operation completes.
	    /// </summary>
	    public void OnReadComplete(
		    int                  dwTransid,
		    int                  hGroup,
		    int                  hrMasterquality,
		    int                  hrMastererror,
		    int                  dwCount,
		    int[]                phClientItems,
		    object[]             pvValues,
		    short[]              pwQualities,
		    System.Runtime.InteropServices.ComTypes.FILETIME[] pftTimeStamps,
		    int[]                pErrors)
	    {
		    try
		    {
			    // unmarshal item values.
			    DaValue[] values = ComDaGroup.GetItemValues(
				    dwCount,
				    pvValues, 
				    pwQualities, 
				    pftTimeStamps, 
				    pErrors);

			    // invoke the callback.
                m_group.OnReadComplete(dwTransid, phClientItems, values);
		    }
		    catch (Exception e) 
		    { 
                Utils.Trace(e, "Unexpected error processing OnReadComplete callback.");
		    }
	    }
        
	    /// <summary>
	    /// Called when an asynchronous write operation completes.
	    /// </summary>
	    public void OnWriteComplete(
		    int   dwTransid,
		    int   hGroup,
		    int   hrMastererror,
		    int   dwCount,
		    int[] phClientItems,
		    int[] pErrors)
	    {
		    try
		    {
                m_group.OnWriteComplete(dwTransid, phClientItems, pErrors);
		    }
		    catch (Exception e) 
		    { 
                Utils.Trace(e, "Unexpected error processing OnWriteComplete callback.");
		    }
	    }
                    
	    /// <summary>
	    /// Called when an asynchronous operation is cancelled.
	    /// </summary>
	    public void OnCancelComplete(
		    int dwTransid,
		    int hGroup)
	    {
	    }
	    #endregion

	    #region Private Members
	    private ComDaGroup m_group;
	    private ConnectionPoint m_connectionPoint;
	    #endregion
    }
}
