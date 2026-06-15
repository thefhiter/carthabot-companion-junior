using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Windows.Media;

namespace AdvancedProgramming.Communs
{

    public class MyCompletionData : ICompletionData
    {
        public MyCompletionData(string text)
        {
            Text = text;
        }

        public ImageSource Image => null;

        public string Text { get; private set; }

        public object Content => Text;

        public object Description => "Python keyword: " + Text;

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // Replace the whole current word (not just insert)
            var word = textArea.Document.GetText(completionSegment);
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
