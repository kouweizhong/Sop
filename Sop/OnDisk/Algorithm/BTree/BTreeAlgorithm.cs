﻿// Scalable Object Persistence (SOP) Framework, main contact - Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: MIT
// Have fun Coding! ;)

using System;
using System.Collections;
using System.Collections.Generic;
using Sop.Mru;
using Sop.OnDisk.Algorithm.Collection;
using Sop.OnDisk.Algorithm.SortedDictionary;
using Sop.OnDisk.DataBlock;
using Sop.Persistence;
using Sop.SpecializedDataStore;

namespace Sop.OnDisk.Algorithm.BTree
{
    internal partial class BTreeAlgorithm : CollectionOnDisk, ICloneable, IBTreeAlgorithm
    {
        #region ctors
        public BTreeAlgorithm()
        {
        }

        public BTreeAlgorithm(File.IFile file)
            : this(file, new BTreeDefaultComparer())
        {
        }

        public BTreeAlgorithm(File.IFile file, IComparer comparer)
            : this(file, comparer, string.Empty)
        {
        }

        public BTreeAlgorithm(File.IFile file,
                              IComparer comparer,
                              string name)
            : this(file, comparer, name, null)
        {
        }

        public BTreeAlgorithm(File.IFile file,
                              IComparer comparer,
                              string name,
                              IDataBlockDriver dataBlockDriver
            )
            : this(file, comparer, name, null, false)
        {
        }

        public BTreeAlgorithm(File.IFile file,
                              IComparer comparer,
                              string name,
                              IDataBlockDriver dataBlockDriver,
                              bool isDataInKeySegment
            )
        {
            this.Name = name;
            var p = new[]
                                                   {
                                                       new KeyValuePair<string, object>("comparer", comparer),
                                                       new KeyValuePair<string, object>("DataBlockDriver",
                                                                                        dataBlockDriver)
                                                   };
            IsDataInKeySegment = isDataInKeySegment;
            Initialize(file, p);
        }
        #endregion

        /// <summary>
        /// Open the collection
        /// </summary>
        public override void Open()
        {
            RemoveDeletedDataBlocks();
            base.Open();
            if (DataSet != null)
                DataSet.IsDeletedBlocksList = IsDeletedBlocksList;
            KeySet.IsDeletedBlocksList = IsDeletedBlocksList;
            if (DataSet != null)
                DataSet.Open();
            KeySet.Open();
            if (RootNode != null)
            {
                if (!RootNeedsReload)
                    MoveFirst();
            }
            else
            {
                RootNode = new BTreeNodeOnDisk(this);
            }

            // log the read HeaderData information useful for debugging.
            //Log.Logger.Instance.Warning(ToString());
        }

        /// <summary>
        /// Get/Set comparer object used in sorting items of the collection
        /// </summary>
        public IComparer Comparer
        {
            get { return _comparer; }
            set
            {
                _comparer = value;
                _comparerWrapper = null;
            }
        }
        // deserialize value from entry's disk buffer...
        private object GetValue(BTreeItemOnDisk entry)
        {
            var itm = entry;
            if (itm == null) return null;
            if (!itm.ValueLoaded || itm.IsDisposed)
            {
                Sop.DataBlock d = itm.Value.DiskBuffer;
                var itemOnDisk = ReadFromBlock(d);
                if (itemOnDisk != null)
                {
                    if (itemOnDisk is ItemOnDisk)
                    {
                        var iod = (ItemOnDisk)itemOnDisk;
                        iod.DiskBuffer = itm.Value.DiskBuffer;
                        itm.Value = iod;
                        if (iod.Data == null && iod.DataIsUserDefined && onValueUnpack != null)
                            iod.Data = onValueUnpack(OnDiskBinaryReader);

                        // fold DiskBuffer to save memory...
                        if (iod.Data != null && itm.Value != null && itm.Value.diskBuffer != null)
                        {
                            itm.Value.diskBuffer.Fold();
                            if (itm.Value.diskBuffer.CountMembers() >= File.Profile.BigDataBlockCount)
                            {
                                var r = itm.Value.Data;
                                // nullify big data to conserve memory
                                itm.Value.Data = null;
                                return r;
                            }
                        }
                    }
                    else
                        throw new SopException(
                            string.Format("Unexpected item of type {0} was deserialized.",
                                          itemOnDisk.GetType()));
                }
                itm.ValueLoaded = true;
            }
            return itm.Value != null ? itm.Value.Data : null;
        }

