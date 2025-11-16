using System;
using System.Collections.Generic;
using System.Text;

namespace ToonNet.Options
{
    /// <summary>TOON deserialization options.</summary>
    public sealed class ToonDecodeOptions
    {
        /// <summary>Expected delimiter for arrays (comma by default).</summary>
        public char Delimiter { get; set; } = ',';
    }
}
