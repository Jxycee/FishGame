// Core/Player/ToolStowAnim.cs: progress of the procedural rod stow / unstow (spec 7.3, R70; contract M2-D31). Pure, no assets.
// Amount 0 = out (rest pose), 1 = fully put away (hidden). Moves at 1 / Seconds per second toward the wanted end; Ease is a smoothstep.
// RodPresenter (every peer) owns one; Create snaps to the current tool, so a spawn or a late join never animates.
namespace FishGame.Core
{
    public struct ToolStowAnim
    {
        public const float Seconds = 0.2f;   // [tune]

        private float _amount;

        public static ToolStowAnim Create(bool shown)
        {
            return new ToolStowAnim { _amount = shown ? 0f : 1f };
        }

        public float Amount => _amount;
        public bool Visible => _amount < 1f;

        // Advances toward shown (0) or stowed (1); dt <= 0, NaN or infinite is ignored. Returns Ease(Amount).
        public float Tick(bool wantShown, float dt)
        {
            if (dt > 0f && !float.IsInfinity(dt))
            {
                float step = dt / Seconds;
                if (wantShown) _amount = _amount > step ? _amount - step : 0f;
                else _amount = _amount + step < 1f ? _amount + step : 1f;
            }

            return Ease(_amount);
        }

        public static float Ease(float t)
        {
            if (!(t > 0f)) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}
