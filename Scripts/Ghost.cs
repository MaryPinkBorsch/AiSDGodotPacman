using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

public partial class Ghost : Actor
{
	public enum Type //канонично смешные имена))
	{
		Blinky,
		Pinky,
		Inky,
		Clyde
	}

	public enum Mode // енам для разных "режимов  работы призраков" - погоня, побег,
					 // когда испуган, только глаза, когда он заперт в доме, когда выходит/заходит в дом
	{
		Chase,
		Scatter,
		Frightened,
		Eyes,
		InHouse,
		LeaveHouse,
		EnterHouse
	}

	public delegate bool LeaveHouseCallback(Ghost g);
	public delegate bool IsFrightenedCallback(Ghost g);
	public delegate Mode ScatterChasePhaseCallback();

	// some constants between ghosts

	private static readonly Vector2I[] scatterTiles = new Vector2I[4]
	{
		new Vector2I(25, -3), new Vector2I(2, -3), new Vector2I(27, 31), new Vector2I(0, 31)
	};

	private static readonly Vector2I[] startPositions = new Vector2I[]
	{
		new Vector2I(112, 92), new Vector2I(112, 116), new Vector2I(96, 116), new Vector2I(128, 116)
	};

	private static readonly Vector2I[] housePositions = new Vector2I[]
	{
		new Vector2I(112, 116), new Vector2I(112, 116), new Vector2I(96, 116), new Vector2I(128, 116)
	};

	private static readonly Random rand = new Random();

	public Type type = Type.Blinky;
	public Mode mode = Mode.Chase;
	public Vector2I targetTile = Vector2I.Zero;
	public Direction nextDirection;

	private Direction GetReverseDirection(Direction dir)
	{
		switch (dir)
		{
			case Direction.Right: return Direction.Left;
			case Direction.Left: return Direction.Right;
			case Direction.Up: return Direction.Down;
			case Direction.Down: return Direction.Up;
		}

		return Direction.Right;
	}

	public void SetStartState()
	{
		//начальная позиция

		Position = startPositions[(int)type];
		animationTick = 0;

		// нач режим

		switch (type)
		{
			case Type.Blinky:
				mode = Mode.Chase;
				nextDirection = direction = Direction.Left;
				break;
			case Type.Pinky:
				mode = Mode.InHouse;
				nextDirection = direction = Direction.Down;
				break;
			default:
				mode = Mode.InHouse;
				nextDirection = direction = Direction.Up;
				break;
		}
	}

	public Vector2I GetHousePosition()
	{
		return housePositions[(int)type];
	}

	public Vector2I GetScatterTile()
	{
		return scatterTiles[(int)type];
	}

	// получаем плитку погони за призраком, ему нужно знать позицию Пакмана, также Инки использует позицию блинки
	// чтобы определить свою целевую плитку, поэтому нам нужно передать массив со всеми призраками

	// важная часть! (добав а*)
	public Vector2I GetChaseTile(Pacman pacman, Ghost[] ghosts) //то куда бегут призраки когда преследуют Пакмана
	{
		Vector2I pacmanDirectionVector = pacman.GetDirectionVector();
		Vector2I pacmanTile = pacman.PositionToTile();

		switch (type)
		{
			case Type.Blinky:
				{
					//return pacmanTile;
					var blinkyPath = AStarPathfinder.FindPath(PositionToTile(), pacmanTile, Maze.IsTileTraversable, Maze.Width, Maze.Height);
					return blinkyPath[0]; // возвращаем первый шаг из пути построенного А*
				}
			case Type.Pinky:
				if (pacmanDirectionVector.Y < 0)
				{
					return pacmanTile + new Vector2I(-4, -4);
				}
				else
				{
					return pacmanTile + 4 * pacmanDirectionVector;
				}
			case Type.Inky:
				Vector2I blinkyTile = ghosts[(int)Type.Blinky].PositionToTile();
				Vector2I tileInPacmanDirection;

				if (pacmanDirectionVector.Y < 0)
				{
					tileInPacmanDirection = pacmanTile + new Vector2I(-2, -2);
				}
				else
				{
					tileInPacmanDirection = pacmanTile + 2 * pacmanDirectionVector;
				}

				return tileInPacmanDirection + (tileInPacmanDirection - blinkyTile);
			case Type.Clyde:
				Vector2I ghostTile = PositionToTile();
				int distanceToPacman = (pacmanTile - ghostTile).LengthSquared();

				if (distanceToPacman < 8 * 8)
				{
					return GetScatterTile();
				}
				else
				{
					return pacmanTile;
				}
		}

		return Vector2I.Zero;
	}

	public void UpdateTargetTile(Pacman pacman, Ghost[] ghosts)
	{
		if (type == Type.Blinky && mode != Mode.Eyes)
			targetTile = GetChaseTile(pacman, ghosts);
		else
		{
			switch (mode)
			{
				case Mode.Chase:
					targetTile = GetChaseTile(pacman, ghosts);
					break;
				case Mode.Scatter:
					targetTile = scatterTiles[(int)type];
					break;
				case Mode.Frightened:
					targetTile = new Vector2I(rand.Next(Maze.Width), rand.Next(Maze.Height));
					break;
				case Mode.Eyes:
					targetTile = new Vector2I(13, 11);
					break;
				default:
					targetTile = Vector2I.Zero;
					break;
			}
		}
	}

