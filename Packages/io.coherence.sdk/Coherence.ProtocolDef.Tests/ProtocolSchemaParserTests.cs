// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef.Tests
{
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using Coherence.Tests;
    using NUnit.Framework;

    public class ProtocolSchemaParserTests : CoherenceTest
    {
        // A culture whose decimal separator is a comma. WebGL builds adopt the
        // browser's locale as CurrentCulture, so players in these regions used to
        // crash parsing the invariant-formatted precision override ("0.001").
        private static readonly CultureInfo CommaDecimalCulture = new("de-DE");

        private const string SchemaWithFixedPointFloat =
            "component _3889a458e94666d4784df30d8dd06d7d_6540142254599110064 [id \"0\"]\n" +
            "  fireTimer Float [compression \"FixedPoint\", range-min \"0\", range-max \"100\", precision \"0.001\", bits \"32\"]\n";

        private CultureInfo originalCulture;

        public override void SetUp()
        {
            base.SetUp();
            originalCulture = Thread.CurrentThread.CurrentCulture;
        }

        public override void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            base.TearDown();
        }

        [Test]
        [Description("Numeric field overrides are parsed with the invariant culture, so an invariant-formatted " +
            "precision (\"0.001\") loads correctly even when the current culture uses a comma decimal separator " +
            "(reproduces the WebGL crash where the browser locale leaks into CurrentCulture).")]
        public void Parse_PrecisionOverride_UnderCommaDecimalCulture_DoesNotThrow()
        {
            Thread.CurrentThread.CurrentCulture = CommaDecimalCulture;

            var definition = ProtocolDefinition.LoadFromText(SchemaWithFixedPointFloat, includeRSSchema: false);

            var field = definition.Components.Values
                .Single(c => c.Name.Contains("6540142254599110064"))
                .Fields.Single(f => f.Name == "fireTimer");

            Assert.That(field.Meta.Precision, Is.EqualTo(0.001d));
        }

        [Test]
        [Description("Sanity check that the same schema parses identically under the invariant culture.")]
        public void Parse_PrecisionOverride_UnderInvariantCulture_ParsesValue()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var definition = ProtocolDefinition.LoadFromText(SchemaWithFixedPointFloat, includeRSSchema: false);

            var field = definition.Components.Values
                .Single(c => c.Name.Contains("6540142254599110064"))
                .Fields.Single(f => f.Name == "fireTimer");

            Assert.That(field.Meta.Precision, Is.EqualTo(0.001d));
        }
    }
}
