﻿// Scalable Object Persistence (SOP) Framework, by Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: LGPL v2.1
// Have fun Coding! ;)

using System;
using System.Collections;
using System.Collections.Generic;
using Sop.OnDisk.Algorithm.Collection;
using Sop.OnDisk.File;
using Sop.OnDisk.IO;
using File = Sop.OnDisk.File.File;

namespace Sop.Transaction
{
    using OnDisk;

    /// <summary>
    /// Transaction Base
    /// </summary>
    internal abstract class TransactionBase : ITransactionLogger
    {
        /// <summary>
        /// Dispose will rollback transaction if isn't in Rollback or Commit state
        /// </summary>
        public virtual void Dispose()
        {
            if (!(CurrentCommitPhase == CommitPhase.Committed ||
                  CurrentCommitPhase == CommitPhase.Rolledback))
                this.Rollback();

            this.Children = null;
            this.Parent = null;
            this._root = null;
        }
        public virtual bool IsDisposing { get; set; }

        public virtual OnDisk.Algorithm.SortedDictionary.ISortedDictionaryOnDisk CreateCollection(OnDisk.File.IFile f)
        {
            return OnDisk.ObjectServer.CreateDictionaryOnDisk(f);
        }

        public virtual Sop.ISortedDictionaryOnDisk CreateCollection(Sop.IFile file, IComparer comparer, string name,
                                                                    bool isDataInKeySegment)
        {
            return OnDisk.ObjectServer.CreateDictionaryOnDisk((OnDisk.File.IFile) file,
                                                                              comparer, name, isDataInKeySegment);
        }

        public virtual OnDisk.File.IFileSet CreateFileSet()
        {
            return new FileSet();
        }

        public virtual OnDisk.File.IFileSet CreateFileSet(OnDisk.File.IFile f)
        {
            OnDisk.File.IFileSet fileset = new FileSet(f);
            fileset.Open();
            f.Store[FileSet.FileSetLiteral] = fileset;
            return fileset;
        }

        public virtual OnDisk.File.IFile CreateFile()
        {
            return new File();
        }

        public virtual OnDisk.File.IFile CreateFile(string name, string filename)
        {
            return new File(((TransactionRoot) this.Root).Server, name, filename);
        }

        public virtual OnDisk.File.IFile CreateFile(ObjectServer server, string name, string filename)
        {
            return new File(server, name, filename);
        }

        public ITransactionLogger GetOuterChild()
        {
            if (Children != null && Children.Count > 0)
                return ((TransactionBase) Children[0]).GetOuterChild();
            return this;
        }

        /// <summary>
        /// Begin a nested Transaction
        /// </summary>
        /// <returns></returns>
        public ITransaction Begin()
        {
            return Begin(false);
        }

        /// <summary>
        /// Begin a nested Transaction
        /// </summary>
        /// <returns></returns>
        virtual public ITransactionLogger Begin(bool ownsRoot)
        {
            ITransactionLogger r;
            if (((TransactionRoot)Root).Server.Profile.MemoryExtenderMode)
                r = new NoTransactionLogger();
            else
            {
                r = new Transaction();
                ((Transaction)r).Initialize(((TransactionRoot)Root).Server);
                ((Transaction)r).OwnsRoot = ownsRoot;
            }
            r.Parent = this;
            if (Children == null)
                this.Children = new List<ITransactionLogger>();
            Children.Add((TransactionBase) r);
            return r;
        }

        /// <summary>
        /// Override to Save transaction ObjectStore(s).
        /// </summary>
        protected virtual void Flush()
        {
            if (Root != null && ((TransactionRoot) Root).Server != null)
                ((TransactionRoot) Root).Server.Flush();
        }

        /// <summary>
        /// Commit starting Children transaction(s). Rollback if a transaction fails.
        /// </summary>
        public virtual bool Commit()
        {
            if (Commit(CommitPhase.FirstPhase))
            {
                Commit(CommitPhase.SecondPhase);
                return true;
            }
            if (CurrentCommitPhase != CommitPhase.Rolledback)
                this.Rollback();
            return false;
        }

        /// <summary>
        /// Commit starting Children transaction(s)
        /// </summary>
        /// <param name="phase"></param>
        virtual public bool Commit(CommitPhase phase)
        {
            Flush();
            if (Children != null && Children.Count > 0)
            {
                var childrenCopy = new ITransactionLogger[Children.Count];
                Children.CopyTo(childrenCopy, 0);
                foreach (ITransactionLogger trans in childrenCopy)
                {
                    if (!trans.InternalCommit(phase))
                        return false;
                }
            }
            //** all Transaction classes implement ITransactionLogger so this cast should be OK..
            return this.InternalCommit(phase);
        }

