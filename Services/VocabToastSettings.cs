namespace TocflQuiz.Services
{
    public sealed class VocabToastSettings
    {
        public bool Enabled { get; set; } = false;

        // Ví dụ bạn nhập 0.1 phút = 6 giây
        public double EveryMinutes { get; set; } = 1.0;

        // toast hiện bao lâu (giây)
        public int ShowSeconds { get; set; } = 10;

        // ✅ mặc định: false = chạy lần lượt từ đầu -> cuối
        public bool RandomFromSet { get; set; } = false;

        // ✅ luôn bỏ qua từ đã "Thuộc"
        public bool SkipLearned { get; set; } = true;

        public VocabToastSettings Clone() => new VocabToastSettings
        {
            Enabled = Enabled,
            EveryMinutes = EveryMinutes,
            ShowSeconds = ShowSeconds,
            RandomFromSet = RandomFromSet,
            SkipLearned = SkipLearned
        };
    }
}