        /// <summary>
        /// Current Item's Value getter/setter.
        /// </summary>
        public virtual object CurrentValue
        {
            get
            {
                LoadSequentialReadBatchedIDs();
                BeginTreeMaintenance();
                try
                {
                    var itm = (BTreeItemOnDisk)CurrentEntry;
                    if (itm == null) return null;
                    bool isItemDisposed = false;
                    if (!itm.ValueLoaded || (isItemDisposed = itm.IsDisposed))
                    {
                        long da = GetId(itm.Value.DiskBuffer);
                        if (da < 0 && isItemDisposed && itm.Value.Data is ISortedDictionary)
                        #region load the Data Store given its Data Address...
                        {
                            long l;
                            if (long.TryParse(itm.Value.Data.ToString(), out l) && l >= 0)
                                da = l;
                            var d = GetDiskBlock(DataSet, da);

                            //if (HintValueSizeOnDisk == 0)
                            //    HintValueSizeOnDisk = d.CountMembers()*(int) IndexBlockSize;

                            var store = ReadFromBlock(d);
                            if (!(store is BTreeAlgorithm))
                                throw new SopException(
                                    string.Format(
                                        "Expecting to deserialize a BTreeAlgorithm type but got {0}",
                                        store.GetType()));
                            // assign the BTreeAlgorithm read from disk as Specialized Data Store's "real collection".
                            var currValue = (SortedDictionaryOnDisk) ((SpecializedStoreBase) itm.Value.Data).Collection;
                            currValue.BTreeAlgorithm= (BTreeAlgorithm)store;
                            currValue.IsDisposed = false;
                            itm.Value.Data = currValue;
                        }
                        #endregion
                        else
                        {
                            Sop.DataBlock d;
                            if (isItemDisposed && itm.Value != null && itm.Value.Data is SpecializedStoreBase)
                            {
                                ((SpecializedStoreBase)itm.Value.Data).Collection = null;
                                // just load object from buffer...
                                d = itm.Value.DiskBuffer;
                            }
                            else
                            {
                                d = GetDiskBlock(DataSet, da);
                                itm.Value.DiskBuffer = d;
                                itm.Value.DiskBuffer.IsHead = true;
                            }
                            //if (HintValueSizeOnDisk == 0)
                            //    HintValueSizeOnDisk = d.CountMembers() * (int)IndexBlockSize;
                            return GetValue(itm);
                        }
                        itm.ValueLoaded = true;
                    }
                    return itm.Value != null ? itm.Value.Data : null;
                }
                finally
                {
                    EndTreeMaintenance();
                }
            }
            set
            {
                BeginTreeMaintenance();
                BTreeNodeOnDisk currNode = CurrentNode;
                if (currNode != null && CurrentItemReference.NodeItemIndex >= 0)
                {
                    BTreeItemOnDisk itm = currNode.Slots[CurrentItemReference.NodeItemIndex];
                    if (CompareSimpleType(itm.Value.Data, value))
                    {
                        EndTreeMaintenance();
                        return;
                    }

                    itm.Value.Data = value;

                    #region Check if we can remove these lines...
                    if (itm.Value.diskBuffer == null)
                        itm.Value.diskBuffer = CreateBlock();
                    else
                    {
                        itm.Value.DiskBuffer.IsDirty = false;
                        itm.Value.DiskBuffer.IsDirty = true;
                        var dataBlock = value as Sop.DataBlock;
                        if (dataBlock != null)
                        {
                            Sop.DataBlock db = itm.Value.DiskBuffer;
                        }
                    }
                    #endregion

                    if (!IsDataInKeySegment)
                    {
                        if (AutoFlush)
                            SetBigDataValue(itm);
                        else
                        {
                            //itm.Value.diskBuffer.IsHead = true;
                            itm.ValueLoaded = true;
                            itm.Value.IsDirty = true;
                            itm.IsDirty = true;
                        }
                    }
                    itm.Value.DataIsUserDefined = false;

                    IsDirty = true;
                    //** if current node has Disk ID, SaveNodeToDisk will not save it, it will be saved 
                    //** when this Node gets offloaded to Disk in OnMaxCapacity event or during B-Tree Save
                    currNode.SaveNodeToDisk(this);
                    EndTreeMaintenance();
                    RegisterChange(true);
                }
                else
                {
                    EndTreeMaintenance();
                    throw new SopException("BTree has no Current Entry.");
                }
            }
        }

