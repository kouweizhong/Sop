// Scalable Object Persistence (SOP) Framework, by Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: LGPL v2.1
// Have fun Coding! ;)

using System;
using System.Collections;
using Sop.Mru;
using Sop.OnDisk.Algorithm.BTree;
using Sop.OnDisk.Algorithm.Collection;
using Sop.OnDisk.Algorithm.SortedDictionary;
using Sop.Persistence;

namespace Sop.OnDisk.File
{
    /// <summary>
    /// FileSet is a Collection of File objects.
    /// </summary>
    internal class FileSet : IFileSet, IInternalFileEntity
    {
        /// <summary>
        /// Default Constructor
        /// </summary>
        public FileSet()
        {
        }

        /// <summary>
        /// Constructor expecting File parameter
        /// </summary>
        /// <param name="f"></param>
        public FileSet(IFile f)
        {
            bool b = f.Profile.IsDataInKeySegment;
            f.Profile.IsDataInKeySegment = true;
            Btree = f.Transaction != null ? ((Transaction.TransactionBase) f.Transaction.GetOuterChild()).CreateCollection(f) : 
                ObjectServer.CreateDictionaryOnDisk(f);
            f.Profile.IsDataInKeySegment = b;
        }

        /// <summary>
        /// Open the File Set
        /// </summary>
        public void Open()
        {
            if (Btree is SortedDictionaryOnDisk)
                ((SortedDictionaryOnDisk) Btree).BTreeAlgorithm.Open();
            else
                Btree.Open();
        }

        #region dispose
        /// <summary>
        /// Dispose from Memory the FileSet
        /// </summary>
        public void Dispose()
        {
            dispose();
        }
        private void dispose()
        {
            if (Btree == null) return;
            if (Btree.MruManager != null)
            {
                foreach (object o in Btree.MruManager.Values)
                {
                    if (!(o is IDisposable)) continue;
                    ((IDisposable)o).Dispose();
                }
            }
            Btree.Dispose();
            Btree = null;
        }
        #endregion

        public void MarkNotDirty()
        {
            foreach (MruItem o in Btree.MruManager.Values)
            {
                for (int i = 0; i < ((BTreeNodeOnDisk) o.Value).Count; i++)
                {
                    object f =
                        ((BTreeNodeOnDisk) o.Value).Slots[i].Value.Data;
                    if (f is File)
                        ((File) f).MarkNotDirty();
                }
            }
            for (int i = 0; i < Btree.RootNode.Count; i++)
            {
                if (Btree.RootNode.Slots[i].Value.Data is File)
                    ((File) Btree.RootNode.Slots[i].Value.Data).MarkNotDirty();
            }
            Btree.IsUnloading = true;
        }

        void IInternalFileEntity.CloseStream()
        {
            OpenOrCloseStream(false);
        }

        private void OpenOrCloseStream(bool open)
        {
            if (Btree.IsOpen && Btree.RootNode != null)
            {
                foreach (MruItem o in Btree.MruManager.Values)
                {
                    for (int i = 0; i < ((BTreeNodeOnDisk) o.Value).Count; i++)
                    {
                        object f =
                            ((BTreeNodeOnDisk) o.Value).Slots[i].Value.Data;
                        if (f is IInternalFileEntity)
                        {
                            if (open)
                                ((IInternalFileEntity) f).OpenStream();
                            else
                                ((IInternalFileEntity) f).CloseStream();
                        }
                    }
                }
                for (int i = 0; i < Btree.RootNode.Count; i++)
                {
                    if (Btree.RootNode.Slots[i].Value.Data is IInternalFileEntity)
                    {
                        if (open)
                            ((IInternalFileEntity) Btree.RootNode.Slots[i].Value.Data).OpenStream();
                        else
                            ((IInternalFileEntity) Btree.RootNode.Slots[i].Value.Data).CloseStream();
                    }
                }
                if (Btree is IInternalFileEntity)
                {
                    if (open)
                        ((IInternalFileEntity) Btree).OpenStream();
                    else
                        ((IInternalFileEntity) Btree).CloseStream();
                }
            }
        }

        void IInternalFileEntity.OpenStream()
        {
            OpenOrCloseStream(true);
        }

