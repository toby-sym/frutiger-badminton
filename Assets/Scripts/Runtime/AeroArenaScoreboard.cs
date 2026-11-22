using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Mirrors the rally score on the generated arena display.</summary>
    public sealed class AeroArenaScoreboard : MonoBehaviour
    {
        static readonly int[] DigitMasks = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
        Renderer[] segments;
        AeroRallyController rally;
        int lastPlayerScore = -1;
        int lastRivalScore = -1;

        public void Initialize(Renderer[] digitSegments) => segments = digitSegments;

        void Start() => rally = FindFirstObjectByType<AeroRallyController>();

        void LateUpdate()
        {
            if (!rally || segments == null) return;
            int player = Mathf.Clamp(rally.PlayerScore, 0, 99);
            int rival = Mathf.Clamp(rally.RivalScore, 0, 99);
            if (player == lastPlayerScore && rival == lastRivalScore) return;
            lastPlayerScore = player;
            lastRivalScore = rival;
            SetDigit(0, player / 10);
            SetDigit(1, player % 10);
            SetDigit(2, rival / 10);
            SetDigit(3, rival % 10);
        }

        void SetDigit(int digit, int value)
        {
            int mask = DigitMasks[value];
            for (int segment = 0; segment < 7; segment++)
                segments[digit * 7 + segment].enabled = (mask & (1 << segment)) != 0;
        }
    }
}