        public bool Detach()
        {
            BTreeNodeOnDisk currNode = CurrentNode;
            if (currNode == null || CurrentItemReference.NodeItemIndex < 0)
                return false;
            BTreeItemOnDisk itm = currNode.Slots[CurrentItemReference.NodeItemIndex];
            itm.Value.Data = null;
            if (IsDataInKeySegment)
            {
                return true;
            }
            // reset the disk buffer so it won't get recycled... (intention is to re-attach it as another entry)
            itm.Value.diskBuffer = CreateBlock();
            itm.ValueLoaded = true;
            itm.Value.IsDirty = false;
            itm.IsDirty = true;
            itm.Value.DataIsUserDefined = false;
            IsDirty = true;
            return true;
        }

        /// <summary>
        /// true if collection is open, false otherwise
        /// </summary>
        public override bool IsOpen
        {
            get { return base.IsOpen || (DataSet != null && DataSet.IsOpen) || (KeySet != null && KeySet.IsOpen); }
        }

        /// <summary>
        /// Save the modified nodes/items to disk/virtual store.
        /// </summary>
        public override void Flush()
        {
            Log.Logger.Instance.Log(Log.LogLevels.Verbose, "BTreeAlgorithm.Flush {0}: Enter.", Name);
            if (RootNode == null)
            {
                Log.Logger.Instance.Log(Log.LogLevels.Warning, "Flush: RootNode == null detected.");
                return;
            }
            RemoveDeletedDataBlocks();
            if (PromoteLookup.Count > 0)
            {
                Log.Logger.Instance.Log(Log.LogLevels.Information, "BTreeAlgorithm.Flush: PromoteLookup(Count={0}), SaveState = {1}, calling SaveNode(PromoteLookup).",
                    PromoteLookup.Count, SaveState);
                SaveNode(PromoteLookup);
                ClearPromoteLookup();
                //PromoteLookup.Clear();
            }
            if (!IsDirty)
            {
                if (Blocks.Count > 0)
                {
                    Log.Logger.Instance.Log(Log.LogLevels.Information, "Flush: BTreeAlgorithm isn't dirty, Blocks Count = {0} returning.", Blocks.Count);
                    SaveBlocks(false);
                }
                return;
            }

            if (DataSet != null && DataSet.DataBlockDriver != null &&
                (DataSet.MruManager == null || DataSet.OnDiskBinaryReader == null))
                DataSet.Open();
            if (KeySet.DataBlockDriver != null &&
                (KeySet.MruManager == null || KeySet.OnDiskBinaryReader == null))
                KeySet.Open();
            if (RootNode != null && (Count > 0 || RootNode.GetAddress(this) >= 0) &&
                (DataSet == null || DataSet.IsOpen) && KeySet.IsOpen)
            {
                SaveState |= SaveTypes.CollectionSave;
                OnMaxCapacity(RootNode);
                RootNode.IsDirty = false;
                SaveState ^= SaveTypes.CollectionSave;
            }
            bool reSave = false;
            base.Flush();

            if (PromoteLookup.Count > 0)
            {
                SaveNode(PromoteLookup);
                ClearPromoteLookup();
                //PromoteLookup.Clear();
            }

            if (DataSet != null && DataSet.IsOpen)
            {
                if (DataSet.MruManager != null && DataSet.OnDiskBinaryReader != null)
                {
                    reSave = DataSet.DataAddress == -1;
                    bool hdsame = DataSet.HeaderData == HeaderData;
                    if (hdsame)
                        DataSet.HeaderData = null;
                    DataSet.Flush();
                    if (hdsame)
                        DataSet.HeaderData = HeaderData;
                }
            }
            if (KeySet.IsOpen)
            {
                if (KeySet.MruManager != null && KeySet.OnDiskBinaryReader != null)
                {
                    if (!reSave)
                        reSave = KeySet.DataAddress == -1;
                    bool hdsame = KeySet.HeaderData == HeaderData;
                    if (hdsame)
                        KeySet.HeaderData = null;
                    KeySet.Flush();
                    if (hdsame)
                        KeySet.HeaderData = HeaderData;
                }
            }
            if (!reSave)
            {
                Log.Logger.Instance.Log(Log.LogLevels.Information, "Flush: reSave is false, Blocks Count = {0} base.Flush will be skipped.", Blocks.Count);
                if (Blocks.Count > 0) SaveBlocks(false);
                return;
            }
            IsDirty = true;
            base.Flush();

            Log.Logger.Instance.Log(Log.LogLevels.Verbose, "BTreeAlgorithm.Flush {0}: Exit.", Name);
        }
        public bool AddIfNotExist(BTreeItemOnDisk item)
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't Add item to a Close ObjectStore.");
            if (HintSequentialRead)
                HintSequentialRead = false;
            BeginTreeMaintenance();