        /// <summary>
        /// Close the FileSet
        /// </summary>
        public void Close()
        {
            if (Btree.IsOpen && Btree.RootNode != null)
            {
                foreach (MruItem o in Btree.MruManager.Values)
                {
                    for (int i = 0; i < ((BTreeNodeOnDisk) o.Value).Count; i++)
                    {
                        object f =
                            ((BTreeNodeOnDisk) o.Value).Slots[i].Value.Data;
                        if (f is File)
                            ((File) f).Close();
                    }
                }
                for (int i = 0; i < Btree.RootNode.Count; i++)
                {
                    if (Btree.RootNode.Slots[i].Value.Data is File)
                        ((File) Btree.RootNode.Slots[i].Value.Data).Close();
                }
                Btree.Close();
            }
            //DiskBuffer.ClearData();
        }

        /// <summary>
        /// Returns count of Files in this FileSet
        /// </summary>
        public int Count
        {
            get { return Btree.Count; }
        }

        /// <summary>
        /// Save the changes on the File set
        /// </summary>
        public void Flush()
        {
            if (this.Btree.File.Server.ReadOnly)
                throw new InvalidOperationException("Object Server is in read only mode.");
            //btree.IsDirty = true;
            Btree.Flush();
        }

        /// <summary>
        /// Return the size on disk(in bytes) of this object
        /// </summary>
        public int HintSizeOnDisk { get; private set; }

        /// <summary>
        /// Serialize
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="writer"></param>
        public void Pack(IInternalPersistent parent, System.IO.BinaryWriter writer)
        {
            if (IsDirty)
                Flush();
            Btree.Pack(parent, writer);
        }

        /// <summary>
        /// DeSerialize
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="reader"></param>
        public void Unpack(IInternalPersistent parent, System.IO.BinaryReader reader)
        {
            if (Btree == null)
            {
                if (((CollectionOnDisk) parent).Transaction != null)
                    Btree = ((Transaction.TransactionBase) ((CollectionOnDisk) parent).Transaction).CreateCollection
                        (
                            ((CollectionOnDisk) parent).File);
                else
                    Btree = ObjectServer.CreateDictionaryOnDisk(((CollectionOnDisk) parent).File);
            }
            Btree.Unpack(parent, reader);
        }

        /// <summary>
        /// Returns list of Names of Files in this FileSet
        /// </summary>
        /// <returns>Array of Names</returns>
        public string[] GetNames()
        {
            var r = new string[this.Btree.Count];
            Btree.Keys.CopyTo(r, 0);
            return r;
        }

        /// <summary>
        /// Returns an enumerator for all the File objects in this FileSet
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            return Btree.Values.GetEnumerator();
        }

        public bool AutoDisposeItem
        {
            get
            {
                return Btree != null && Btree.AutoDisposeItem;
            }
            set
            {
                if (Btree != null)
                    Btree.AutoDisposeItem = value;
            }
        }

        /// <summary>
        /// Add a File object to the Set
        /// </summary>
        /// <param name="name">Name of File Object</param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public IFile Add(string name, string filename = null)
        {
            if (this.Btree.File.Server.ReadOnly)
                throw new InvalidOperationException("Object Server is in read only mode.");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");
            if (string.IsNullOrEmpty(filename))
                filename = string.Format("{0}{1}.{2}", this.Btree.File.Server.Path, name,
                                         ObjectServer.DefaultFileExtension);
            if (Btree.Contains(name))
                throw new ArgumentException(string.Format("File object with Name '{0}' is already in FileSet", name));
            IFile f = null;
            if (Btree.Transaction != null)
                f = ((Transaction.TransactionBase) Btree.Transaction).CreateFile(Btree.File.Server, name, filename);
            else
                f = new File(Btree.File.Server, name, filename);
            ((File)f).Profile = new Profile(Btree.File.Profile);
            return Add(f);
        }
        Sop.Client.IFile Sop.Client.IFileSet.Add(string name, string filename = null)
        {
            return Add(name, filename);
        }

