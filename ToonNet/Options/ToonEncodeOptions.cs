using System;
using System.Collections.Generic;
using System.Text;

namespace ToonNet.Options
{
    /// <summary>TOON serialization options.</summary>
    public sealed class ToonEncodeOptions
    {
        /// <summary>Indentation string (default is 2 spaces).</summary>
        public string Indent { get; set; } = "  ";

        /// <summary>
        ///  Delimiter for tabular and primitive arrays.  
        /// The spec uses a comma by default.
        /// </summary>
        public char Delimiter { get; set; } = ',';
    }
}
