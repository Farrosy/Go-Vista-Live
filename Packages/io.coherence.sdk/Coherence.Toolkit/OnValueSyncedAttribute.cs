// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;

    /// <summary>
    /// Designates a method to be called when the value of a synced field or property changes on a remote instance.
    /// </summary>
    /// <remarks>
    /// This attribute can be applied to a field, a property, or a method.
    /// <list type="bullet">
    /// <item>
    /// Applied to a field or property — <see cref="Member"/> identifies the name of the method
    /// to invoke when the value of the decorated member changes.
    /// </item>
    /// <item>
    /// Applied to a method — <see cref="Member"/> identifies the name of the synced field or
    /// property whose changes trigger the decorated method.
    /// </item>
    /// <item>
    /// If applied to both a method and a field or property, relating to one another, the field or property one takes precedence.
    /// </item>
    /// </list>
    /// The method is invoked only on entities without state authority (see <see cref="CoherenceSync.HasStateAuthority"/>),
    /// and must satisfy the following requirements:
    /// <list type="bullet">
    /// <item>It must be defined in the same component as the synced field or property.</item>
    /// <item>It must declare exactly two parameters of same type as the synced field or property. These parameters represent the previous and the current value.</item>
    /// </list>
    /// The method will be fetched through reflection at runtime, so unless it's explicitly called elsewhere in code,
    /// you should mark it with <see cref="UnityEngine.Scripting.PreserveAttribute"/> to avoid code stripping to remove it.
    /// </remarks>
    /// <example>
    /// <code source="Toolkit/OnValueSyncedAttributeSamples.cs" language="csharp" region="MethodAttribute" />
    /// </example>
    /// <example>
    /// <code source="Toolkit/OnValueSyncedAttributeSamples.cs" language="csharp" region="FieldAttribute" />
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class OnValueSyncedAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the member linked to this attribute.
        /// </summary>
        /// <remarks>
        /// This value is interpreted differently depending on where the attribute is applied to:
        /// <list type="bullet">
        /// <item>
        /// Applied to a field or property — <see cref="Member"/> identifies the name of the method
        /// to invoke when the value of the decorated member changes.
        /// </item>
        /// <item>
        /// Applied to a method — <see cref="Member"/> identifies the name of the synced field or
        /// property whose changes trigger the decorated method.
        /// </item>
        /// </list>
        /// </remarks>
        public readonly string Member;

        /// <summary>
        /// Suppress error when the attribute targets a field or property not marked as synced.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to suppress the error; otherwise, <see langword="false"/>.
        /// The default is <see langword="false"/>.
        /// </value>
        public bool SuppressNotBoundError { get; set; }

        /// <summary>
        /// Suppress error when the attribute targets a method where parameter naming suggests incorrect order.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to suppress the error; otherwise, <see langword="false"/>.
        /// The default is <see langword="false"/>.
        /// </value>
        public bool SuppressParamOrderError { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="OnValueSyncedAttribute"/> with the specified member name.
        /// </summary>
        /// <param name="member">
        /// The name of the member to link to. See <see cref="Member"/> for details.
        /// </param>
        public OnValueSyncedAttribute(string member)
        {
            Member = member;
        }

        /// <summary>Gets the name of the member linked to this attribute.</summary>
        /// <remarks>This property is obsolete. Use <see cref="Member"/> instead.</remarks>
        [Obsolete("Use Member instead.", false)]
        [Deprecated("03/2026", 2, 2, 0, Reason = "Renamed to Member to reflect dual usage as a field/property or method name.")]
        public string Callback => Member;
    }
}
