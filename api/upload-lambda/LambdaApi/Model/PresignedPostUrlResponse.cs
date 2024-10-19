namespace LambdaApi.Model;

public class PresignedPostUrlResponse
{
    public string Url { get; private set; }

    public PresignedPostUrlResponse(string url)
    {
        Url = url;
    }
}