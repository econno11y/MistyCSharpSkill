using Google.Cloud.Dialogflow.V2;

namespace MistyVoiceCommandSkill.DataObjects
{
	internal class DetectIntentContent
	{
		public QueryInput queryInput { get; set; }
		public string inputAudio { get; set; }

		public OutputAudioConfig outputAudioConfig { get; set; }
	}
}
