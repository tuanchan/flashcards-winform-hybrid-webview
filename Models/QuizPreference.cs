using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TocflQuiz.Models
{
    public sealed class QuizPreference
    {
        public int AnswerMode { get; set; } = 0;
        public int Count { get; set; } = 0;
        public bool Multi { get; set; } = true;
        public bool Essay { get; set; } = false;
        public bool Sentence { get; set; } = false;
    }
}