            bool uniqueSetting = IsUnique;
            IsUnique = true;
            bool r = RootNode.Add(this, item);
            IsUnique = uniqueSetting;
            if (r)
            {
                if (AutoFlush)
                    SetBigDataValue(item);
            }
            else
            {
                EndTreeMaintenance();
                return r;
            }

            Distribute();
            Promote();
            EndTreeMaintenance();
            UpdateCount(UpdateCountType.Increment);
            SaveBlocks(MaxBlocks, false);
            RegisterChange(true);
            // Current item pointer points to null after add of an item.
            SetCurrentItemAddress(-1, 0);
            return r;
        }

        /// <summary>
        /// Add Item(Key & Value) to the proper location on B-Tree.
        /// </summary>
        /// <param name="item"></param>
        public void Add(BTreeItemOnDisk item)
        {
#if (DEBUG)
            Log.Logger.Instance.Verbose("BTreeAlgorithm.Add.");
#endif

            if (RootNode == null)
                throw new InvalidOperationException("Can't Add item to a Close ObjectStore.");
            if (HintSequentialRead)
                HintSequentialRead = false;
            BeginTreeMaintenance();
            bool dataSaved = false;
            if (!IsUnique)
            {
                if (AutoFlush)
                {
                    SetBigDataValue(item);
                    dataSaved = true;
                }
            }
            if (RootNode.Add(this, item))
            {
                if (!dataSaved && AutoFlush)
                    SetBigDataValue(item);
            }
            else
            {
                EndTreeMaintenance();
                SetCurrentItemAddress(-1, 0);
                throw new DuplicateKeyException(string.Format("Item with key {0} already exists.", item.Key));
            }

            Distribute();
            Promote();
            EndTreeMaintenance();
            UpdateCount(UpdateCountType.Increment);
            SaveBlocks(MaxBlocks, false);
            RegisterChange(true);
            // Current item pointer points to null after add of an item.
            SetCurrentItemAddress(-1, 0);
        }

