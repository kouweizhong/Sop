﻿using Sop.OnDisk.File;

namespace Sop
{
    public partial class ObjectServer
    {
        /// <summary>
        /// Open in read only mode.
        /// </summary>
        /// <param name="serverFilename">Filename of Object Server to open in read only mode.</param>
        /// <returns></returns>
        public static Sop.IObjectServer OpenReadOnly(string serverFilename)
        {
            return OpenReadOnly(serverFilename, new Profile());
        }
        /// <summary>
        /// License Key.
        /// </summary>
        public static string LicenseKey
        {
            get { return OnDisk.ObjectServer.LicenseKey; }
            set { OnDisk.ObjectServer.LicenseKey = value; }
        }

        /// <summary>
        /// Open Object Server in read only mode.
        /// </summary>
        /// <param name="serverFilename"></param>
        /// <param name="serverProfile"></param>
        /// <returns></returns>
        public static Sop.IObjectServer OpenReadOnly(string serverFilename, Preferences preferences)
        {
            return new OnDisk.ObjectServer(serverFilename, null, preferences, true);
        }

        /// <summary>
        /// Open Object Server and begin a lightweight (high speed!) SOP transaction.
        /// </summary>
        /// <param name="serverFilename"></param>
        /// <returns></returns>
        public static Sop.ObjectServer OpenWithTransaction(string serverFilename)
        {
            return OpenWithTransaction(serverFilename, new Preferences());
        }

        /// <summary>
        /// Open Object Server and begin a low-level SOP transaction (transaction with no-ACID properties).
        /// </summary>
        /// <param name="serverFilename"></param>
        /// <param name="serverProfile"></param>
        /// <returns></returns>
        public static Sop.ObjectServer OpenWithTransaction(string serverFilename, Preferences preferences)
        {
            return new ObjectServer(Sop.Transaction.Transaction.BeginOpenServer(serverFilename, preferences));
        }

        /// <summary>
        /// Maximum number of File Stream Instance count.
        /// 
        /// NOTE: SOP manages File Stream instances so when total number of
        /// Opened File Streams reaches maximum amount, SOP will close down
        /// least recently used File Stream. This management is important for 
        /// scalability as File Streams are expensive resources, OS imposes
        /// maximum limit and .
        /// </summary>
        public static int MaxFileStreamInstanceCount
        {
            get { return FileStream.MaxInstanceCount; }
            set { FileStream.MaxInstanceCount = value; }
        }

        /// <summary>
        /// Rollback all pending transactions left open by previous Application run.
        /// </summary>
        public static void RollbackAll(string serverRootPath)
        {
            Sop.Transaction.TransactionRoot.RollbackAll(serverRootPath);
        }
    }
}