        /// <summary>
        /// Rollback starting with Children-most transaction(s) then parent
        /// trans to this trans and its Parent until Root is finally rolled back.
        /// </summary>
        public virtual void Rollback()
        {
            if (Children != null)
            {
                var childrenCopy = new ITransactionLogger[Children.Count];
                Children.CopyTo(childrenCopy, 0);
                foreach (ITransactionLogger trans in childrenCopy)
                    trans.InternalRollback(IsDisposing);
            }
            //** all Transaction classes implement ITransactionLogger so this cast should be OK..
            this.InternalRollback(IsDisposing);
        }

        /// <summary>
        /// Transaction ID
        /// </summary>
        public int Id
        {
            get { return _id; }
            internal set { _id = value; }
        }

        private int _id = 0;

        /// <summary>
        /// Commit Phase
        /// </summary>
        public CommitPhase CurrentCommitPhase { get; set; }

        /// <summary>
        /// Returns Parent Transaction Object
        /// </summary>
        public virtual ITransactionLogger Parent { get; set; }

        /// <summary>
        /// Nested or Children Transactions
        /// </summary>
        public List<ITransactionLogger> Children { get; set; }

        /// <summary>
        /// Get the 1st element Leafmost Child Transaction
        /// </summary>
        /// <returns></returns>
        public TransactionBase GetLeafChild()
        {
            if (Children != null && Children.Count > 0)
                return ((TransactionBase) Children[0]).GetLeafChild();
            return this;
        }

        /// <summary>
        /// Returns the Root transaction
        /// </summary>
        public ITransactionLogger Root
        {
            get
            {
                if (_root == null)
                {
                    if (Parent != null)
                        _root = (TransactionRoot) Parent.Root;
                    else
                        _root = (TransactionRoot) this;
                }
                return _root;
            }
        }

        private TransactionRoot _root;

        public abstract bool InternalCommit(CommitPhase phase);
        public abstract void InternalRollback(bool isDisposing);

        public void Register(ActionType action,
                             CollectionOnDisk collection,
                             long blockAddress,
                             long blockSize)
        {
            switch (action)
            {
                case ActionType.Add:
                    if (blockSize > (long) DataBlockSize.Maximum)
                        throw new ArgumentOutOfRangeException("blockSize");
                    RegisterAdd(collection, blockAddress, (int) blockSize);
                    break;
                case ActionType.Remove:
                    RegisterRemove(collection);
                    break;
                case ActionType.Grow:
                    RegisterFileGrowth(collection, blockAddress, blockSize);
                    break;
                case ActionType.Recycle:
                    if (blockSize > int.MaxValue)
                        throw new ArgumentOutOfRangeException("blockSize");
                    RegisterRecycle(collection, blockAddress, (int) blockSize);
                    break;
                case ActionType.RecycleCollection:
                    if (blockSize > int.MaxValue)
                        throw new ArgumentOutOfRangeException("blockSize");
                    RegisterRecycleCollection(collection, blockAddress, (int) blockSize);
                    break;
                default:
                    throw new SopException(string.Format("Unsupported Transaction Action {0}", action));
            }
        }

        protected internal abstract void RegisterAdd(CollectionOnDisk collection,
                                                     long blockAddress, int blockSize);

        protected internal abstract void RegisterRemove(CollectionOnDisk collection);

        /// <summary>
        /// Try to register block for update and back up contents as needed.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="blockAddress"></param>
        /// <param name="segmentSize"></param>
        /// <param name="readPool"></param>
        /// <param name="writePool"></param>
        /// <returns>true means block was registered and contents backed up,
        /// false means block is either new or recycled and wasn't registered for update and no backup occurred.</returns>
        protected internal abstract bool RegisterSave(CollectionOnDisk collection,
                                                      long blockAddress, int segmentSize,
                                                      ConcurrentIOPoolManager readPool,
                                                      ConcurrentIOPoolManager writePool);

        protected internal abstract void RegisterFileGrowth(CollectionOnDisk collection,
                                                            long segmentAddress, long segmentSize);

        protected internal abstract void RegisterRecycle(CollectionOnDisk collection,
                                                         long blockAddress, int blockSize);

        protected internal abstract void RegisterRecycleCollection(CollectionOnDisk collection,
                                                                   long blockAddress, int blockSize);
    }
}