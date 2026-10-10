using System;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// <c>--arcade MODE</c>: once the game reaches the main menu, open that arcade card and press its Start,
    /// the way a player does (the card's own modal, through <see cref="Inspector.Arcade"/>). Prisma's STUDIOS
    /// page uses it for PLAY IN ENGINE: the game's own vessel in its own mode, one click from the studio.
    /// It never skips the menu or the first-run prompts (a new profile still answers them), and the pilot
    /// presses Ready in the race as usual.
    /// </summary>
    internal static class ArcadeAutoStart
    {
        public static string Mode;

        const string MenuScene = "Menu_Main";
        const int SettleFrames = 90, StartAfter = 45;   // let the menu finish building; let the card's modal open
        static int _menuSince = -1, _cardAt = -1;
        static bool _done;

        public static void Tick(int frame)
        {
            if (string.IsNullOrWhiteSpace(Mode) || _done) return;
            string scene = SceneManager.GetActiveScene()?.name ?? "";
            if (_cardAt < 0)
            {
                if (scene != MenuScene) { _menuSince = -1; return; }
                if (_menuSince < 0) _menuSince = frame;
                if (frame - _menuSince < SettleFrames) return;
                Console.WriteLine($"[arcade] --arcade {Mode}: opening the card");
                Inspector.Arcade(Mode);
                _cardAt = frame;
                return;
            }
            if (frame - _cardAt < StartAfter) return;
            Inspector.Arcade("start");
            _done = true;
        }
    }
}
