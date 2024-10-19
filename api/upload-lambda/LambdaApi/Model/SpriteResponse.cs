namespace LambdaApi.Model;

public class SpriteResponse
{
    public string SpriteUrl { get; set; }
    public string Css { get; set; }
    public string Html { get; set; }
    public string ZipUrl { get; set; }

    public SpriteResponse()
    {
        SpriteUrl = Css = Html = ZipUrl = string.Empty;
    }
}
