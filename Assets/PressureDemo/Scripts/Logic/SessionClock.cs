namespace HexRMeta.Pressure
{
    /// <summary>
    /// Counts only the time spent actually playing. The caller feeds it delta time every frame,
    /// so pausing is just "stop calling Tick"; nothing here depends on Unity.
    /// </summary>
    public class SessionClock
    {
        private readonly float limitSeconds;

        public float ElapsedSeconds { get; private set; }
        public bool IsRunning { get; private set; }
        public bool LimitReached => limitSeconds > 0f && ElapsedSeconds >= limitSeconds;
        public float RemainingSeconds => limitSeconds > 0f ? System.Math.Max(0f, limitSeconds - ElapsedSeconds) : -1f;

        public SessionClock(float limitSeconds)
        {
            this.limitSeconds = limitSeconds;
        }

        public void Start()
        {
            ElapsedSeconds = 0f;
            IsRunning = true;
        }

        public void Pause() { IsRunning = false; }
        public void Resume() { IsRunning = true; }
        public void Stop() { IsRunning = false; }

        public void Tick(float deltaSeconds)
        {
            if (IsRunning)
                ElapsedSeconds += deltaSeconds;
        }

        public static string Format(float seconds)
        {
            int total = (int)seconds;
            return string.Format("{0:00}:{1:00}", total / 60, total % 60);
        }
    }
}
