// Scalable Object Persistence (SOP) Framework, main contact - Gerardo Recinto (email: gerardorecinto@Yahoo.com for questions/comments)
// Open Source License: MIT
// Have fun Coding! ;)

namespace Sop.OnDisk.File
{
    /// <summary>
    /// Entity interface
    /// </summary>
    internal interface IEntity
    {
        /// <summary>
        /// true means this entity is new, otherwise false
        /// </summary>
        bool IsNew { get; set; }

        /// <summary>
        /// Name of the entity
        /// </summary>
        string Name { get; }
    }
}