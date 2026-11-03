using Microsoft.UI.Dispatching;

namespace VibeCode.MdEditor.Services;

public interface IUiDispatcher
{
    void Initialize(DispatcherQueue queue);

    bool TryEnqueue(Action action);
}

public sealed class UiDispatcher : IUiDispatcher
{
    private DispatcherQueue? _queue;

    public void Initialize(DispatcherQueue queue)
    {
        _queue = queue;
    }

    public bool TryEnqueue(Action action)
    {
        var queue = _queue;
        return queue is not null && queue.TryEnqueue(() => action());
    }
}