        //private const int ObjectBlockCountThreshold = 64;
        /// <summary>
        /// If in AutoFlush mode and data Value is not saved in Key segment,
        /// this method will Add/Update Big Data Value to disk and at end of the process,
        /// set the Value to null to conserve memory.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private bool SetBigDataValue(BTreeItemOnDisk item)
        {
            if (!IsDataInKeySegment)
            {
                // load meta data so Blocks layout in-memory can get recreated...
                long da = GetId(item.Value.DiskBuffer);
                if (da >= 0)
                {
                    if (!item.Value.DiskBuffer.IsFolded)
                    {
                        if (item.Value.DiskBuffer.IsFoldedInfoOnDisk)
                        {
                            // read the block chain info so these blocks will not be replaced during update!
                            var metaBlocks = DataBlockDriver.ReadBlockInfoFromDisk(this, da);
                            // assign read block chain info to a DataBlock simulating a "Folded" scenario.
                            item.Value.diskBuffer.foldedDataAddresses = metaBlocks.ToArray();
                            item.Value.diskBuffer.contiguousBlockCount = metaBlocks[0].BlockCount;
                        }
                    }
                    // delete the blocks on disk if they are big and data being set is null
                    if (item.Value.Data == null && 
                        item.Value.DiskBuffer.IsFolded && 
                        item.Value.DiskBuffer.CountMembers() >= 1024)
                    {
                        DataSet.DataBlockDriver.Remove(DataSet, item.Value.DiskBuffer);
                        item.Value.DiskBuffer = CreateBlock();
                    }
                }

                // save big data to disk
                if (item.Value.diskBuffer != null)
                    item.Value.diskBuffer.Unfold(this);
                Sop.DataBlock b = WriteToBlock(item.Value);
                DataSet.DataBlockDriver.SetDiskBlock(DataSet, b, false, false);
                var cnt = b.ProcessHeadSets();

                // store blocks to the pool if block count < 500, otherwise direct write to disk!
                if (cnt < File.Profile.BigDataBlockCount)
                {
                    AddToBlocks(b, Blocks);
                    item.ValueLoaded = true;
                    item.Value.IsDirty = false;
                    item.IsDirty = false;
                    item.Value.diskBuffer.Fold();
                }
                else
                {
                    if (cnt <= 500)
                        AddToBlocks(b, Blocks);
                    else
                    {
                        // direct write to disk
                        var blocks = new Collections.Generic.SortedDictionary<long, Sop.DataBlock>();
                        AddToBlocks(b, blocks);
                        WriteBlocksToDisk(DataSet, blocks, false);
                    }
                    item.IsDirty = false;
                    item.Value.IsDirty = false;
                    item.ValueLoaded = false;
                    item.Value.diskBuffer.Fold();
                    // nullify big data to conserve memory
                    item.Value.Data = null;
                }
                return true;
            }
            return false;
        }

        #region Item Distribution to Sibling Nodes & Promotion to Parent Node processing related
        internal void Promote()
        {
            while (PromoteParent != null)
            {
                Log.Logger.Instance.Log(Log.LogLevels.Verbose, "ProcessPromotion: PromoteParent Node Address {0}.", PromoteParent.GetAddress(this));
                BTreeNodeOnDisk n = PromoteParent;
                short i = PromoteIndexOfNode;
                PromoteParent = null;
                PromoteIndexOfNode = 0;
                n.Promote(this, i);
            }
        }
        internal void Distribute()
        {
            if (DistributeSibling != null)
                Log.Logger.Instance.Log(Log.LogLevels.Information, "ProcessDistribution: DistributeSibling Node Address {0}.", DistributeSibling.GetAddress(this));
            while (DistributeSibling != null)
            {
                BTreeNodeOnDisk n = DistributeSibling;
                BTreeItemOnDisk item = DistributeItem;
                DistributeSibling = null;
                DistributeItem = null;
                if (DistributeLeftDirection)
                    n.DistributeToLeft(this, item);
                else
                    n.DistributeToRight(this, item);
            }
        }
        internal bool DistributeLeftDirection;
        internal BTreeNodeOnDisk DistributeSibling;
        internal BTreeItemOnDisk DistributeItem;
        #endregion


        /// <summary>
        /// Remove item from B-Tree.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(object item) // return true if found, else false
        {
            return Remove(item, false);
        }

