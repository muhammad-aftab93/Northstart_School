using System.Collections.Concurrent;
using System.Globalization;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using EnterpriseUI.Editors.Interactivity;

namespace TestDataGrid.Services;

/// <summary>Dictation with the speech recognizer installed in Windows; audio is streamed in as it is captured.</summary>
public sealed class WindowsSpeechProvider : ISpeechRecognitionProvider
{
    public Task<ISpeechRecognitionStream> StartAsync(SpeechRecognitionOptions options, CancellationToken cancellationToken)
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers();
        var culture = CultureInfo.GetCultureInfo(options.Language);
        var recognizer = installed.FirstOrDefault(item => item.Culture.Equals(culture))
            ?? installed.FirstOrDefault(item => item.Culture.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
            ?? throw new InvalidOperationException("No Windows speech recognizer is installed for " + options.Language + ". Add a speech language in Windows settings.");
        var format = options.Format ?? AudioFormat.Speech16kMono;
        return Task.FromResult<ISpeechRecognitionStream>(new RecognitionStream(new SpeechRecognitionEngine(recognizer), format, options.PartialResults));
    }

    private sealed class RecognitionStream : ISpeechRecognitionStream
    {
        private readonly SpeechRecognitionEngine _engine;
        private readonly BlockingStream _audio = new();
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecognitionStream(SpeechRecognitionEngine engine, AudioFormat format, bool partial)
        {
            _engine = engine;
            _engine.LoadGrammar(new DictationGrammar());
            _engine.SetInputToAudioStream(_audio, new SpeechAudioFormatInfo(format.SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            if (partial) _engine.SpeechHypothesized += (_, e) => Transcribed?.Invoke(this, new SpeechTranscript(e.Result.Text, false, e.Result.Confidence));
            _engine.SpeechRecognized += (_, e) => Transcribed?.Invoke(this, new SpeechTranscript(e.Result.Text, true, e.Result.Confidence));
            _engine.RecognizeCompleted += (_, _) => _completed.TrySetResult();
            _engine.RecognizeAsync(RecognizeMode.Multiple);
        }

        public event EventHandler<SpeechTranscript>? Transcribed;

        public ValueTask WriteAudioAsync(ReadOnlyMemory<byte> pcm, CancellationToken cancellationToken)
        {
            _audio.Enqueue(pcm.ToArray());
            return ValueTask.CompletedTask;
        }

        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            _audio.Complete();
            await Task.WhenAny(_completed.Task, Task.Delay(4000, CancellationToken.None)).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            _audio.Complete();
            _engine.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Read blocks until audio arrives; returns 0 after <see cref="Complete"/> so the engine sees the end of the stream.</summary>
    private sealed class BlockingStream : Stream
    {
        private readonly BlockingCollection<byte[]> _chunks = new();
        private byte[] _current = Array.Empty<byte>();
        private int _offset;
        private long _position;

        public void Enqueue(byte[] data)
        {
            if (!_chunks.IsAddingCompleted) _chunks.Add(data);
        }

        public void Complete()
        {
            if (!_chunks.IsAddingCompleted) _chunks.CompleteAdding();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_offset >= _current.Length)
            {
                if (!_chunks.TryTake(out var next, Timeout.Infinite)) return 0;
                _current = next;
                _offset = 0;
            }

            var length = Math.Min(count, _current.Length - _offset);
            Buffer.BlockCopy(_current, _offset, buffer, offset, length);
            _position += length;
            _offset += length;
            return length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        // SAPI asks for a length and position but never seeks a live stream.
        public override long Length => long.MaxValue;
        public override long Position { get => _position; set { } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _position;
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
