﻿// Scalable Object Persistence (SOP) Framework, main contact - Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: MIT
// Have fun Coding! ;)

using Sop.Mru;

namespace Sop.OnDisk.Algorithm.Collection
{
    /// <summary>
    /// Collection Cache interface.
    /// </summary>
    internal interface ICollectionCache
    {
        /// <summary>
        /// Objects MRU cache manager.
        /// </summary>
        IMruManager MruManager { get; set; }
        /// <summary>
        /// Data Blocks cache.
        /// </summary>
        Collections.Generic.ISortedDictionary<long, Sop.DataBlock> Blocks { get; set; }
    }
}