        /// <summary>
        /// Remove "Item" from the tree. Doesn't throw exception if "Item" is not found
        /// </summary>
        /// <param name="item">Record to remove</param>
        /// <param name="removeAllOccurrence"> </param>
        public virtual bool Remove(object item, bool removeAllOccurrence) // return true if found, else false
        {
            QueryResult[] r;
            return Remove(new[] {new QueryExpression {Key = item}}, removeAllOccurrence, out r);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"> </param>
        /// <param name="removeAllOccurrence"></param>
        /// <param name="results"> </param>
        /// <returns></returns>
        public bool Remove(QueryExpression[] items, bool removeAllOccurrence,
            out QueryResult[] results)
        {
            if (items == null || items.Length == 0)
                throw new ArgumentNullException("items");
            if (RootNode == null)
                throw new InvalidOperationException("Can't Remove an item, ObjectStore is close.");
            bool found = false;

            results = new QueryResult[items.Length];
            BeginTreeMaintenance();
            for (int i = 0; i < items.Length; i++ )
            {
                if (items[i].Key == null) continue;
                bool valueFilter = items[i].ValueFilterFunc != null;
                while (Search(items[i].Key, valueFilter))
                {
                    if (!removeAllOccurrence && valueFilter)
                    {
                        do
                        {
                            if (!items[i].ValueFilterFunc(CurrentValue)) continue;
                            results[i].Found = true;
                            break;
                        } while (MoveNext() && Comparer.Compare(CurrentKey, items[i].Key) == 0);
                        if (!results[i].Found)
                            break;
                    }
                    else
                        results[i].Found = true;
                    found = true;
                    Remove();
                    if (!removeAllOccurrence)
                        break;
                }
            }
            EndTreeMaintenance();
            return found;
        }
        /// <summary>
        /// Search for Item in this collection
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Search(object item)
        {
            return Search(item, false);
        }
        /// <summary>
        /// Search for Item in this collection with an option
        /// to position the B-Tree current pointer to the 1st
        /// instance of the record having matching key with 'Item'.
        /// This is useful when searching a tree with items having
        /// duplicate Keys.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="goToFirstInstance"></param>
        /// <returns></returns>
        public bool Search(object item, bool goToFirstInstance)
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't Search item, ObjectStore is close.");
            if (HintSequentialRead)
                HintSequentialRead = false;
            if (RootNode != null && Count > 0)
            {
                BeginTreeMaintenance();
                try
                {
                    if (CurrentEntry == null ||
                        ComparerWrapper.Compare(CurrentEntry, item) != 0 ||
                        goToFirstInstance)
                    {
                        bool r = RootNode.Search(this, item, goToFirstInstance);
                        return r;
                    }
                }
                finally
                {
                    EndTreeMaintenance();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Close the collection
        /// </summary>
        public override void Close()
        {
            if (!IsOpen) return;

            if (!this.File.Profile.MemoryExtenderMode)
                RemoveDeletedDataBlocks();

            HintSequentialRead = false;
            base.Close();
            if (DataSet != null)
                DataSet.Close();
            if (KeySet != null)
                KeySet.Close();
            SetCurrentItemAddress(-1, 0);

            if (!IsCloned && RootNode != null)
                RootNode.Dispose();
            RootNode = null;
            currentDataBlock = null;
            currentEntry = null;

            if (IsCloned || Transaction == null) return;
            // remove this collection from being processed in Transaction Commit (as was fully flushed and stream was closed).
            ((Transaction.TransactionBase)Transaction).TrackModification(this, true);

            DisposeCachedItems();
        }
        private void DisposeCachedItems()
        {
            if (AutoDisposeItem)
            {
                // Dispose any cached objects that are disposable.
                MruManager.Dispose();
            }
        }

        /// <summary>
        /// Clear all items of the Collection
        /// </summary>
        public override void Clear()
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't Clear items of a Close ObjectStore.");

            RemoveDeletedDataBlocks();
            if (HintSequentialRead)
                HintSequentialRead = false;
            if (Count > 0)
            {
                CurrentSequence = 0;
                RootNode.Clear();
                SetCurrentItemAddress(-1, 0);
                if (DataSet != null)
                    DataSet.Clear();
                if (KeySet != null)
                    KeySet.Clear();
                base.Clear();
            }
            ClearPromoteLookup();
            //PromoteLookup.Clear();
        }

        /// <summary>
        /// Recycle DataSet, KeySet and DeletedBlocks' Segments
        /// </summary>
        public override void Delete()
        {
            if (DataBlockDriver != null)
            {
                RemoveDeletedDataBlocks();
                CollectionOnDisk cod = GetTopParent();
                IDataBlockRecycler db = cod.deletedBlocks;
                cod.deletedBlocks = null;
                if (DataSet != null)
                    DataSet.Delete();
                cod.deletedBlocks = db;
            }
            base.Delete();
        }

        #region Move functions
        /// <summary>
        /// Move current item pointer to the 1st item of the tree
        /// </summary>
        /// <returns></returns>
        public override bool MoveFirst()
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't MoveFirst, ObjectStore is close.");
            if (HintSequentialRead)
            {
                _sequentialReadBatchedIDs.Clear();
                _sequentialReadIndex = 0;
            }
            if (IsOpen && this.Count > 0)
            {
                BeginTreeMaintenance();
                bool r = RootNode.MoveFirst(this);
                EndTreeMaintenance();

                if (HintSequentialRead)
                {
                    _sequentialReadBatchedIDs.Clear();
                    _sequentialReadIndex = 0;
                    LoadSequentialReadBatchedIDs(false);
                }
                return r;
            }
            return false;
        }

        /// <summary>
        /// Move current item pointer to the previous item relative to the
        /// current item
        /// </summary>
        /// <returns></returns>
        public override bool MovePrevious()
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't MovePrevious, ObjectStore is close.");
            if (HintSequentialRead)
            {
                if (_sequentialReadBatchedIDs.Count == 0)
                    LoadSequentialReadBatchedIDs();
                if (_sequentialReadIndex >= 0) //< SequentialReadBatchedIDs.Count)
                    _sequentialReadIndex--; //++
                if (_sequentialReadIndex < 0) //== SequentialReadBatchedIDs.Count)
                    LoadSequentialReadBatchedIDs();
                return _sequentialReadBatchedIDs.Count > 0;
            }
            if (CurrentItemReference != null)
            {
                BeginTreeMaintenance();
                try
                {
                    BTreeNodeOnDisk o = CurrentItemReference.GetNode(this);
                    if (
                        !(CurrentItemReference.NodeAddress == -1 || o.Slots == null || o.Slots[CurrentItemReference.NodeItemIndex] == null))
                        return o.MovePrevious(this);
                }
                finally
                {
                    EndTreeMaintenance();
                }
            }
            return false;
        }

