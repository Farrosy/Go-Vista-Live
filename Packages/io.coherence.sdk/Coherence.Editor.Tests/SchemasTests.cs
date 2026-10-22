// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using Editor.Portal;

    /// <summary>
    /// Tests related to <see cref="Schemas"/>.
    /// </summary>
    public sealed class SchemasTests : CoherenceTest
    {
        [Test]
        public void GetNameOrId_Returns_Name_When_Present()
        {
            var project = new ProjectInfo { id = "cphgvm4pb608atnc0bkg", name = "My Project" };

            Assert.That(Schemas.GetNameOrId(project), Is.EqualTo("My Project"));
        }

        [Test]
        public void GetNameOrId_Falls_Back_To_Id_When_Name_Is_Empty()
        {
            var project = new ProjectInfo { id = "cphgvm4pb608atnc0bkg", name = "" };

            Assert.That(Schemas.GetNameOrId(project), Is.EqualTo("cphgvm4pb608atnc0bkg"));
        }

        [Test]
        public void GetNameOrId_Falls_Back_To_Id_When_Name_Is_Null()
        {
            var project = new ProjectInfo { id = "cphgvm4pb608atnc0bkg", name = null };

            Assert.That(Schemas.GetNameOrId(project), Is.EqualTo("cphgvm4pb608atnc0bkg"));
        }

        [Test]
        public void GetNameOrId_Returns_Empty_String_When_Project_Is_Null()
        {
            Assert.That(Schemas.GetNameOrId(null), Is.EqualTo(""));
        }
    }
}
