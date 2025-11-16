using System.Text.Json;
using ToonNet.Converter;
using ToonTest.Net.Models;

var data = new StoreWrapper
{
    store = new Store
    {
        products = new List<Product>
                {
                    new() { id = 1, name = "T-Shirt",  price = 19.99, inStock = true },
                    new() { id = 2, name = "Cap",     price = 14.5,  inStock = false },
                    new() { id = 3, name = "Socks",price = 5,     inStock = true }
                }
    }
};

string toon = ToonConverter.SerializeObject(data);

string json = JsonSerializer.Serialize(data);

Console.WriteLine("TOON:");
Console.WriteLine(toon);

Console.WriteLine("******************");

Console.WriteLine("json:");
Console.WriteLine(json);

Console.WriteLine("******************");

// Deserialize it back.
StoreWrapper decoded = ToonConverter.DeserializeObject<StoreWrapper>(toon);
Console.WriteLine();
Console.WriteLine($"Decoded products: {decoded?.store.products.Count}");
Console.ReadLine();