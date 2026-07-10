using UnityEngine;

/// <summary>
/// One block of tooltip copy (main tooltip or one secondary status/action explanation entry).
/// </summary>
public struct TooltipContent
{
    public string Title;
    public string Description;
    public Sprite Background;

    public TooltipContent(string title, string description, Sprite background = null)
    {
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        Background = background;
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Description);
}
