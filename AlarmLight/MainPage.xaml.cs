using System;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Devices.Gpio;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

// Inspired by: https://www.hackster.io/krvarma/rpivoice-051857

namespace AlarmLight
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Project Properties> Application> Click on Package Manifest> Capabilities> Make sure Microphone is checked.
        /// </summary>

        // Grammar File
        private const string SRGS_FILE = "Grammar\\SimpleGrammar.xml";
        // RED Led Pin
        private const int LIGHT_PIN = 5;
      
        // Tag TARGET
        private const string TAG_TARGET = "target";
        // Tag CMD
        private const string TAG_CMD = "cmd";
        // Tag Device
        private const string TAG_DEVICE = "device";
        // On State
        private const string STATE_ON = "ON";
        // Off State
        private const string STATE_OFF = "OFF";
        // LED Device
        private const string DEVICE_LED = "LED";
        // Light Device
        private const string DEVICE_LIGHT = "LIGHT";
        // Alarm 
        private const string TARGET_ALARM = "ALARM";


        // Speech Recognizer
        private SpeechRecognizer recognizer;

        // GPIO 
        private static GpioController gpio = null;

        // GPIO Pin for light
        private static GpioPin pin = null;
        

        public MainPage()
        {
            this.InitializeComponent();

            Unloaded += MainPage_Unloaded;

            // Initialize Recognizer
            initializeSpeechRecognizer();

            // Initialize GPIO controller and pins
            initializeGPIO();
        }

        private void initializeGPIO()
        {
            // Initialize GPIO controller
            gpio = GpioController.GetDefault();

            // Initialize GPIO Pin
            pin = gpio.OpenPin(LIGHT_PIN);
            
            // For output
            pin.SetDriveMode(GpioPinDriveMode.Output);
            
            // Relay is low switching, so turn off = high atf first instance
            pin.Write(GpioPinValue.High);
            
        }

        // Release resources, stop recognizer, release pins, etc...
        private async void MainPage_Unloaded(object sender, object args)
        {
            // Stop recognizing
            await recognizer.ContinuousRecognitionSession.StopAsync();

            // Release pins
            pin.Dispose();
            recognizer.Dispose();

            gpio = null;
            pin = null;
            recognizer = null;
        }

        // Initialize Speech Recognizer and start async recognition
        private async void initializeSpeechRecognizer()
        {
            // Initialize recognizer

            #region GermanSpeech
            // German did not work
            // recognizer = new SpeechRecognizer(new Language("de-DE"));

            /*
             * https://microsoft.hackster.io/en-US/krvarma/rpivoice-051857?ref=channel&ref_id=4087_trending___&offset=22
             TTS files:

            Copy folder "de-DE" from Windows 10:
            C:\Windows\Speech_OneCore\Engines\TTS\de-DE

            to Windows IoT:
            \\minwinpc\C$\Windows\Speech_OneCore\Engines\TTS\de-DE

            Copy folder "de-DE" from Windows 10:
            C:\Windows\System32\Speech_OneCore\Common\de-DE

            to Windows IoT:

            \\minwinpc\C$\Windows\System32\Speech_OneCore\de-DE

            Then call power shell command:
            (https://developer.microsoft.com/en-us/windows/iot/docs/powershell)
            Move-Item \\minwinpc\C$\Windows\System32\Speech_OneCore\de-DE\* \\minwinpc\C$\Windows\System32\Speech_OneCore\Common\de-DE\

            SR files:

            Copy folder "de-DE-N" from Windows 10:
            C:\Windows\Speech_OneCore\Engines\SR\de-DE-N

            to Windows IoT:
            \\minwinpc\C$\Windows\Speech_OneCore\Engines\SR\de-DE-N
            */
            #endregion

            recognizer = new SpeechRecognizer();

            // Set event handlers
            recognizer.StateChanged += RecognizerStateChanged;
            recognizer.ContinuousRecognitionSession.ResultGenerated += RecognizerResultGenerated;

            // Load Grammer file constraint
            string fileName = String.Format(SRGS_FILE);
            StorageFile grammarContentFile = await Package.Current.InstalledLocation.GetFileAsync(fileName);

            SpeechRecognitionGrammarFileConstraint grammarConstraint = new SpeechRecognitionGrammarFileConstraint(grammarContentFile);

            // Add to grammer constraint
            recognizer.Constraints.Add(grammarConstraint);

            // Compile grammer
            SpeechRecognitionCompilationResult compilationResult = await recognizer.CompileConstraintsAsync();

            Debug.WriteLine("Status: " + compilationResult.Status.ToString());

            // If successful, display the recognition result.
            if (compilationResult.Status == SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Result: " + compilationResult.ToString());

                await recognizer.ContinuousRecognitionSession.StartAsync();
            }
            else
            {
                Debug.WriteLine("Status: " + compilationResult.Status);
            }
        }

        // Recognizer generated results
        private void RecognizerResultGenerated(SpeechContinuousRecognitionSession session, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Output debug strings
            Debug.WriteLine(args.Result.Status);
            Debug.WriteLine(args.Result.Text);

            int count = args.Result.SemanticInterpretation.Properties.Count;

            Debug.WriteLine("Count: " + count);
            Debug.WriteLine("Tag: " + args.Result.Constraint.Tag);

            // Check for different tags and initialize the variables
            String target = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_TARGET) ?
                            args.Result.SemanticInterpretation.Properties[TAG_TARGET][0] :
                            "";

            String cmd = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_CMD) ?
                            args.Result.SemanticInterpretation.Properties[TAG_CMD][0] :
                            "";

            String device = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_DEVICE) ?
                            args.Result.SemanticInterpretation.Properties[TAG_DEVICE][0] :
                            "";

            // Whether state is on or off
            bool isOn = cmd.Equals(STATE_ON);

            Debug.WriteLine("Target: " + target + ", Command: " + cmd + ", Device: " + device);

            // First check which device the user refers to
            if (device.Equals(DEVICE_LED))
            {
                
            }
            else if (device.Equals(DEVICE_LIGHT))
            {
                // Check target location
                if (target.Equals(TARGET_ALARM))
                {
                    Debug.WriteLine("ALARM LIGHT " + (isOn ? STATE_ON : STATE_OFF));

                    // Turn on the alaram light
                    WriteGPIOPin(pin, isOn ? GpioPinValue.Low : GpioPinValue.High);
                }
                
                else
                {
                    Debug.WriteLine("Unknown Target");
                }
            }
            else
            {
                Debug.WriteLine("Unknown Device");
            }
        }

        // Recognizer state changed
        private void RecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine("Speech recognizer state: " + args.State);
        }

        // Control Gpio Pins
        private void WriteGPIOPin(GpioPin pin, GpioPinValue value)
        {
            pin.Write(value);
        }
    }
}

