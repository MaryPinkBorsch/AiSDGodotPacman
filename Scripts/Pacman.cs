using Godot;
using System;

public partial class Pacman : Actor
{
    // онимация ням

    private static readonly int[] animationFramePhase = new int[] { 1, 0, 1, 2 };

    // выставка в старт позицию

    public void SetStartState()
    {
        Position = new Vector2I(112, 188);
        direction = Direction.Left;
        animationTick = 0;
        SetStartRoundSprite();
    }

    // считать позицию

    public Vector2I GetPacmanPosition()
    {
        return (Vector2I)Position;
    }

    private Direction GetInputDirection()
    {
        if (Input.IsActionPressed("Right"))
            return Direction.Right;
        else if (Input.IsActionPressed("Left"))
            return Direction.Left;
        else if (Input.IsActionPressed("Up"))
            return Direction.Up;
        else if (Input.IsActionPressed("Down"))
            return Direction.Down;

        return direction;
    }

    // спраты

    public void SetStartRoundSprite()
    {
        FrameCoords = new Vector2I(2, (int)Direction.Left);
    }

    public void SetDefaultSpriteAnimation()
    {
        int phase = (animationTick / 2) & 3;
        FrameCoords = new Vector2I(animationFramePhase[phase], (int)direction);
    }

    public void SetDeathSpriteAnimation(int tick)
    {
        int phase = 3 + tick / 8;

        if (phase >= 14)
        {
            Visible = false;
        }

        phase = Mathf.Clamp(phase, 3, 13);
        FrameCoords = new Vector2I(phase, 0);
    }

    //в начале игры выхов

    public override void _Ready()
    {
        direction = Direction.Left;
    }

    //тики

    public override void Tick(int ticks)
    {
        // движение

        Direction oldDirection = direction;
        direction = GetInputDirection();

        //проверка можно ли двигаться

        if (!CanMove(true))
        {
            // если нет, то меняем направление на старое

            direction = oldDirection;
        }

        // проверка движения в текущем направлении

        if (CanMove(true))
        {
            Move(true);
            animationTick++;
        }
    }
}