        /// <summary>
        /// Add a File object to the Set
        /// </summary>
        /// <param name="f"></param>
        /// <param name="f"> </param>
        /// <returns></returns>
        public IFile Add(IFile f)
        {
            if (this.Btree.File.Server.ReadOnly)
                throw new InvalidOperationException("Object Server is in read only mode.");
            if (f == null)
                throw new ArgumentNullException("f");

            if (!Btree.Contains(f.Name))
            {
                if (f.Server == null)
                    f.Server = this.Btree.File.Server;
                if (!f.IsOpen)
                    f.Open();
                if (f.IsDirty)
                    f.Flush();
                Btree.Add(f.Name, f);
                Btree.Flush();
                return f;
            }
            throw new ArgumentException(string.Format("File object with Name '{0}' is already in FileSet", f.Name));
        }
        Sop.Client.IFile Sop.Client.IFileSet.Add(Sop.Client.IFile f)
        {
            return Add((IFile) f);
        }

        public bool Contains(Sop.Client.IFile f)
        {
            if (f == null)
                throw new ArgumentNullException("F");
            return Contains(f.Name);
        }
        public bool Contains(Sop.IFile f)
        {
            return Contains((Sop.Client.IFile) f);
        }

        public bool Contains(string name)
        {
            return Btree.Contains(name);
        }

        /// <summary>
        /// Remove the File with a given Name from the Set
        /// </summary>
        /// <param name="name"></param>
        public bool Remove(string name)
        {
            return Remove(name, false);
        }

        /// <summary>
        /// Remove the File with a given Name from the Set and optionally
        /// delete it from the file system
        /// </summary>
        /// <param name="name"></param>
        /// <param name="deleteFile"></param>
        private bool Remove(string name, bool deleteFile)
        {
            if (this.Btree.File.Server.ReadOnly)
                throw new InvalidOperationException("Object Server is in read only mode.");
            if (!deleteFile)
            {
                Btree.Remove(name);
                return true;
            }
            //** todo: optimize to not open File as isn't needed...
            IFile f = this[name];
            Btree.Remove(name);
            if (f != null)
            {
                string filename = f.Filename;
                Transaction.ITransactionLogger trans = null;
                if (f.Server != null)
                    trans = (Transaction.ITransactionLogger) f.Server.Transaction;
                f.Dispose();
                if (trans != null)
                    ((Transaction.TransactionRoot) trans.Root).RegisterRemoveFile(filename);
                try
                {
                    System.IO.File.Delete(filename);
                }
                catch
                {
                    if (trans != null)
                        ((Transaction.TransactionRoot) trans.Root).RegisterFailure(
                            Sop.Transaction.TransactionRootFailTypes.RemoveFileFailure, filename);
                    throw;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Searches files in set for the one having its File Name equal to Name parameter.
        /// </summary>
        /// <param name="name"> </param>
        /// <returns></returns>
        public IFile this[string name]
        {
            get
            {
                File f = (File) Btree[name];
                if (f != null && f._store == null &&
                    ((SortedDictionaryOnDisk) Btree).CurrentItemOnDisk.Value.DiskBuffer.DataAddress >= 0)
                {
                    ((SortedDictionaryOnDisk) Btree).CurrentItemOnDisk.Value.Data = null;
                    ((SortedDictionaryOnDisk) Btree).CurrentItemOnDisk.ValueLoaded = false;
                    f = (File) Btree[name];
                }
                return f;
            }
        }
        Sop.Client.IFile Sop.Client.IFileSet.this[string name]
        {
            get { return this[name]; }
        }

        public const string FileSetLiteral = "__System_FileSet";

        public Transaction.ITransactionLogger Transaction
        {
            get { return Btree.Transaction; }
        }

        internal Algorithm.SortedDictionary.ISortedDictionaryOnDisk Btree = null;

        /// <summary>
        /// get/set the DiskBuffer of this FileSet
        /// </summary>
        public Sop.DataBlock DiskBuffer
        {
            get { return Btree.DiskBuffer; }
            set { Btree.DiskBuffer = value; }
        }

        /// <summary>
        /// get/set whether this FileSet was modified or not
        /// </summary>
        public bool IsDirty
        {
            get { return Btree.IsDirty; }
            set { Btree.IsDirty = value; }
        }


        Sop.IFile Sop.IFileSet.Add(Sop.IFile f)
        {
            return (IFile) this.Add((IFile) f);
        }

        Sop.IFile Sop.IFileSet.Add(string name, string filename = null)
        {
            return (IFile) this.Add(name, filename);
        }

        public bool Contains(IFile f)
        {
            return this.Contains((Sop.IFile)f);
        }

        Sop.IFile Sop.IFileSet.this[string name]
        {
            get { return (Sop.IFile)this[name]; }
        }
    }
}