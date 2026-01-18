namespace ApiPosture.Core.Models;

/// <summary>
/// HTTP methods supported by endpoints.
/// </summary>
[Flags]
public enum HttpMethod
{
    None = 0,
    Get = 1,
    Post = 2,
    Put = 4,
    Delete = 8,
    Patch = 16,
    Head = 32,
    Options = 64,
    All = Get | Post | Put | Delete | Patch | Head | Options
}