        /// <summary>
        /// Move current item pointer to next item relative to the current item
        /// </summary>
        /// <returns></returns>
        public override bool MoveNext()
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't MoveNext, ObjectStore is close.");
            if (HintSequentialRead)
            {
                if (_sequentialReadBatchedIDs.Count == 0)
                    LoadSequentialReadBatchedIDs();
                if (_sequentialReadIndex < _sequentialReadBatchedIDs.Count)
                    _sequentialReadIndex++;
                if (_sequentialReadIndex == _sequentialReadBatchedIDs.Count)
                    LoadSequentialReadBatchedIDs();
                return _sequentialReadBatchedIDs.Count > 0;
            }
            if (CurrentItemReference != null)
            {
                BeginTreeMaintenance();
                try
                {
                    BTreeNodeOnDisk o = CurrentItemReference.GetNode(this);
                    if (o != null &&
                        (!(CurrentItemReference.NodeAddress == -1 || o.Slots == null || o.Slots[CurrentItemReference.NodeItemIndex] == null)))
                        return o.MoveNext(this);
                }
                finally
                {
                    EndTreeMaintenance();
                }
            }
            return false;
        }

        /// <summary>
        /// Move current item pointer to the last item of the tree
        /// </summary>
        /// <returns></returns>
        public override bool MoveLast()
        {
            if (RootNode == null)
                throw new InvalidOperationException("Can't MoveLast, ObjectStore is close.");
            if (HintSequentialRead)
            {
                _sequentialReadBatchedIDs.Clear();
                _sequentialReadIndex = 0;
            }
            if (IsOpen && this.Count > 0)
            {
                BeginTreeMaintenance();
                bool r = RootNode.MoveLast(this);
                EndTreeMaintenance();

                if (HintSequentialRead)
                {
                    _sequentialReadBatchedIDs.Clear();
                    _sequentialReadIndex = 0;
                    LoadSequentialReadBatchedIDs(true);
                }
                return r;
            }
            return false;
        }
        #endregion
    }
}
