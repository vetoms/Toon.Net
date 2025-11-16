using System;
using System.Collections.Generic;
using System.Text;

namespace ToonTest.Net.Models
{
    public sealed class Product
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public double price { get; set; }
        public bool inStock { get; set; }
    }
}
