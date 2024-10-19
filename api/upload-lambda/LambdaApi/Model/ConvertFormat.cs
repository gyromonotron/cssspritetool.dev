namespace LambdaApi.Model;

public class ConvertFormat
{
    public static ConvertFormat Unsupported => new("unsupported");
    public static ConvertFormat WebP => new("webp");
    public static ConvertFormat Png => new("png");

    public string Name { get; }

    public ConvertFormat(string name) => Name = name;

    public static ConvertFormat FromName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return Unsupported;

        return name.ToLower() switch
        {
            "webp" => WebP,
            "png" => Png,
            _ => Unsupported,
        };
    }

    public override string ToString()
    {
        return Name;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ConvertFormat otherValue)
        {
            return false;
        }

        return Name.Equals(otherValue.Name);
    }

    public override int GetHashCode() => Name.GetHashCode();
}