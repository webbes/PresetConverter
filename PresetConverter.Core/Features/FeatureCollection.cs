namespace PresetConverter;

public sealed class FeatureCollection : IFeatureCollection
{
    private readonly Dictionary<Type, object> _features = [];

    public TFeature? Get<TFeature>() =>
        _features.TryGetValue(typeof(TFeature), out var feature) ? (TFeature)feature : default;

    public void Set<TFeature>(TFeature? feature)
    {
        if (feature is null)
        {
            _features.Remove(typeof(TFeature));
            return;
        }

        _features[typeof(TFeature)] = feature;
    }
}