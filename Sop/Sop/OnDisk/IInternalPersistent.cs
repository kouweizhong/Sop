// Scalable Object Persistence (SOP) Framework, by Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: LGPL v2.1
// Have fun Coding! ;)

using System;
using System.Collections;
using Sop.Mru;

[assembly: CLSCompliant(true)]

namespace Sop.OnDisk
{
    using Mru;

    /// <summary>
    /// InternalPersistent Object Reference interface
    /// </summary>
    internal interface IInternalPersistentRef
    {
        /// <summary>
        /// DataAddress
        /// </summary>
        long DataAddress { get; set; }

        /// <summary>
        /// MRU cache manager
        /// </summary>
        IMruManager MruManager { get; set; }
    }
}