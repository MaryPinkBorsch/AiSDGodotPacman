using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node2D
{
	private enum FreezeType
	{
		None = 0,
		Ready = (1 << 1),  // новый раунд игры начался	
		EatGhost = (1 << 2),  // пакман схавал призрака
		Dead = (1 << 3),  // пакмана съели
		Won = (1 << 4),  // победа (съели все точки)
		GameOver = (1 << 5),  // проигрыш
		Reset = (1 << 6),  //  служебное
	}

	public enum FruitType
	{
		Cherries,
		Strawberry,
		Peach,
		Apple,
		Grapes,
		Galaxian,
		Bell,
		Key
	}

	// константы

	private readonly Vector2I fruitTile = new Vector2I(112, 140) / Maze.TileSize;

	private readonly int[] ghostEatenScores = new int[] { 200, 400, 800, 1600 };
	private readonly int[] fruitScores = new int[] { 100, 300, 500, 700, 1000, 2000, 3000, 5000 };
	private readonly int dotScore = 10;
	private readonly int pillScore = 50;

	private readonly int ghostFrightenedTicks = 6 * 60;
	private readonly int ghostEatenFreezeTicks = 60;
	private readonly int pacmanEatenFreezeTicks = 60;
	private readonly int pacmanDeathTicks = 150;
	private readonly int roundWonFreezeTicks = 4 * 60;
	private readonly int fruitActiveTicks = 560;

	// сцены пакмана и призраков

	[Export]
	private PackedScene pacmanScene;

	[Export]
	private PackedScene ghostScene;

	[Export]
	private Texture2D dotsTexture;

	[Export]
	private Texture2D readyTextTexture;

	[Export]
	private Texture2D gameOverTextTexture;

	[Export]
	private Texture2D lifeTexture;

	[Export]
	private Texture2D fruitTexture;

	private Label scoreText;
	private Label debugText; // элемент UI показывающий режим (нормальный и режим демонстрации пути)
	private Label highScoreText;
	private Sprite2D mazeSprite;
	private ColorRect ghostDoorSprite; // спрайты

	private Pacman pacman;
	private Ghost[] ghosts = new Ghost[4];

	// звуки

	private AudioStreamPlayer munch1Sound;
	private AudioStreamPlayer munch2Sound;
	private AudioStreamPlayer fruitSound;
	private AudioStreamPlayer ghostEatenSound;
	private AudioStreamPlayer sirenSound;
	private AudioStreamPlayer powerPelletSound;

	// переменные для управлния

	private int ticks;
	private int freeze;
	private int level;
	private int debugmode = 1; // режим демонстрации пути
	private int score;
	private int highScore;
	private int numGhostsEaten;
	private int numLifes;
	private int numDotsEaten;
	private int numDotsEatenThisRound;

	// триггеры

	private List<Trigger> triggers = new List<Trigger>();
	private Trigger dotEatenTrigger;
	private Trigger pillEatenTrigger;
	private Trigger ghostEatenUnFreezeTrigger;
	private Trigger pacmanEatenTrigger;
	private Trigger readyStartedTrigger;
	private Trigger roundStartedTrigger;
	private Trigger roundWonTrigger;
	private Trigger gameOverTrigger;
	private Trigger resetTrigger;
	private Trigger fruitActiveTrigger;
	private Trigger fruitEatenTrigger;
	private Trigger[] ghostFrightenedTrigger = new Trigger[4];
	private Trigger[] ghostEatenTrigger = new Trigger[4];

	// debug

	private List<Vector2I>[] ghostsPaths = new List<Vector2I>[4];

	//загрузка макс. очков

	private void LoadHighScore()
	{
		FileAccess highScoreFile = FileAccess.Open("user://highscore.data", FileAccess.ModeFlags.Read);

		if (highScoreFile != null)
		{
			highScore = (int)highScoreFile.Get32();
		}
		else
		{
			highScoreFile = FileAccess.Open("user://highscore.data", FileAccess.ModeFlags.Write);
			highScoreFile.Store32((uint)(highScore = 39870));
		}

		highScoreFile.Close();
	}

	private void SaveHighScore()
	{
		FileAccess highScoreFile = FileAccess.Open("user://highscore.data", FileAccess.ModeFlags.Write);
		highScoreFile.Store32((uint)highScore);
		highScoreFile.Close();
	}



	private void StopSounds()
	{
		sirenSound.Stop();
		powerPelletSound.Stop();
	}

	private void ResetTriggers()
	{
		foreach (Trigger t in triggers)
		{
			t.Reset(); // выключение триггеров
		}
	}

	private void DisableTriggers()
	{
		foreach (Trigger t in triggers)
		{
			t.Disable();
		}
	}

	private void ResetActors()
	{
		// пакман

		pacman.Visible = true;
		pacman.SetStartState();

		// призраки

		foreach (Ghost g in ghosts)
		{
			g.Visible = true;
			g.SetStartState();
		}
	}

	private void Reset()
	{
		// обнуление переменных управления

		ticks = 0;
		level = 1;
		score = 0;
		SetFreezeTo(FreezeType.Reset);
		numGhostsEaten = 0;
		numLifes = 3;
		numDotsEaten = 0;
		numDotsEatenThisRound = 0;

		StopSounds();

		// выключение триггеров и акторов

		ResetTriggers();
		ResetActors();
		Maze.Reset();

		// резет лабиринта

		mazeSprite.SelfModulate = new Color("417ae2");
		ghostDoorSprite.Visible = true;

		// подготовка и старт тригерра

		readyStartedTrigger.Start();
	}

	//сама игра

	private bool IsFrozen()
	{
		return freeze != (int)FreezeType.None;
	}

	private bool IsFrozenBy(FreezeType freezeType)
	{
		return (freeze & (int)freezeType) == (int)freezeType;
	}

	private void SetFreezeTo(FreezeType freezeType)
	{
		freeze = (int)freezeType;
	}

	private void FreezeBy(FreezeType freezeType)
	{
		freeze |= (int)freezeType;
	}

	private void UnFreeze()
	{
		freeze = (int)FreezeType.None;
	}

	private void UnFreezeBy(FreezeType freezeType)
	{
		freeze &= ~(int)freezeType;
	}

	// инициировать новый раунд (только сообщение о готовности)
	// это происходит, когда пакман теряет жизнь или в начале игры
	private void InitRound()
	{
		// выкл таймер

		DisableTriggers();

		// выкл акторов

		ResetActors();

		// зафризить

		SetFreezeTo(FreezeType.Ready);

		// проверка, выигран или проигран предыдущий раунд

		if (numDotsEaten >= Maze.NumDots)
		{
			numDotsEaten = 0;
			level++;
			Maze.Reset();
		}
		else
		{
			numLifes--;
		}

		// резет кол-ва съеденных точек

		numDotsEatenThisRound = 0;

		// резет лабиринта

		mazeSprite.SelfModulate = new Color("417ae2");
		ghostDoorSprite.Visible = true;
	}

	// Пакманчик

	// проверка надо ли пакману двигаться в этом тике

	private bool PacmanShouldMove()
	{
		if (dotEatenTrigger.IsActive())
		{
			return false;
		}
		else if (pillEatenTrigger.IsActive())
		{
			return false;
		}

		return true;
	}

	// Призраки

	// режимы призраков

	private Ghost.Mode GhostScatterChasePhase()
	{
		int s = roundStartedTrigger.TicksSinceStarted();

		if (s < 7 * 60) return Ghost.Mode.Scatter;
		else if (s < 27 * 60) return Ghost.Mode.Chase;
		else if (s < 34 * 60) return Ghost.Mode.Scatter;
		else if (s < 54 * 60) return Ghost.Mode.Chase;
		else if (s < 59 * 60) return Ghost.Mode.Scatter;
		else if (s < 79 * 60) return Ghost.Mode.Chase;
		else if (s < 84 * 60) return Ghost.Mode.Scatter;

		return Ghost.Mode.Chase;
	}

	// смена режимов

	private bool GhostLeaveHouse(Ghost g)
	{
		switch (g.type)
		{
			case Ghost.Type.Pinky:
				if (numDotsEatenThisRound >= 7)
				{
					return true;
				}
				break;
			case Ghost.Type.Inky:
				if (numDotsEatenThisRound >= 17)
				{
					return true;
				}
				break;
			case Ghost.Type.Clyde:
				if (numDotsEatenThisRound >= 30)
				{
					return true;
				}
				break;
		}

		return false;
	}

	private bool IsGhostFrightened(Ghost g)
	{
		return ghostFrightenedTrigger[(int)g.type].IsActive();
	}

	// и пакман и призраки

	private void UpdateDotsEaten()
	{
		numDotsEaten++;
		numDotsEatenThisRound++;

		// проверка на то что все точки сьели

		if (numDotsEaten >= Maze.NumDots)
		{
			roundWonTrigger.Start();
		}

		// заспавнить фрукты

		if (numDotsEaten == 70 || numDotsEaten == 170)
		{
			fruitActiveTrigger.Start();
		}

		// звук сьеденного фрукта

		if ((numDotsEaten & 1) != 0)
		{
			munch1Sound.Play();
		}
		else
		{
			munch2Sound.Play();
		}
	}

	private void UpdateActors()
	{
		// тик пакмана

		if (PacmanShouldMove())
		{
			pacman.Tick(ticks);
		}

		// поедание точек и гигаточек (которые пугают прзраков)

		Vector2I pacmanTile = pacman.PositionToTile();
		Maze.Tile mazeTile = Maze.GetTile(pacmanTile);

		if (mazeTile == Maze.Tile.Dot || mazeTile == Maze.Tile.Pill)
		{
			switch (mazeTile)
			{
				case Maze.Tile.Dot:
					dotEatenTrigger.Start();

					// плюсуем очки

					score += dotScore;

					break;
				case Maze.Tile.Pill:
					pillEatenTrigger.Start();

					//обнулить число сьеденных призраков

					numGhostsEaten = 0;

					// выставить режим Испуган всем призракам

					foreach (Ghost g in ghosts)
					{
						if (g.mode == Ghost.Mode.Chase || g.mode == Ghost.Mode.Scatter || g.mode == Ghost.Mode.Frightened)
						{
							ghostFrightenedTrigger[(int)g.type].Start();
						}
					}

					// увеличить очки

					score += pillScore;

					// звуки

					StopSounds();
					powerPelletSound.Play();

					break;
			}

			// очистить клетку и рповерить сьедены ли все точки

			Maze.SetTile(pacmanTile, Maze.Tile.Empty);

			UpdateDotsEaten();
		}

		// проверка на поедание фрукта

		if (fruitActiveTrigger.IsActive())
		{
			if (pacmanTile == fruitTile)
			{
				fruitActiveTrigger.Disable();
				fruitEatenTrigger.Start();



				score += fruitScores[(int)GetFruitTypeFromLevel(level)];

				// звук

				fruitSound.Play();
			}
		}

		// проверка если сьели призрака (или наоборот)

		foreach (Ghost g in ghosts)
		{
			if (pacman.PositionToTile() == g.PositionToTile())
			{
				if (g.mode == Ghost.Mode.Frightened)
				{
					//съели призрака

					//режим фриз

					FreezeBy(FreezeType.EatGhost);

					// режим глаза

					g.mode = Ghost.Mode.Eyes;

					// вырубить триггре

					ghostFrightenedTrigger[(int)g.type].Disable();

					// триггер сьеденного призрака

					ghostEatenUnFreezeTrigger.Start(ghostEatenFreezeTicks);
					ghostEatenTrigger[(int)g.type].Start();

					// очки++

					score += ghostEatenScores[numGhostsEaten];

					// увеличить кол-во сьеденных призраков

					numGhostsEaten++;



					ghostEatenSound.Play();
				}
				else if (g.mode == Ghost.Mode.Chase || g.mode == Ghost.Mode.Scatter)
				{
					// если сьели пакмана

					// фриз

					FreezeBy(FreezeType.Dead);

					// триггер сьеденного пакмана

					pacmanEatenTrigger.Start(pacmanEatenFreezeTicks);

					// проверка на число жизней

					if (numLifes >= 1)
					{
						// перезапуск раунда

						readyStartedTrigger.Start(pacmanEatenFreezeTicks + pacmanDeathTicks);
					}
					else
					{
						// конец игры

						gameOverTrigger.Start(pacmanEatenFreezeTicks + pacmanDeathTicks);
					}

					// вырубить звук

					StopSounds();
				}
			}
		}

		//тики призраков

		foreach (Ghost g in ghosts)
		{
			g.UpdateGhostMode(GhostLeaveHouse, IsGhostFrightened, GhostScatterChasePhase);
			g.UpdateTargetTile(pacman, ghosts);
			g.Tick(ticks);
		}
	}

	private void UpdatePacmanSprite()
	{
		if (IsFrozenBy(FreezeType.EatGhost))
		{
			pacman.Visible = false;
		}
		else if (IsFrozenBy(FreezeType.Dead))
		{
			pacman.Visible = true;

			if (pacmanEatenTrigger.IsActive())
			{
				int tick = pacmanEatenTrigger.TicksSinceStarted();
				pacman.SetDeathSpriteAnimation(tick);
			}
		}
		else if (IsFrozenBy(FreezeType.Ready))
		{
			pacman.Visible = true;
			pacman.SetStartRoundSprite();
		}
		else if (IsFrozenBy(FreezeType.GameOver))
		{
			pacman.Visible = false;
		}
		else
		{
			pacman.Visible = true;
			pacman.SetDefaultSpriteAnimation();
		}
	}

	private void UpdateGhostSprite(Ghost g)
	{
		// если сьели призрака

		if (ghostEatenTrigger[(int)g.type].IsActive())
		{
			g.Visible = true;
			g.SetScoreSprite(numGhostsEaten - 1);
		}
		else if (IsFrozenBy(FreezeType.Dead))
		{
			g.Visible = true;

			if (pacmanEatenTrigger.IsActive())
			{
				g.Visible = false;
			}
		}
		else if (IsFrozenBy(FreezeType.Won) || IsFrozenBy(FreezeType.GameOver))
		{
			g.Visible = false;
		}
		else
		{
			g.Visible = true;

			// выбор анимации

			switch (g.mode)
			{
				case Ghost.Mode.Frightened:
					int ticksSinceFrightened = ghostFrightenedTrigger[(int)g.type].TicksSinceStarted();
					int phase = (ticksSinceFrightened / 4) & 1;
					g.SetFrightenedSpriteAnimation(phase, ticksSinceFrightened > ghostFrightenedTicks - 60 && (ticksSinceFrightened & 0x10) != 0);
					break;
				case Ghost.Mode.EnterHouse:
				case Ghost.Mode.Eyes:
					g.SetEyesSprite();
					break;
				default:
					g.SetDefaultSpriteAnimation();
					break;
			}
		}
	}

	private FruitType GetFruitTypeFromLevel(int levelNumber)
	{
		switch (levelNumber)
		{
			case 1:
				return FruitType.Cherries;
			case 2:
				return FruitType.Strawberry;
			case 3:
			case 4:
				return FruitType.Peach;
			case 5:
			case 6:
				return FruitType.Apple;
			case 7:
			case 8:
				return FruitType.Grapes;
			case 9:
			case 10:
				return FruitType.Galaxian;
			case 11:
			case 12:
				return FruitType.Bell;
			default:
				return FruitType.Key;
		}
	}

	private void UpdateActorsSprites()
	{
		// пакман

		UpdatePacmanSprite();

		// призраки

		foreach (Ghost g in ghosts)
		{
			UpdateGhostSprite(g);
		}
	}

	// обновление очков

	private void UpdateScore()
	{
		scoreText.Text = (score == 0) ? "00" : score.ToString();

		if (score > highScore)
		{
			highScore = score;
		}

		highScoreText.Text = "HIGH SCORE\n" + ((highScore == 0) ? "00" : highScore.ToString());
	}

	//дебаг

	private void DrawGhostsPaths()
	{
		Color[] pathColors = new Color[4] { Color.Color8(255, 0, 0, 255), Color.Color8(252, 181, 255, 255), Color.Color8(0, 255, 255, 255), Color.Color8(248, 187, 85, 255) };
		Color targetTileColor = Color.Color8(127,127,127,255);
		Color BlinkyTileColor = Color.Color8(64,64,64,255);

		// следы полного страданий дебага
		// DrawRect(new Rect2I(ghosts[(int)Ghost.Type.Blinky].PositionToTile()*8, new Vector2I(10,10)),BlinkyTileColor);
		// DrawRect(new Rect2I(ghosts[(int)Ghost.Type.Blinky].targetTile * 8, new Vector2I(10,10)),targetTileColor);

		int pathLineWidth = 2;

		for (int i = 0; i < 4; i++)
		{
			List<Vector2I> path = ghostsPaths[i];

			if (path.Count > 0)
			{
				for (int j = 0; j < path.Count - 1; j++)
				{
					Vector2I p1 = path[j];
					Vector2I p2 = path[j + 1];
					Vector2I pathDirection = p2 - p1;

					Vector2I pathLineSize = Vector2I.Zero;

					switch (pathDirection.X)
					{
						case 0:
							pathLineSize.X = pathLineWidth;
							break;
						case 1:
							pathLineSize.X = 8 + pathLineWidth;
							break;
						case -1:
							pathLineSize.X = -8;
							break;
					}

					switch (pathDirection.Y)
					{
						case 0:
							pathLineSize.Y = pathLineWidth;
							break;
						case 1:
							pathLineSize.Y = 8 + pathLineWidth;
							break;
						case -1:
							pathLineSize.Y = -8;
							break;
					}

					DrawRect(new Rect2I(p1 * 8 + new Vector2I(3, 3), pathLineSize), pathColors[i]);
				}

				DrawRect(new Rect2I(path[path.Count - 1] * 8 + Vector2I.One * ((8 - pathLineWidth * 2) >> 1), new Vector2I(pathLineWidth, pathLineWidth) * 2), pathColors[i]);
			}
		}
	}

	private void CalculateGhostsPaths()
	{
		for (int i = 0; i < 4; i++)
		{
			if (ghosts[i].DistanceToTileMid() == Vector2I.Zero)
			{
				//ghosts[i].GetCurrentPath(ghostsPaths[i], 17);
				ghosts[i].GetCurrentPathAStar(ghostsPaths[i], pacman.PositionToTile());
			}
		}
	}

	// Вызывается, когда узел впервые попадает в дерево сцены
	public override void _Ready()
	{
		// создание триггеров

		triggers.Add(dotEatenTrigger = new Trigger());
		triggers.Add(pillEatenTrigger = new Trigger(3));
		triggers.Add(readyStartedTrigger = new Trigger(Callable.From(() =>
		{
			InitRound();
			roundStartedTrigger.Start(2 * 60);
		})));
		triggers.Add(roundStartedTrigger = new Trigger(Callable.From(() =>
		{
			UnFreeze();

			StopSounds();
			sirenSound.Play();
		})));
		triggers.Add(roundWonTrigger = new Trigger(Callable.From(() =>
		{
			FreezeBy(FreezeType.Won);
			readyStartedTrigger.Start(roundWonFreezeTicks);

			StopSounds();
		})));
		triggers.Add(gameOverTrigger = new Trigger(Callable.From(() =>
		{
			DisableTriggers();
			SetFreezeTo(FreezeType.GameOver);
			StopSounds();

			if (score >= highScore)
			{
				SaveHighScore();
			}

			resetTrigger.Start(3 * 60);
		})));
		triggers.Add(resetTrigger = new Trigger(Callable.From(() =>
		{
			Reset();
		})));
		triggers.Add(fruitActiveTrigger = new Trigger(fruitActiveTicks));
		triggers.Add(fruitEatenTrigger = new Trigger(2 * 60)); // show fruit score for 2 secs
		triggers.Add(pacmanEatenTrigger = new Trigger(pacmanDeathTicks));
		triggers.Add(ghostEatenUnFreezeTrigger = new Trigger(Callable.From(() =>
		{
			UnFreezeBy(FreezeType.EatGhost);
		})));

		for (int i = 0; i < 4; i++)
		{
			triggers.Add(ghostFrightenedTrigger[i] = new Trigger(ghostFrightenedTicks));
			triggers.Add(ghostEatenTrigger[i] = new Trigger(ghostEatenFreezeTicks));
		}

		// получение узлов

		scoreText = GetNode<Label>("Score");
		debugText = GetNode<Label>("Debug");
		highScoreText = GetNode<Label>("HighScore");
		mazeSprite = GetNode<Sprite2D>("Maze");
		ghostDoorSprite = GetNode<ColorRect>("GhostDoor");

		munch1Sound = GetNode<AudioStreamPlayer>("Munch1Sound");
		munch2Sound = GetNode<AudioStreamPlayer>("Munch2Sound");
		fruitSound = GetNode<AudioStreamPlayer>("FruitSound");
		ghostEatenSound = GetNode<AudioStreamPlayer>("GhostEatenSound");
		sirenSound = GetNode<AudioStreamPlayer>("SirenSound");
		powerPelletSound = GetNode<AudioStreamPlayer>("PowerPelletSound");

		// создатьб пакмана

		pacman = (Pacman)pacmanScene.Instantiate();
		AddChild(pacman);

		// создатьб призраков

		for (int i = 0; i < 4; i++)
		{
			ghosts[i] = (Ghost)ghostScene.Instantiate();
			ghosts[i].type = (Ghost.Type)i;
			AddChild(ghosts[i]);
		}

		// пути

		for (int i = 0; i < 4; i++)
		{
			ghostsPaths[i] = new List<Vector2I>();
		}

		// очки

		LoadHighScore();
		Reset();

		//спрятать курсор мышы

		DisplayServer.MouseSetMode(DisplayServer.MouseMode.Hidden);
	}

	// рисование путей

	public override void _Draw()
	{
		// пути

		if (Input.IsKeyPressed(Godot.Key.F1))
			debugmode = 1;
		if (Input.IsKeyPressed(Godot.Key.F2))
			debugmode = 0;

		debugText.Text = (debugmode == 1) ? "DEBUG" : "";

		if (debugmode == 1)
			DrawGhostsPaths();

		// нарисоавть точки и гигаточки

		for (int j = 0; j < Maze.Height; j++)
		{
			for (int i = 0; i < Maze.Width; i++)
			{
				Rect2 dotRect = new Rect2(new Vector2(i * Maze.TileSize, j * Maze.TileSize), new Vector2(Maze.TileSize, Maze.TileSize));

				switch (Maze.GetTile(new Vector2I(i, j)))
				{
					case Maze.Tile.Dot:
						DrawTextureRectRegion(dotsTexture, dotRect, new Rect2(Vector2.Zero, new Vector2(Maze.TileSize, Maze.TileSize)));
						break;
					case Maze.Tile.Pill:
						if ((ticks & 8) != 0 || freeze != 0)
						{
							DrawTextureRectRegion(dotsTexture, dotRect, new Rect2(new Vector2(Maze.TileSize, 0), new Vector2(Maze.TileSize, Maze.TileSize)));
						}
						break;
				}
			}
		}

		// текст нарисовать

		if (IsFrozenBy(FreezeType.Ready))
		{
			DrawTexture(readyTextTexture, new Vector2I(89, 131));
		}

		// нарисовать гейм овер текст

		if (IsFrozenBy(FreezeType.GameOver))
		{
			DrawTexture(gameOverTextTexture, new Vector2I(73, 131));
		}

		// анимация победы

		if (IsFrozenBy(FreezeType.Won))
		{
			int ticksSinceWon = roundWonTrigger.TicksSinceStarted();
			mazeSprite.SelfModulate = (ticksSinceWon & 16) != 0 ? new Color("417ae2") : new Color("ffffff");
			ghostDoorSprite.Visible = false;
		}

		// нарисовать жизни

		for (int i = 0; i < numLifes; i++)
		{
			DrawTexture(lifeTexture, new Vector2I(16 + 16 * i, 248));
		}

		// рисовка фруктов отображающих уровень

		int levelStart = level - 7 > 0 ? level - 7 : 0;

		for (int i = levelStart; i < level; i++)
		{
			int fruitIndex = (int)GetFruitTypeFromLevel(i + 1);
			DrawTextureRectRegion(fruitTexture, new Rect2I(new Vector2I(188 - 16 * (i - levelStart), 248), new Vector2I(24, 16)), new Rect2I(new Vector2I(0, fruitIndex * 16), new Vector2I(24, 16)));
		}

		// рисовка фруктов 

		if (fruitActiveTrigger.IsActive())
		{
			int fruitIndex = (int)GetFruitTypeFromLevel(level);
			DrawTextureRectRegion(fruitTexture, new Rect2I(new Vector2I(100, 132), new Vector2I(24, 16)), new Rect2I(new Vector2I(0, fruitIndex * 16), new Vector2I(24, 16)));
		}
		else if (fruitEatenTrigger.IsActive())
		{
			int fruitIndex = (int)GetFruitTypeFromLevel(level);
			DrawTextureRectRegion(fruitTexture, new Rect2I(new Vector2I(100, 132), new Vector2I(24, 16)), new Rect2I(new Vector2I(24, fruitIndex * 16), new Vector2I(24, 16)));
		}
	}

	// 60 фпс

	public override void _PhysicsProcess(double delta)
	{
		// фулл экран

		if (Input.IsActionJustPressed("ToggleFullscreen"))
		{
			Window window = GetWindow();

			if (window.Mode != Window.ModeEnum.ExclusiveFullscreen)
			{
				window.Mode = Window.ModeEnum.ExclusiveFullscreen;
			}
			else
			{
				window.Mode = Window.ModeEnum.Windowed;
			}
		}



		if (Input.IsActionJustPressed("Reset"))
		{
			Reset();
		}

		// обновить триггеры

		foreach (Trigger t in triggers)
		{
			t.Tick(ticks);
		}

		// смена звука

		if (powerPelletSound.Playing)
		{
			bool changeToSiren = true;

			foreach (Ghost g in ghosts)
			{
				if (IsGhostFrightened(g))
				{
					changeToSiren = false;
					break;
				}
			}

			if (changeToSiren)
			{
				StopSounds();
				sirenSound.Play();
			}
		}

		// обновить акторов

		if (!IsFrozen())
		{
			UpdateActors();
		}

		// обновитьо чки

		UpdateScore();

		// обновить спрайты

		UpdateActorsSprites();

		// дебаг

		if (debugmode == 1)
			CalculateGhostsPaths();

		// редрав

		QueueRedraw();

		// увеличить тики

		ticks++;
	}
}
