namespace GameShell.Core.Services;

public static class PathExpander
{
    public static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Environment.ExpandEnvironmentVariables(path);
    }
}
