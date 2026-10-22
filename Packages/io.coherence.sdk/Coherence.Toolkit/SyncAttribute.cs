// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using Bindings;

    /// <summary>
    /// Marks a variable to be networked.
    /// </summary>
    /// <remarks>
    /// Must be applied to a public field or a property.
    /// <para>
    /// Supported types:
    /// <list type="bullet">
    /// <item>
    /// Primitive types: int, float, double, bool, char, byte, sbyte, short, ushort, uint, long, ulong
    /// </item>
    /// <item>
    /// Composite types: string, byte[], Color, Vector2, Vector3, Quaternion
    /// </item>
    /// <item>
    /// Unity Object types: GameObjects and Components (read note below).
    /// </item>
    /// <item>Network identification types: <see cref="Coherence.Connection.ClientID"/> and <see cref="Coherence.Entities.Entity"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// When syncing Unity Object types, only the reference to the object is synced, not the actual data of the object.
    /// GameObjects and Components will only get synced if they are part of a CoherenceSync prefab.
    /// Internally, what's synced is the Entity ID associated with the CoherenceSync prefab.
    /// </para>
    /// <para>
    /// When using this attribute successfully, the variable will be added to the <see cref="CoherenceSync.Bindings"/> list.
    /// Variables with this attribute will appear on the Configure window as synced,
    /// and can only be unsynced by removing the attribute and them unchecking them in the Configure window.
    /// Variables without this attribute can be marked as synced via the Configure window directly.
    /// </para>
    /// </remarks>
    /// <seealso cref="OnValueSyncedAttribute"/>
    /// <seealso cref="CommandAttribute"/>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SyncAttribute : Attribute
    {
        /// <summary>
        /// How this variable was previously known as.
        /// </summary>
        /// <remarks>
        /// Used for migration purposes. Bindings whose name matches OldName will be updated, instead of considered new.
        /// </remarks>
        /// <seealso cref="UnityEngine.Serialization.FormerlySerializedAsAttribute"/>
        public string OldName { get; }

        /// <summary>
        /// Sets the sync mode on the binding.
        /// </summary>
        public SyncMode DefaultSyncMode = SyncMode.Always;

        public SyncAttribute()
        {
        }

        /// <param name="oldName">How this variable was previously known as. See <see cref="OldName"/></param>
        public SyncAttribute(string oldName = null) => OldName = oldName;
    }
}
