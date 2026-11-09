using Microsoft.UI.Xaml;

namespace VibeCode.MdEditor.Models;

public sealed class DiffRowViewModel
{
    public required string LeftNumber { get; init; }

    public required string LeftText { get; init; }

    public required Visibility LeftRemovedVisibility { get; init; }

    public required string RightNumber { get; init; }

    public required string RightText { get; init; }

    public required Visibility RightAddedVisibility { get; init; }
}
