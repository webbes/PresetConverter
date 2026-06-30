namespace PresetConverter;

public interface IFeatureCollection
{
    TFeature? Get<TFeature>();

    void Set<TFeature>(TFeature? feature);
}