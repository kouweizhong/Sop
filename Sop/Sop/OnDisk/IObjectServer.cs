﻿// Scalable Object Persistence (SOP) Framework, by Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: LGPL v2.1
// Have fun Coding! ;)

using System;

namespace Sop.OnDisk
{
    /// <summary>
    /// Object Server interface specifically for On Disk use-case.
    /// </summary>
    internal interface IObjectServer : Sop.IObjectServer
    {
        int HintSizeOnDisk { get; }
        void Initialize(string filename);
        new bool IsDirty { get; set; }
        new bool IsNew { get; set; }
        new string Path { get; set; }
    }
}