using System;
using CosmicShore.Cli;
using CosmicShore.Data;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-G verification host (`--mode play [--game hexrace|crystalcapture]`): the
    /// WINDOWED game-mode host. The exact same rounds the CLI proves headlessly —
    /// verbatim worlds behind the steppable <see cref="IRoundDriver"/> split
    /// (HexRaceRoundHandle, CrystalCaptureRoundHandle; the CLI's blocking Run is the
    /// same handle in a while-loop, transcript-pinned) — stepped ONE engine frame per
    /// window update and rendered through the shared <see cref="RoundScenePass"/>
    /// (deterministic wireframe scene + round HUD). Fixed 1/60 stepping + sim-derived
    /// camera → `--screenshot` captures are byte-identical across runs.
    ///
    /// This host proves Arc G's "construction" criterion standalone; the
    /// menu→game→menu loop lives in the menushell host (Arc G part 2 / Arc I).
    /// </summary>
    public sealed class ModeHostWindow
    {
        readonly string _game;
        readonly int _seed;
        readonly int _playerCount;
        readonly int _crystalTarget;
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        UiRenderer _ui;
        RoundScenePass _scene;
        IRoundDriver _round;

        int _frameIndex;
        bool _raceDone;

        public ModeHostWindow(string game, int seed, int playerCount, int crystalTarget,
            string screenshotPath, int screenshotFrame)
        {
            _game = game;
            _seed = seed;
            _playerCount = playerCount;
            _crystalTarget = crystalTarget;
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        /// <summary>The shared driver factory (menushell's game phase uses it too).</summary>
        public static IRoundDriver CreateDriver(string game, int seed, int playerCount, int crystalTarget,
            Action<string> liveLog)
            => game switch
            {
                "crystalcapture" => CrystalCaptureRound.Setup(new CrystalCaptureRoundOptions
                {
                    PlayerCount = playerCount,
                    Seed = seed,
                    CrystalTarget = crystalTarget,
                }, liveLog),
                "joust" => JoustRound.Setup(new JoustRoundOptions
                {
                    PlayerCount = playerCount,
                    Seed = seed,
                    JoustTarget = crystalTarget,
                }, liveLog),
                "astroleague" => AstroLeagueRound.Setup(new AstroLeagueRoundOptions
                {
                    PlayerCount = playerCount,
                    Seed = seed,
                    GoalLimit = crystalTarget,
                }, liveLog),
                _ => HexRaceRound.Setup(new HexRaceRoundOptions
                {
                    PlayerCount = playerCount,
                    Seed = seed,
                    CrystalTarget = crystalTarget,
                }, liveLog),
            };

        /// <summary>Diag-friendly name: "HEX RACE" → "HexRace".</summary>
        public static string DiagName(IRoundDriver round)
        {
            var parts = round.GameLabel.ToLowerInvariant().Split(' ');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
            return string.Concat(parts);
        }

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "Cosmic Shore — mode host (port progress build)",
                VSync = true,
            };
            Console.WriteLine("[1/3] creating window (GLFW)...");
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _round?.Dispose();
        }

        void OnLoad()
        {
            Console.WriteLine("[2/3] window open — initializing GL/round...");
            _gl = GL.GetApi(_window);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.Enable(EnableCap.ProgramPointSize);
            _ui = new UiRenderer(_gl);
            _scene = new RoundScenePass(_gl);

            // The REAL round world — same construction the CLI gate proves headless.
            _round = CreateDriver(_game, _seed, _playerCount, _crystalTarget,
                line => Console.WriteLine("  " + line));

            Console.WriteLine($"[3/3] ready — {_round.GameLabel}, {_playerCount} AI pilots, first domain to {_crystalTarget}.");
        }

        void OnUpdate(double dt)
        {
            // ONE deterministic engine frame per window update (fixed 1/60 inside the
            // handle) — the windowed twin of the CLI's while-loop.
            if (!_raceDone && _round != null)
            {
                if (_round.StepFrame())
                {
                    _raceDone = true;
                    _round.FinishAndScore();
                }
                else if (_round.FramesStepped >= _round.MaxFrames)
                {
                    _raceDone = true;
                    _round.CompleteStepping();
                }
            }
            _frameIndex++;
        }

        void OnRender(double dt)
        {
            if (_round != null)
            {
                _scene.Render(_round, _window.FramebufferSize.X, _window.FramebufferSize.Y);
                _scene.DrawHud(_ui, _round, _window.FramebufferSize.X, _window.FramebufferSize.Y);
            }

            if (_screenshotPath != null && _frameIndex >= _screenshotFrame)
            {
                CaptureScreenshot();
                _window.Close();
            }
        }

        unsafe void CaptureScreenshot()
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            var pixels = new byte[w * h * 4];
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(_screenshotPath, pixels, w, h);

            string state = _round.Finished ? "Finished" : "Racing";
            string winner = _round.Finished ? _round.WinnerName : "none";
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"mode play, game {DiagName(_round)}, players {_playerCount}, target {_round.Target}, " +
                $"t {RoundScenePass.Clock(_round):0.00}, claims {_round.TotalClaims}, " +
                $"jade {RoundScenePass.DomainSum(_round, Domains.Jade)} ruby {RoundScenePass.DomainSum(_round, Domains.Ruby)} gold {RoundScenePass.DomainSum(_round, Domains.Gold)}, " +
                $"state {state}, winner {winner}");
        }
    }
}
