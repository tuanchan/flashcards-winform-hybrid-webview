
namespace TocflQuiz.Services
{
    internal class VocabToastForm
    {
        private string key;
        private string han;
        private string pinyin;
        private string meaning;
        private int showSeconds;
        private Action<string, ToastAction> onAction;

        public VocabToastForm(string key, string han, string pinyin, string meaning, int showSeconds, Action<string, ToastAction> onAction)
        {
            this.key = key;
            this.han = han;
            this.pinyin = pinyin;
            this.meaning = meaning;
            this.showSeconds = showSeconds;
            this.onAction = onAction;
        }

        internal void Show()
        {
            throw new NotImplementedException();
        }
    }
}
