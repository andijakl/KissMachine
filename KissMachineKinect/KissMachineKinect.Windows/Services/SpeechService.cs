using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace KissMachineKinect.Services
{
    public class SpeechService
    {
        private const string PreferredLanguage = "de";
        private SpeechSynthesizer _synthesizer;
        private readonly MediaElement _speakerMediaElement;

        public SpeechService(MediaElement speakerMedia)
        {
            _speakerMediaElement = speakerMedia;
        }

        public void Init()
        {
            if (_synthesizer == null)
            {
                _synthesizer = new SpeechSynthesizer();
            }
            // get all of the installed voices
            var voices = SpeechSynthesizer.AllVoices;
            foreach (var curVoice in voices)
            {
                // Select German voice if available
                if (curVoice.Language.Contains(PreferredLanguage))
                {
                    Debug.WriteLine("Found preferred voice");
                    _synthesizer.Voice = curVoice;
                    break;
                }
            }
        }

        public void Suspend()
        {
            _synthesizer.Dispose();
            _synthesizer = null;
        }

        public async Task SpeakTextAsync(string textToSpeak)
        {
            Debug.WriteLine(_speakerMediaElement.CurrentState);
            if (_speakerMediaElement.CurrentState == MediaElementState.Playing ||
                _speakerMediaElement.CurrentState == MediaElementState.Buffering ||
                _speakerMediaElement.CurrentState == MediaElementState.Opening)
            {
                _speakerMediaElement.Stop();
            }

            // create the data stream
            SpeechSynthesisStream synthesisStream;
            try
            {
                //creating a stream from the text which can be played using media element. This new API converts text input into a stream.
                synthesisStream = await _synthesizer.SynthesizeTextToStreamAsync(textToSpeak);
            }
            catch (Exception)
            {
                synthesisStream = null;
            }
            // if the SSML stream is not in the correct format throw an error message to the user
            if (synthesisStream == null)
            {
                return;
            }


            // start this audio stream playing
            _speakerMediaElement.AutoPlay = true;
            _speakerMediaElement.SetSource(synthesisStream, synthesisStream.ContentType);
            _speakerMediaElement.Play();
            await _synthesizer.SynthesizeTextToStreamAsync(textToSpeak);
        }
    }
}
