// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;

    /// <summary>
    /// Marks a method to be networked, creating a command.
    /// </summary>
    /// <remarks>
    /// Must be applied to a public method with no return value.
    /// <para>
    /// Supported parameter types:
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
    /// GameObject and Component arguments will only get synced if they are part of a CoherenceSync prefab.
    /// Internally, what's synced is the Entity ID associated with the CoherenceSync prefab.
    /// </para>
    /// <para>
    /// If a prefab has multiple components of the same type,
    /// commands that send those components as arguments might not deliver the specific instance.
    /// </para>
    /// <para>
    /// When using this attribute successfully, a method binding will be added to the <see cref="CoherenceSync.Bindings"/> list.
    /// Methods with this attribute will appear on the Configure window as synced,
    /// and can only be unsynced by removing the attribute and them unchecking them in the Configure window.
    /// Methods without this attribute can be marked as synced via the Configure window directly.
    /// </para>
    /// </remarks>
    /// <seealso cref="SyncAttribute"/>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandAttribute : Attribute
    {
        /// <summary>
        /// Sets the default routing on the binding (it can still
        /// be changed in the bindings configuration window.)
        /// </summary>
        public MessageTarget defaultRouting = MessageTarget.All;

        /// <summary>
        /// What this method name was before.
        /// </summary>
        /// <remarks>
        /// Used for migration purposes. Bindings whose name matches <see cref="OldName"/> and parameter types match <see cref="OldParams"/> will be updated, instead of considered new.
        /// </remarks>
        /// <see cref="OldParams"/>
        public string OldName { get; }

        /// <summary>
        /// What this method's parameter types were before.
        /// </summary>
        /// <remarks>
        /// Used for migration purposes. Bindings whose name matches <see cref="OldName"/> and parameter types match <see cref="OldParams"/> will be updated, instead of considered new.
        /// </remarks>
        /// <see cref="OldName"/>
        public Type[] OldParams { get; }

        /// <summary>
        /// Send command metadata with the command.
        /// </summary>
        /// <remarks>
        /// Includes the sender's ClientID and the simulation frame at which the command was sent.
        /// </remarks>
        public bool UseMeta { get; set; }

        public CommandAttribute()
        {
        }

        /// <param name="oldName">What this method name was before.</param>
        /// <param name="oldParams">What this method's parameter types were before.</param>
        /// <see cref="OldName"/>
        /// <see cref="OldParams"/>
        public CommandAttribute(string oldName = null, params Type[] oldParams)
        {
            OldName = oldName;
            OldParams = oldParams;
        }
    }
}