	/*
* получить следующее направление, в котором призрак должен продолжить движение, если возвращается true, направление принудительное,
* это означает, что призрак должен продолжить движение в этом направлении, даже если столкнется со стеной, проходящей через него
* это необходимо при пересечении двери призрака
*/

	private bool GetNextDirection()
	{
		// проверить, находится ли призрак в доме или входит или выходит из него

		Vector2I pos = (Vector2I)Position;

		switch (mode)
		{
			case Mode.InHouse:
				if (pos.Y <= 14 * Maze.TileSize)
				{
					nextDirection = Direction.Down;
				}
				else if (pos.Y >= 15 * Maze.TileSize)
				{
					nextDirection = Direction.Up;
				}

				direction = nextDirection;


				return true;
			case Mode.LeaveHouse:

				// если он перед дверью

				if (pos.X == 112)
				{
					if (pos.Y > 92)
					{
						nextDirection = Direction.Up;
					}
				}
				else
				{
					// если не перед дверью, то сначала идем к Y = 116

					if (pos.Y < 116)
					{
						nextDirection = Direction.Down;
					}
					else if (pos.Y > 116)
					{
						nextDirection = Direction.Up;
					}
					else
					{
						// если он уже на Y = 116, то переходим к двери x = 112

						nextDirection = (pos.X < 112) ? Direction.Right : Direction.Left;
					}
				}

				direction = nextDirection;

				return true;
			case Mode.EnterHouse:

				// идем к двери

				if (pos.X != 112 && pos.Y < 116)
				{
					nextDirection = (pos.X < 112) ? Direction.Right : Direction.Left;
				}
				else
				{
					// от двери вниз

					if (pos.Y < 116)
					{
						nextDirection = Direction.Down;
					}
					else
					{
						// домой

						nextDirection = pos.X < housePositions[(int)type].X ? Direction.Right : Direction.Left;
					}
				}

				direction = nextDirection;

				return true;
		}

		// если он находится в режиме преследования, разбегания или испуга

		Vector2I distanceToTileMid = DistanceToTileMid();

		if (distanceToTileMid.X == 0 && distanceToTileMid.Y == 0) // вычисляем следующее направление только если он находится в середине плитки
		{
			direction = nextDirection;
			Vector2I lookAheadTile = PositionToTile() + GetDirectionVector();

			Vector2I[] neightbourTiles = new Vector2I[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(0, 1) };

			for (int i = 0; i < 4; i++)
			{
				neightbourTiles[i] += lookAheadTile;
			}

			// для каждой возможной плитки пересечения проверка расстояние до целевой плитки

			int lowestDistance = int.MaxValue;
			Direction[] testDirections = new Direction[] { Direction.Up, Direction.Left, Direction.Down, Direction.Right }; // the ghost prefers directions in this order

			foreach (Direction d in testDirections)
			{
				//если призрак не испуган то нельзя в красн зону

				if (mode != Mode.Frightened)
				{
					if (d == Direction.Up && Maze.IsRedZone(lookAheadTile))
					{
						continue;
					}
				}

				// нельзя в обр направлении идти

				if (d != GetReverseDirection(direction) && Maze.GetTile(neightbourTiles[(int)d]) != Maze.Tile.Wall)
				{
					int distanceToTarget = (targetTile - neightbourTiles[(int)d]).LengthSquared();

					if (distanceToTarget < lowestDistance)
					{
						lowestDistance = distanceToTarget;
						nextDirection = d;
					}
				}
			}
		}

		return false;
	}

	// обнов режим

	public void UpdateGhostMode(LeaveHouseCallback leaveHouse, IsFrightenedCallback isFrightened, ScatterChasePhaseCallback scatterChasePhase)
	{
		Mode newMode = mode;
		Vector2I pos = (Vector2I)Position;

		switch (newMode)
		{
			case Mode.InHouse:
				if (leaveHouse(this))
				{
					newMode = Mode.LeaveHouse;
				}
				break;
			case Mode.LeaveHouse:
				// призраки немедленно переключаются в режим разбегания после того, как покидают дом призраков

				if (pos.Y == 92)
				{
					newMode = Mode.Scatter;
				}
				break;
			case Mode.EnterHouse:
				// проверить, находится ли привидение внутри дома
				Vector2I housePosition = GetHousePosition();

				if (IsNearEqual(housePosition, 1))
				{
					newMode = Mode.LeaveHouse;
				}

				break;
			case Mode.Eyes:
				if (IsNearEqual(new Vector2I(13 * 8 + 4, 11 * 8 + 4), 1))
				{
					newMode = Mode.EnterHouse;
				}
				break;
			default:
				// проверить, должно ли привидение быть в испуганном состоянии

				if (isFrightened(this))
				{
					newMode = Mode.Frightened;
				}
				else
				{
					// если нет, череда погоню и разброс

					newMode = scatterChasePhase();
				}

				break;
		}

		// если новое состояние отличается от предыдущего, обработать переходы между состояниями
		if (newMode != mode)
		{
			switch (mode)
			{
				case Mode.LeaveHouse:
					// из дома налево
					nextDirection = direction = Direction.Left;
					break;
				case Mode.EnterHouse:

					break;
				case Mode.Scatter:
				case Mode.Chase:
					// любой переход из режима разбегания в режим погони приводит к изменению направления

					nextDirection = GetReverseDirection(direction);
					break;
			}

			mode = newMode;
		}
	}

