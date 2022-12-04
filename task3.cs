using System.Threading;

public interface IStack<T>
{
    void Push(T item);
    bool TryPop(out T item);
    int Count { get; }
}
 
public class ConcurrentStack<T> : IStack<T>
{
    private StackNode<T> head = null;
    private int count = 0;
        
    public void Push(T item)
    {
        var newHeadNode = new StackNode<T>(item, head);
        var spinWait = new SpinWait();
 
        while (true)
        {
            var headNode = head;
            newHeadNode.NextNode = headNode;
            if (Interlocked.CompareExchange(ref head, newHeadNode, headNode) == headNode)
            {
                Interlocked.Increment(ref count);
                break; 
            }
                
            spinWait.SpinOnce();
        }
    }
 
    public bool TryPop(out T item)
    {
        var spinWait = new SpinWait();
        while (true)
        {
            var headNode = head;
            if (headNode is null)
            {
                item = default;
                return false;
            }
 
            if (Interlocked.CompareExchange(ref head, headNode.NextNode, headNode) == headNode)
            {
                item = headNode.Value;
                Interlocked.Decrement(ref count);
                return true;
            }
            spinWait.SpinOnce();
        }
    }
 
    public int Count => count;
}
 
public class StackNode<T>
{
    public readonly T Value;
    public StackNode<T> NextNode;
    public StackNode(T value, StackNode<T> nextNode)
    {
        Value = value;
        NextNode = nextNode;
    }
}