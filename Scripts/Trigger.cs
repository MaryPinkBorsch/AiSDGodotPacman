using Godot;
using System;

public class Trigger
{
    private static readonly int disabledTicks = int.MaxValue;

    private int gameTicks;
    private int triggerTicks;
    private int durationTicks;

    private bool disabled;
    private bool hasCallback;

    private Callable callback;

    public Trigger()
    {
        durationTicks = 1;
        disabled = true;
        hasCallback = false;
    }

    public Trigger(int duration)
    {
        durationTicks = duration;
        disabled = true;
        hasCallback = false;
    }

    public Trigger(Callable callback) : this(1, callback)
    {
    }

    public Trigger(int duration, Callable callback)
    {
        durationTicks = duration;
        this.callback = callback;
        hasCallback = true;
    }

    public void Reset()
    {
        disabled = true;
        gameTicks = 0;
    }

    public void Start(int delayTicks = 0)
    {
        triggerTicks = gameTicks + delayTicks + 1;
        disabled = false;
    }

    public void Disable()
    {
        disabled = true;
    }

    public int TicksSinceStarted()
    {
        if (!disabled)
        {
            if (gameTicks >= triggerTicks)
            {
                return gameTicks - triggerTicks;
            }
        }

        return disabledTicks;
    }

    public bool IsActive()
    {
        if (disabled)
        {
            return false;
        }
        else
        {
            return TicksSinceStarted() < durationTicks;
        }
    }

    // необходимо вызывать каждый кадр для синхронизации с игровыми тиками

    public void Tick(int ticks)
    {
        // устанавливаем новые тики

        gameTicks = ticks;

       // если триггер не отключен, то проверяем

        if (!disabled && hasCallback)
        {
            // проверяем, началось ли событие только что

            if (triggerTicks == gameTicks)
            {
                callback.Call();
            }
        }
    }
}