	// получает количество пикселей, на которое должен переместиться призрак

	private int GetSpeed(int ticks)
	{
		switch (mode) //если хочется хардкор с разной (ВЫСОКОЙ) скоростью, то можно закомментить надпись выше и призраки станут очень быстрыми
		{
			case Mode.InHouse:
			case Mode.LeaveHouse:
			case Mode.Frightened:
				return ticks & 1;
			case Mode.EnterHouse:
			case Mode.Eyes:
				return 2;
		}
		return (ticks % 3) == 0 ? 1 : 0; // это выставляет скорость передвижения призраков (чем выше число, тем медленнее призраки)

		//чек позиуции

		Vector2I tile = PositionToTile();

		if (Maze.GetTile(tile) == Maze.Tile.Tunnel)
		{
			return ticks & 1;
		}

		//движение чуть медленне пакмана

		return (ticks % 20) != 0 ? 1 : 0;
	}

	// спрайты

	public void SetDefaultSpriteAnimation()
	{
		int phase = (animationTick / 8) & 1;
		FrameCoords = new Vector2I(phase + 2 * (int)nextDirection, (int)type);
	}

	public void SetFrightenedSpriteAnimation(int phase, bool flashTick)
	{
		FrameCoords = new Vector2I(phase + (flashTick ? 10 : 8), 0);
	}

	public void SetScoreSprite(int scoreIndex)
	{
		FrameCoords = new Vector2I(scoreIndex + 8, 2);
	}

	public void SetEyesSprite()
	{
		FrameCoords = new Vector2I(8 + (int)nextDirection, 1);
	}

	// получить путь с помощью А*
	public void GetCurrentPathAStar(List<Vector2I> path, Vector2I pacmanposition)
	{
		path.Clear();

		if (mode != Mode.Chase && mode != Mode.Scatter && mode != Mode.Eyes)
			return;

		Vector2I start = PositionToTile();

		var goal = pacmanposition;

		var res = AStarPathfinder.FindPath(start, goal, Maze.IsTileTraversable, Maze.Width, Maze.Height);
		foreach (var step in res)
		{
			path.Add(step);
		}

	}

	// получить путь классическим образом
	public void GetCurrentPath(List<Vector2I> path, int maxDepth)
	{
		path.Clear();

		if (mode != Mode.Chase && mode != Mode.Scatter && mode != Mode.Eyes)
			return;

		Vector2I currentTile = PositionToTile();
		Direction currentDirection = direction;

		do
		{
			Vector2I[] neightbourTiles = new Vector2I[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(0, 1) };

			for (int i = 0; i < 4; i++)
			{
				neightbourTiles[i] += currentTile;
			}

			// для каждой возможной плитки пересечения проверить расстояние до целевой плитки
			int lowestDistance = int.MaxValue;
			Direction[] testDirections = new Direction[] { Direction.Up, Direction.Left, Direction.Down, Direction.Right }; // the ghost prefers directions in this order
			Direction chosenNextDirection = Direction.Left;

			foreach (Direction d in testDirections)
			{
				// в красных зонах нельязя идти вверх (если только призрак не находится в состоянии испуга)
				if (mode != Mode.Frightened)
				{
					if (d == Direction.Up && Maze.IsRedZone(currentTile))
					{
						continue;
					}
				}

				// назад идти нельзя

				if (d != GetReverseDirection(currentDirection) && Maze.GetTile(neightbourTiles[(int)d]) != Maze.Tile.Wall)
				{
					int distanceToTarget = (targetTile - neightbourTiles[(int)d]).LengthSquared();

					if (distanceToTarget < lowestDistance)
					{
						lowestDistance = distanceToTarget;
						chosenNextDirection = d;
					}
				}
			}

			currentDirection = chosenNextDirection;
			currentTile += directionsMap[(int)chosenNextDirection];
			path.Add(currentTile);

		} while (currentTile != targetTile && currentTile != PositionToTile() && path.Count < maxDepth);
	}

	// при начале игры

	public override void _Ready()
	{
		mode = Mode.Chase;
	}

	// тики
	public override void Tick(int ticks)
	{
		// движение рпизрака

		int numPixelsToMove = GetSpeed(ticks);

		for (int i = 0; i < numPixelsToMove; i++)
		{
			// рассчитать следующее направление (только в середине плитки)

			bool forcedMove = GetNextDirection();

			// идти по направлениеию

			if (CanMove(false) || forcedMove)
			{
				Move(false);
				animationTick++;
			}
		}
	}
}
