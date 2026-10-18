using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using OddsFactory.Sports.Data.Contracts.SportRadar;

namespace GameGraph.Nba.Data;

public static class ValidatedDataJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddEventTypes } },
    };

    private static void AddEventTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(Event))
            return;

        var polymorphism = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = "Type" };

        foreach (var type in typeof(Event).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Event)) && !t.IsAbstract))
            polymorphism.DerivedTypes.Add(new JsonDerivedType(type, type.Name));

        info.PolymorphismOptions = polymorphism;
    }
}
