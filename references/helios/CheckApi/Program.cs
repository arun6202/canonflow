using System;
using System.Reflection;
using Elastic.Clients.Elasticsearch.Core.Search;

class Program {
    static void Main() {
        var t = typeof(HitsMetadata<object>);
        foreach(var p in t.GetProperties()) {
            Console.WriteLine(p.Name + " " + p.PropertyType);
        }
    }
}
