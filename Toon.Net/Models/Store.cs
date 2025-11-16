using System;
using System.Collections.Generic;
using System.Text;

namespace ToonTest.Net.Models
{
    public sealed class Store
    {
        public List<Product> products { get; set; } = new();
    }
}
