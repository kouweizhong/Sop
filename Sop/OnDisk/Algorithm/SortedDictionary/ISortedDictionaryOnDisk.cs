﻿// Scalable Object Persistence (SOP) Framework, main contact - Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: MIT
// Have fun Coding! ;)

using Sop.OnDisk.Algorithm.BTree;
using Sop.OnDisk.Algorithm.Collection;
using Sop.OnDisk.DataBlock;
using Sop.OnDisk.IO;
using Sop.Persistence;

namespace Sop.OnDisk.Algorithm.SortedDictionary
{

    internal interface ISortedDictionaryOnDisk : Collection.ICollectionOnDisk, Sop.ISortedDictionaryOnDisk,
                                                 IPersistent
    {
        /// <summary>
        /// Index Block Size
        /// </summary>
        DataBlockSize IndexBlockSize { get; }

        void Initialize(File.IFile file);
        bool ChangeRegistry { get; set; }
        BTreeItemOnDisk CurrentItemOnDisk { get; }
        new long DataAddress { get; set; }
        IDataBlockDriver DataBlockDriver { get; }
        int HintKeySizeOnDisk { get; set; }
        int HintValueSizeOnDisk { get; set; }
        bool IsDataLongInt { get; set; }
        new bool IsUnique { get; set; }
        new bool IsDataInKeySegment { get; set; }
        bool IsItMe(CollectionOnDisk other);
        bool IsOnInnerMemberPackEventHandlerSet { get; }
        bool IsOnPackEventHandlerSet { get; }

        OnDiskBinaryReader OnDiskBinaryReader { get; }
        event Sop.OnObjectPack OnInnerMemberKeyPack;
        event Sop.OnObjectUnpack OnInnerMemberKeyUnpack;
        event Sop.OnObjectPack OnInnerMemberValuePack;
        event Sop.OnObjectUnpack OnInnerMemberValueUnpack;
        event Sop.OnObjectPack OnKeyPack;
        event Sop.OnObjectUnpack OnKeyUnpack;
        event Sop.OnObjectPack OnValuePack;
        event Sop.OnObjectUnpack OnValueUnpack;

        OnObjectPack OnKeyPackEventHandler { get; }
        OnObjectUnpack OnKeyUnpackEventHandler { get; }
        OnObjectPack OnValuePackEventHandler { get; }
        OnObjectUnpack OnValueUnpackEventHandler { get; }

        Sop.Transaction.ITransactionLogger ParentTransactionLogger { get; set; }
        object ReadFromBlock(Sop.DataBlock source);
        IBTreeNodeOnDisk CurrentNode { get; }
        IBTreeNodeOnDisk RootNode { get; }
        void SetId(long address);
        int Size { get; }
        Sop.Collections.BTree.IBTree Synchronized();
        void SetCurrentValueInMemoryData(object value);

        bool RemoveInMemory(long dataAddress, Transaction.ITransactionLogger transaction);

        /// <summary>
        /// Transaction Logger
        /// </summary>
        new Transaction.ITransactionLogger Transaction { get; set; }
    }
}