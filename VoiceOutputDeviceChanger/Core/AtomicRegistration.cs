using System;
using System.Threading;

namespace VoiceOutputDeviceChanger.Core;

internal sealed class AtomicRegistration<T>
    where T : class
{
    private T? _current;

    public T? Read()
    {
        return Volatile.Read(ref _current);
    }

    public T? Exchange(T? value)
    {
        return Interlocked.Exchange(ref _current, value);
    }

    public bool IsCurrent(T value)
    {
        return ReferenceEquals(Volatile.Read(ref _current), value);
    }
}

internal sealed class AtomicCommitLease
{
    private const int Active = 0;
    private const int Committing = 1;
    private const int Retired = 2;
    private int _state;

    public bool TryBegin()
    {
        return Interlocked.CompareExchange(ref _state, Committing, Active) == Active;
    }

    public void End()
    {
        if (Interlocked.CompareExchange(ref _state, Active, Committing) != Committing)
        {
            throw new InvalidOperationException("The commit lease is not active.");
        }
    }

    public void Retire()
    {
        var spinner = new SpinWait();
        while (Interlocked.CompareExchange(ref _state, Retired, Active) != Active)
        {
            if (Volatile.Read(ref _state) == Retired)
            {
                return;
            }

            spinner.SpinOnce();
        }
    }
}

internal sealed class AtomicUsageLease
{
    private const int RetiredMask = int.MinValue;
    private const int UsageMask = int.MaxValue;
    private int _state;

    public bool TryBegin()
    {
        while (true)
        {
            int state = Volatile.Read(ref _state);
            if ((state & RetiredMask) != 0)
            {
                return false;
            }

            if ((state & UsageMask) == UsageMask)
            {
                throw new InvalidOperationException("The usage lease is exhausted.");
            }

            if (Interlocked.CompareExchange(ref _state, state + 1, state) == state)
            {
                return true;
            }
        }
    }

    public void End()
    {
        int state = Interlocked.Decrement(ref _state);
        if ((state & UsageMask) == UsageMask)
        {
            throw new InvalidOperationException("The usage lease was released without a matching acquisition.");
        }
    }

    public void Retire()
    {
        while (true)
        {
            int state = Volatile.Read(ref _state);
            if ((state & RetiredMask) != 0)
            {
                break;
            }

            if (Interlocked.CompareExchange(ref _state, state | RetiredMask, state) == state)
            {
                break;
            }
        }

        var spinner = new SpinWait();
        while ((Volatile.Read(ref _state) & UsageMask) != 0)
        {
            spinner.SpinOnce();
        }
    }
}
